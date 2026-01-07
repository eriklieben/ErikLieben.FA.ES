using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides dependency injection extensions to register CosmosDB-backed Event Store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string CosmosDbServiceKey = "cosmosdb";
    private static bool _cosmosExceptionExtractorRegistered;

    /// <summary>
    /// Registers the CosmosDB-based implementations for document tags, documents, and event streams using the provided settings.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="settings">The CosmosDB settings controlling containers, throughput, and defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance to allow chaining.</returns>
    public static IServiceCollection ConfigureCosmosDbEventStore(this IServiceCollection services, EventStreamCosmosDbSettings settings)
    {
        services.AddSingleton(settings);
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, CosmosDbTagFactory>(CosmosDbServiceKey);
        services.AddKeyedSingleton<IObjectDocumentFactory, CosmosDbObjectDocumentFactory>(CosmosDbServiceKey);
        services.AddKeyedSingleton<IEventStreamFactory, CosmosDbEventStreamFactory>(CosmosDbServiceKey);
        services.AddKeyedSingleton<IObjectIdProvider, CosmosDbObjectIdProvider>(CosmosDbServiceKey);

        // Register the CosmosException status code extractor for ResilientDataStore
        RegisterCosmosExceptionExtractor();

        return services;
    }

    /// <summary>
    /// Registers the CosmosException status code extractor with <see cref="ResilientDataStore"/>.
    /// This enables proper retry handling for CosmosDB-specific transient errors (429 throttling, etc.).
    /// </summary>
    /// <remarks>
    /// This method is automatically called by <see cref="ConfigureCosmosDbEventStore"/> but can be called
    /// explicitly if you're configuring CosmosDB services manually without that extension method.
    /// </remarks>
    public static void RegisterCosmosExceptionExtractor()
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
