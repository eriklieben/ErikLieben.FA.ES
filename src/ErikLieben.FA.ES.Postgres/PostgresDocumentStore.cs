using System.Text.Json;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;
using ErikLieben.FA.ES.Postgres.Model;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed object document store. Uses each row's <c>xmin</c> system column as the
/// optimistic concurrency token: read returns xmin in <see cref="ObjectDocument.Hash"/>, and
/// <see cref="SetAsync"/> performs <c>UPDATE … WHERE xmin = $prev_xmin</c>.
/// </summary>
internal sealed class PostgresDocumentStore : IPostgresDocumentStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly EventStreamPostgresSettings settings;
    private readonly EventStreamDefaultTypeSettings typeSettings;
    private readonly IDocumentTagDocumentFactory documentTagFactory;
    private readonly PostgresSchemaBootstrapper bootstrapper;

    public PostgresDocumentStore(
        NpgsqlDataSource dataSource,
        EventStreamPostgresSettings settings,
        EventStreamDefaultTypeSettings typeSettings,
        IDocumentTagDocumentFactory documentTagFactory,
        PostgresSchemaBootstrapper bootstrapper)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(typeSettings);
        ArgumentNullException.ThrowIfNull(documentTagFactory);
        ArgumentNullException.ThrowIfNull(bootstrapper);
        this.dataSource = dataSource;
        this.settings = settings;
        this.typeSettings = typeSettings;
        this.documentTagFactory = documentTagFactory;
        this.bootstrapper = bootstrapper;
    }

    public async Task<IObjectDocument> CreateAsync(string objectName, string objectId, string? store = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        await bootstrapper.EnsurePartitionForObjectNameAsync(objectName).ConfigureAwait(false);

        var targetStore = store ?? settings.DefaultDocumentStore;
        var activeWire = new PostgresStreamInformation
        {
            StreamIdentifier     = $"{objectId.Replace("-", string.Empty)}-0000000000",
            StreamType           = typeSettings.StreamType,
            DocumentType         = typeSettings.DocumentType,
            DocumentTagType      = typeSettings.DocumentTagType,
            EventStreamTagType   = typeSettings.EventStreamTagType,
            DocumentRefType      = typeSettings.DocumentRefType,
            DataStore            = targetStore,
            DocumentStore        = targetStore,
            DocumentTagStore     = settings.DefaultDocumentTagStore,
            StreamTagStore       = settings.DefaultDocumentTagStore,
            SnapShotStore        = settings.DefaultSnapShotStore,
            CurrentStreamVersion = -1,
        };

        var activeJson = JsonSerializer.Serialize(activeWire, PostgresJsonContext.Default.PostgresStreamInformation);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using (var insertCmd = new NpgsqlCommand(
            "INSERT INTO faes_documents (object_name, object_id, active, terminated_streams) " +
            "VALUES ($1, $2, $3::jsonb, '[]'::jsonb) " +
            "ON CONFLICT (object_name, object_id) DO NOTHING", connection))
        {
            insertCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
            insertCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });
            insertCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = activeJson });
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        return await GetInternalAsync(connection, objectName, objectId).ConfigureAwait(false)
            ?? throw new PostgresProcessingException($"Document {objectName}/{objectId} could not be created.");
    }

    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        var doc = await GetInternalAsync(connection, objectName, objectId).ConfigureAwait(false);
        return doc ?? throw new PostgresProcessingException($"Document {objectName}/{objectId} not found.");
    }

    public async Task<IObjectDocument?> GetFirstByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var tagStoreType = documentTagStore ?? settings.DefaultDocumentTagStore;
        var tagStore = documentTagFactory.CreateDocumentTagStore(tagStoreType);
        var first = (await tagStore.GetAsync(objectName, tag).ConfigureAwait(false)).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? null : await GetAsync(objectName, first, store).ConfigureAwait(false);
    }

    public async Task<IEnumerable<IObjectDocument>> GetByDocumentByTagAsync(string objectName, string tag, string? documentTagStore = null, string? store = null)
    {
        var tagStoreType = documentTagStore ?? settings.DefaultDocumentTagStore;
        var tagStore = documentTagFactory.CreateDocumentTagStore(tagStoreType);
        var ids = await tagStore.GetAsync(objectName, tag).ConfigureAwait(false);
        var result = new List<IObjectDocument>();
        foreach (var id in ids)
        {
            result.Add(await GetAsync(objectName, id, store).ConfigureAwait(false));
        }
        return result;
    }

    public async Task SetAsync(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var activeWire = PostgresStreamInformation.From(document.Active);
        var activeJson = JsonSerializer.Serialize(activeWire, PostgresJsonContext.Default.PostgresStreamInformation);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);

        // PrevHash carries the xmin from the last load; absence means "no prior version known" → first save after create.
        if (!string.IsNullOrEmpty(document.PrevHash))
        {
            await using var cmd = new NpgsqlCommand(
                "UPDATE faes_documents SET active = $3::jsonb, schema_version = $4, updated_at = now() " +
                "WHERE object_name = $1 AND object_id = $2 AND xmin::text = $5 " +
                "RETURNING xmin::text", connection);
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = activeJson });
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)document.SchemaVersion ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.PrevHash });

            var newXmin = (string?)await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (newXmin is null)
            {
                throw new PostgresProcessingException(
                    $"Optimistic concurrency check failed for {document.ObjectName}/{document.ObjectId}: xmin mismatch.");
            }
            document.SetHash(newXmin, newXmin);
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO faes_documents (object_name, object_id, active, schema_version, terminated_streams) " +
                "VALUES ($1, $2, $3::jsonb, $4, '[]'::jsonb) " +
                "ON CONFLICT (object_name, object_id) DO UPDATE SET active = EXCLUDED.active, schema_version = EXCLUDED.schema_version, updated_at = now() " +
                "RETURNING xmin::text", connection);
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = activeJson });
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)document.SchemaVersion ?? DBNull.Value });

            var newXmin = (string?)await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            document.SetHash(newXmin, newXmin);
        }
    }

    private static async Task<IObjectDocument?> GetInternalAsync(NpgsqlConnection connection, string objectName, string objectId)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT active::text, terminated_streams::text, schema_version, xmin::text " +
            "FROM faes_documents WHERE object_name = $1 AND object_id = $2", connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = objectId });

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        var activeJson         = reader.GetString(0);
        var terminatedJson     = reader.GetString(1);
        var schemaVersion      = reader.IsDBNull(2) ? null : reader.GetString(2);
        var xmin               = reader.GetString(3);

        var activeWire = JsonSerializer.Deserialize(activeJson, PostgresJsonContext.Default.PostgresStreamInformation)
            ?? throw new PostgresProcessingException($"Unable to deserialize active stream info for {objectName}/{objectId}.");

        var terminated = JsonSerializer.Deserialize(terminatedJson, PostgresJsonContext.Default.ListPostgresTerminatedStream)
            ?? new List<PostgresTerminatedStream>();

        var doc = new PostgresObjectDocument(
            objectId,
            objectName,
            activeWire.ToStreamInformation(),
            terminated.Select(t => new TerminatedStream { StreamIdentifier = t.StreamIdentifier }),
            schemaVersion,
            xmin,
            xmin);
        return doc;
    }
}
