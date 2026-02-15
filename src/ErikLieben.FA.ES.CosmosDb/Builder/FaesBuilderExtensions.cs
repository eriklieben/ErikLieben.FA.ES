using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.CosmosDb.HealthChecks;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.CosmosDb.Builder;

/// <summary>
/// Extension methods for configuring CosmosDB provider with the FAES builder.
/// </summary>
public static class FaesBuilderExtensions
{
    private const string CosmosDbServiceKey = "cosmosdb";
    private static bool _cosmosExceptionExtractorRegistered;

    /// <summary>
    /// Configures Azure CosmosDB as an event store provider.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="settings">The CosmosDB settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseCosmosDb(this IFaesBuilder builder, EventStreamCosmosDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        builder.Services.AddSingleton(settings);
        builder.Services.AddKeyedSingleton<IDocumentTagDocumentFactory, CosmosDbTagFactory>(CosmosDbServiceKey);
        builder.Services.AddKeyedSingleton<IObjectDocumentFactory, CosmosDbObjectDocumentFactory>(CosmosDbServiceKey);
        builder.Services.AddKeyedSingleton<IEventStreamFactory, CosmosDbEventStreamFactory>(CosmosDbServiceKey);
        builder.Services.AddKeyedSingleton<IObjectIdProvider, CosmosDbObjectIdProvider>(CosmosDbServiceKey);

        RegisterCosmosExceptionExtractor();

        return builder;
    }

    /// <summary>
    /// Configures Azure CosmosDB with a configuration action.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="configure">Action to configure CosmosDB settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseCosmosDb(this IFaesBuilder builder, Action<EventStreamCosmosDbSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new EventStreamCosmosDbSettings();
        configure(settings);
        return builder.UseCosmosDb(settings);
    }

    /// <summary>
    /// Adds a health check for Azure CosmosDB.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithCosmosDbHealthCheck(this IFaesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddCosmosDbHealthCheck(tags: ["faes", "storage", "cosmosdb"]);

        return builder;
    }

    /// <summary>
    /// Registers the CosmosDB-backed projection status coordinator.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="databaseName">The database name for projection status documents.</param>
    /// <param name="containerName">The container name for projection status documents. Default: "projection-status".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithCosmosDbProjectionStatusCoordinator(
        this IFaesBuilder builder,
        string databaseName,
        string containerName = "projection-status")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(databaseName);

        builder.Services.AddSingleton<Projections.IProjectionStatusCoordinator>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var logger = sp.GetService<ILogger<CosmosDbProjectionStatusCoordinator>>();
            return new CosmosDbProjectionStatusCoordinator(cosmosClient, databaseName, containerName, logger);
        });

        return builder;
    }

    private static void RegisterCosmosExceptionExtractor()
    {
        if (_cosmosExceptionExtractorRegistered)
        {
            return;
        }

        ResilientDataStore.RegisterStatusCodeExtractor(exception =>
        {
            if (exception is CosmosException cosmosEx)
            {
                return (int)cosmosEx.StatusCode;
            }
            return null;
        });

        _cosmosExceptionExtractorRegistered = true;
    }
}
