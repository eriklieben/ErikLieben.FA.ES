using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Table;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AzureStorage;

/// <summary>
/// Provides dependency injection extensions to register Azure Storage-backed Event Store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string BlobServiceKey = "blob";
    private const string TableServiceKey = "table";

    /// <summary>
    /// Registers the Blob-based implementations for document tags, documents, and event streams using the provided settings.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="settings">The Blob settings controlling containers, chunking, and defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance to allow chaining.</returns>
    public static IServiceCollection ConfigureBlobEventStore(this IServiceCollection services, EventStreamBlobSettings settings)
    {
        services.AddSingleton(settings);
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, BlobTagFactory>(BlobServiceKey);
        services.AddKeyedSingleton<IObjectDocumentFactory, BlobObjectDocumentFactory>(BlobServiceKey);
        services.AddKeyedSingleton<IEventStreamFactory, BlobEventStreamFactory>(BlobServiceKey);
        services.AddKeyedSingleton<IObjectIdProvider, BlobObjectIdProvider>(BlobServiceKey);
        return services;
    }

    /// <summary>
    /// Registers the Table-based implementations for document tags, documents, and event streams using the provided settings.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="settings">The Table settings controlling tables, chunking, and defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance to allow chaining.</returns>
    public static IServiceCollection ConfigureTableEventStore(this IServiceCollection services, EventStreamTableSettings settings)
    {
        services.AddSingleton(settings);
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, TableTagFactory>(TableServiceKey);
        services.AddKeyedSingleton<IObjectDocumentFactory, TableObjectDocumentFactory>(TableServiceKey);
        services.AddKeyedSingleton<IEventStreamFactory, TableEventStreamFactory>(TableServiceKey);
        services.AddKeyedSingleton<IObjectIdProvider, TableObjectIdProvider>(TableServiceKey);
        return services;
    }
}
