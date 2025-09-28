﻿using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AzureStorage;

/// <summary>
/// Provides dependency injection extensions to register Azure Blob Storage-backed Event Store services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Blob-based implementations for document tags, documents, and event streams using the provided settings.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="settings">The Blob settings controlling containers, chunking, and defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance to allow chaining.</returns>
    public static IServiceCollection ConfigureBlobEventStore(this IServiceCollection services, EventStreamBlobSettings settings)
    {
        services.AddSingleton(settings);
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, BlobTagFactory>("blob");
        services.AddKeyedSingleton<IObjectDocumentFactory, BlobObjectDocumentFactory>("blob");
        services.AddKeyedSingleton<IEventStreamFactory, BlobEventStreamFactory>("blob");
        return services;
    }
}
