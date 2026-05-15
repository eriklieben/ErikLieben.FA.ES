using System.Runtime.CompilerServices;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;
using ErikLieben.FA.ES.Postgres.Model;
using Npgsql;
using NpgsqlTypes;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Postgres-backed <see cref="IDataStore"/>. Appends are atomic via the server-side
/// <c>faes_append</c> function; reads use the partitioned <c>faes_events</c> table with
/// partition-pruning by <c>object_name</c>.
/// </summary>
public sealed class PostgresDataStore : IDataStore, IDataStoreRecovery
{
    private readonly NpgsqlDataSource dataSource;
    private readonly EventStreamPostgresSettings settings;

    /// <summary>Initializes a new instance.</summary>
    public PostgresDataStore(NpgsqlDataSource dataSource, EventStreamPostgresSettings settings)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(settings);
        this.dataSource = dataSource;
        this.settings = settings;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("PostgresDataStore.Read");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.StartVersion, startVersion);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var list = new List<IEvent>();
        await foreach (var evt in ReadAsStreamAsyncCore(document, startVersion, untilVersion, chunk, cancellationToken).ConfigureAwait(false))
        {
            list.Add(evt);
        }
        return list.Count == 0 ? null : list;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);
        return ReadAsStreamAsyncCore(document, startVersion, untilVersion, chunk, cancellationToken);
    }

    private async IAsyncEnumerable<IEvent> ReadAsStreamAsyncCore(
        IObjectDocument document,
        int startVersion,
        int? untilVersion,
        int? chunk,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const string columns =
            "version, event_type, schema_version, payload, " +
            "correlation_id, causation_id, idempotent_key, originated_from_user, event_occured_at, " +
            "metadata, external_sequencer";
        var sql = untilVersion.HasValue
            ? $"SELECT {columns} FROM faes_events WHERE object_name = $1 AND stream_id = $2 AND version >= $3 AND version <= $4 ORDER BY version"
            : $"SELECT {columns} FROM faes_events WHERE object_name = $1 AND stream_id = $2 AND version >= $3 ORDER BY version";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = startVersion });
        if (untilVersion.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = untilVersion.Value });
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ReadEvent(reader);
        }
    }

    /// <inheritdoc />
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, cancellationToken, events);

    /// <inheritdoc />
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("PostgresDataStore.Append");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.EventCount, events?.Length ?? 0);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);
        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        var expectedVersion = document.Active.CurrentStreamVersion;
        var eventsJson = SerializeEvents(events, preserveTimestamp);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (settings.AppendMode == AppendMode.Exclusive)
            {
                await using var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, tx);
                lockCmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
                await lockCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var cmd = new NpgsqlCommand("SELECT * FROM faes_append($1, $2, $3, $4, $5)", connection, tx);
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectId });
            cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
            cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = expectedVersion });
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = eventsJson });

            // Drain the result set so server-side errors raised after RETURNING surface.
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Reflect the new stream version on the in-memory document so subsequent appends
            // in the same logical commit use the right expected version.
            document.Active.CurrentStreamVersion = expectedVersion + events.Length;
        }
        catch (PostgresException ex) when (ex.SqlState == "40001")
        {
            // serialization_failure raised by faes_append on version mismatch.
            throw new PostgresProcessingException(
                $"Optimistic concurrency conflict on stream '{document.Active.StreamIdentifier}': {ex.MessageText}", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // unique_violation — typically (object_name, stream_id, version) collision.
            throw new PostgresProcessingException(
                $"Duplicate event version on stream '{document.Active.StreamIdentifier}': {ex.MessageText}", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42883")
        {
            // undefined_table or undefined_function — schema not bootstrapped.
            throw new PostgresSchemaException(
                "faes schema is missing. Enable AutoCreate=CreateOrUpdate or run the V1 install script.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        await using var connection = await dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM faes_events WHERE object_name = $1 AND stream_id = $2 AND version BETWEEN $3 AND $4",
            connection);
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.ObjectName });
        cmd.Parameters.Add(new NpgsqlParameter<string> { TypedValue = document.Active.StreamIdentifier });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = fromVersion });
        cmd.Parameters.Add(new NpgsqlParameter<int> { TypedValue = toVersion });
        return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static IEvent ReadEvent(NpgsqlDataReader reader)
    {
        var version            = reader.GetInt32(0);
        var eventType          = reader.GetString(1);
        var schemaVersion      = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        var payload            = reader.GetString(3);  // jsonb auto-converts to text
        var correlationId      = reader.IsDBNull(4)  ? null : reader.GetString(4);
        var causationId        = reader.IsDBNull(5)  ? null : reader.GetString(5);
        var idempotentKey      = reader.IsDBNull(6)  ? null : reader.GetString(6);
        var originatedFromUser = reader.IsDBNull(7)  ? null : reader.GetString(7);
        var eventOccuredAt     = reader.IsDBNull(8)  ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(8);
        var metadataRaw        = reader.IsDBNull(9)  ? null : reader.GetString(9);
        var externalSeq        = reader.IsDBNull(10) ? null : reader.GetString(10);

        var actionMeta = new ActionMetadata(
            CorrelationId:      correlationId,
            CausationId:        causationId,
            IdempotentKey:      idempotentKey,
            OriginatedFromUser: ParseVersionToken(originatedFromUser),
            EventOccuredAt:     eventOccuredAt);

        var metadata = metadataRaw is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize(metadataRaw, PostgresJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();

        return new JsonEvent
        {
            EventType         = eventType,
            EventVersion      = version,
            SchemaVersion     = schemaVersion == 0 ? 1 : schemaVersion,
            Payload           = payload,
            ActionMetadata    = actionMeta,
            Metadata          = metadata,
            ExternalSequencer = externalSeq,
        };
    }

    // The wire format for OriginatedFromUser is "vt[<canonical-value>]<schemaVersion>" (see
    // VersionTokenJsonConverter). originated_from_user stores that raw string; we unwrap it
    // here rather than round-tripping through STJ to avoid an extra allocation per row.
    private static VersionToken? ParseVersionToken(string? wire)
    {
        if (string.IsNullOrEmpty(wire) || !wire.StartsWith("vt[", StringComparison.Ordinal))
        {
            return null;
        }
        var endIdx = wire.IndexOf(']');
        if (endIdx < 4)
        {
            return null;
        }
        var value = wire.Substring(3, endIdx - 3);
        var schemaVersion = wire[(endIdx + 1)..];
        return string.IsNullOrEmpty(schemaVersion)
            ? new VersionToken(value)
            : new VersionToken(value) { SchemaVersion = schemaVersion };
    }

    /// <summary>
    /// Builds a jsonb array parameter value containing the supplied events. Payload is embedded as
    /// raw JSON (no string-escaping). AOT-safe: hand-written Utf8JsonWriter, no reflection.
    /// </summary>
    private static byte[] SerializeEvents(IEvent[] events, bool preserveTimestamp)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartArray();
            foreach (var e in events)
            {
                writer.WriteStartObject();
                writer.WriteString("eventType", e.EventType ?? string.Empty);
                writer.WriteNumber("eventVersion", e.EventVersion);
                if (e.SchemaVersion > 1)
                {
                    writer.WriteNumber("schemaVersion", e.SchemaVersion);
                }
                if (!string.IsNullOrEmpty(e.ExternalSequencer))
                {
                    writer.WriteString("externalSequencer", e.ExternalSequencer);
                }

                writer.WritePropertyName("payload");
                if (string.IsNullOrEmpty(e.Payload))
                {
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                }
                else
                {
                    using var doc = JsonDocument.Parse(e.Payload);
                    doc.WriteTo(writer);
                }

                if (e.ActionMetadata is not null)
                {
                    writer.WritePropertyName("actionMetadata");
                    JsonSerializer.Serialize(writer, e.ActionMetadata, PostgresJsonContext.Default.ActionMetadata);
                }

                if (e.Metadata is { Count: > 0 })
                {
                    writer.WritePropertyName("metadata");
                    JsonSerializer.Serialize(writer, e.Metadata, PostgresJsonContext.Default.DictionaryStringString);
                }

                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return ms.ToArray();
    }
}
