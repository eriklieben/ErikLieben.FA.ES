using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure event store Minimal API bindings.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds event store Minimal API services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method configures the services required for event stream and projection
    /// parameter binding in Minimal API endpoints. It should be called after
    /// configuring the core event store services.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// // Add core event store services first
    /// builder.Services.ConfigureEventStore();
    ///
    /// // Add Minimal API bindings
    /// builder.Services.AddEventStoreMinimalApis();
    ///
    /// var app = builder.Build();
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddEventStoreMinimalApis(this IServiceCollection services)
    {
        // Currently no additional services are required beyond what's provided by the core library.
        // This method is provided for future extensibility and consistency with the Azure Functions
        // extensions pattern (ConfigureEventStoreBindings).
        //
        // Users should call ConfigureEventStore() to register core services like:
        // - IAggregateFactory
        // - IObjectDocumentFactory
        // - IEventStreamFactory
        // - IProjectionFactory<T> implementations

        return services;
    }
}
