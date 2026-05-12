using System.Reflection;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// Applies the V1 schema script to the target database, guarded by a per-database advisory lock
/// so multiple processes can race the bootstrap safely.
/// </summary>
public sealed class PostgresSchemaBootstrapper
{
    // Stable arbitrary key for the advisory lock. Anything constant works; chosen randomly.
    private const long BootstrapAdvisoryKey = 0x46414553_53434D31L; // 'FAES_SCM1'

    private readonly NpgsqlDataSource dataSource;
    private readonly EventStreamPostgresSettings settings;

    /// <summary>Initializes a new instance.</summary>
    public PostgresSchemaBootstrapper(NpgsqlDataSource dataSource, EventStreamPostgresSettings settings)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(settings);
        this.dataSource = dataSource;
        this.settings = settings;
    }

    /// <summary>
    /// Ensures the schema is present. No-op when <see cref="EventStreamPostgresSettings.AutoCreate"/> is <see cref="SchemaBootstrapMode.None"/>.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (settings.AutoCreate == SchemaBootstrapMode.None)
        {
            return;
        }

        var sql = LoadEmbeddedScript("Schema.V1__Install.sql");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock($1)", connection))
        {
            lockCmd.Parameters.Add(new NpgsqlParameter<long> { TypedValue = BootstrapAdvisoryKey });
            await lockCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw new PostgresSchemaException($"Failed to bootstrap schema: {ex.MessageText}", ex);
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock($1)", connection);
            unlockCmd.Parameters.Add(new NpgsqlParameter<long> { TypedValue = BootstrapAdvisoryKey });
            await unlockCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a dedicated LIST partition for the given object_name. Idempotent.
    /// Only call when <see cref="EventStreamPostgresSettings.AutoCreatePartitionsPerObjectName"/> is enabled.
    /// </summary>
    public async Task EnsurePartitionForObjectNameAsync(string objectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        if (!settings.AutoCreatePartitionsPerObjectName)
        {
            return;
        }

        var safe = SanitizeIdentifier(objectName);
        var partitionTable = $"faes_events__{safe}";

        // Quote the literal we substitute into FOR VALUES IN. Use Postgres dollar-quoting via parameter is not possible
        // for DDL identifiers, so we sanitize and quote inline. objectName itself is the *literal* value, which we
        // pass through Postgres's standard literal escaping.
        var literal = objectName.Replace("'", "''");
        var ddl = $"CREATE TABLE IF NOT EXISTS \"{partitionTable}\" PARTITION OF faes_events FOR VALUES IN ('{literal}')";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(ddl, connection);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            // duplicate_table — already exists, ignore.
        }
    }

    private static string LoadEmbeddedScript(string resourceLeafName)
    {
        var asm = typeof(PostgresSchemaBootstrapper).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceLeafName, StringComparison.Ordinal))
            ?? throw new PostgresSchemaException(
                $"Embedded schema script '{resourceLeafName}' was not found in assembly {asm.GetName().Name}.");

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new PostgresSchemaException($"Unable to open embedded schema script '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string SanitizeIdentifier(string input)
    {
        Span<char> buf = stackalloc char[input.Length];
        var i = 0;
        foreach (var c in input)
        {
            buf[i++] = char.IsLetterOrDigit(c) || c == '_' ? char.ToLowerInvariant(c) : '_';
        }
        return new string(buf[..i]);
    }
}
