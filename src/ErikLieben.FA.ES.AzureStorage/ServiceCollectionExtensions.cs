using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AzureStorage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureBlobEventStore(this IServiceCollection services, EventStreamBlobSettings settings)
    {
        services.AddSingleton(settings);
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, BlobTagFactory>("blob");
        services.AddKeyedSingleton<IObjectDocumentFactory, BlobObjectDocumentFactory>("blob");
        services.AddKeyedSingleton<IEventStreamFactory, BlobEventStreamFactory>("blob");
        return services;
    }
}
