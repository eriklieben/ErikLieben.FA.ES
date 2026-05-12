using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Postgres.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ErikLieben.FA.ES.Postgres;

/// <summary>
/// DI registration for the Postgres event store provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The service key under which Postgres-backed implementations are registered.</summary>
    public const string PostgresServiceKey = "postgres";

    private static bool _postgresExceptionExtractorRegistered;

    /// <summary>
    /// Registers Postgres-backed event store services using the provided settings. Registers
    /// <see cref="NpgsqlDataSource"/> as a singleton (multiplexing + auto-prepare enabled) and
    /// keyed implementations of the core event store contracts under <see cref="PostgresServiceKey"/>.
    /// </summary>
    public static IServiceCollection ConfigureNpgsqlEventStore(this IServiceCollection services, EventStreamPostgresSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new ArgumentException("EventStreamPostgresSettings.ConnectionString is required.", nameof(settings));
        }

        services.AddSingleton(settings);

        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
            builder.ConnectionStringBuilder.Multiplexing = true;
            builder.ConnectionStringBuilder.MaxAutoPrepare = 32;
            return builder.Build();
        });

        services.AddSingleton<PostgresSchemaBootstrapper>();
        services.AddSingleton<PostgresDataStore>();
        services.AddSingleton<IPostgresDocumentStore, PostgresDocumentStore>();

        // Keyed registrations the composite ObjectDocumentFactory / DocumentTagDocumentFactory route to.
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, PostgresTagFactory>(PostgresServiceKey);
        services.AddKeyedSingleton<IObjectDocumentFactory, PostgresObjectDocumentFactory>(PostgresServiceKey);
        services.AddKeyedSingleton<IEventStreamFactory, PostgresEventStreamFactory>(PostgresServiceKey);
        services.AddKeyedSingleton<IObjectIdProvider, PostgresObjectIdProvider>(PostgresServiceKey);

        // Snapshot store is consumed directly by PostgresEventStreamFactory via DI.
        services.AddSingleton<ISnapShotStore, PostgresSnapShotStore>();

        services.AddSingleton<HealthChecks.PostgresHealthCheck>();

        RegisterPostgresExceptionExtractor();

        return services;
    }

    /// <summary>
    /// Registers an additional named <see cref="NpgsqlDataSource"/> for projection storage.
    /// Resolved by the codegen-emitted projection factory when a projection is annotated
    /// with <c>[PostgresJsonbProjection(Connection = "name")]</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionName">The key under which the data source is registered.</param>
    /// <param name="connectionString">The Npgsql connection string.</param>
    public static IServiceCollection AddNamedNpgsqlDataSource(
        this IServiceCollection services,
        string connectionName,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddKeyedSingleton<NpgsqlDataSource>(connectionName, (_, _) =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.ConnectionStringBuilder.Multiplexing = true;
            builder.ConnectionStringBuilder.MaxAutoPrepare = 32;
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Registers a status-code extractor for <see cref="ResilientDataStore"/> so that PostgresException
    /// SQL states map to HTTP-style codes used by the retry policy.
    /// </summary>
    public static void RegisterPostgresExceptionExtractor()
    {
        if (_postgresExceptionExtractorRegistered)
        {
            return;
        }

        ResilientDataStore.RegisterStatusCodeExtractor(exception =>
        {
            if (exception is PostgresException pg)
            {
                return pg.SqlState switch
                {
                    "40001" => 409,
                    "23505" => 409,
                    "40P01" => 409,
                    "57014" => 408,
                    "53300" => 503,
                    "08006" => 503,
                    "08003" => 503,
                    "08001" => 503,
                    _       => null,
                };
            }
            return null;
        });

        _postgresExceptionExtractorRegistered = true;
    }
}
