using ErikLieben.FA.ES.CosmosDb.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Provides dependency injection extensions to register CosmosDB-backed Event Store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string CosmosDbServiceKey = "cosmosdb";

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
        return services;
    }
}
