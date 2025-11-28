using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides extension methods to configure the Event Store dependencies for dependency injection.
/// </summary>
public static class EventStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Event Store services and default settings for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="settings">The default event stream type settings used to resolve factories.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance to support fluent configuration.</returns>
    public static IServiceCollection ConfigureEventStore(this IServiceCollection services, EventStreamDefaultTypeSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IObjectDocumentFactory, ObjectDocumentFactory>();
        services.RegisterKeyedDictionary<string, IObjectDocumentFactory>();
        services.AddSingleton<IDocumentTagDocumentFactory, DocumentTagDocumentFactory>();
        services.RegisterKeyedDictionary<string, IDocumentTagDocumentFactory>();
        services.AddSingleton<IEventStreamFactory, EventStreamFactory>();
        services.RegisterKeyedDictionary<string, IEventStreamFactory>();
        services.AddSingleton<IObjectIdProvider, ObjectIdProvider>();
        services.RegisterKeyedDictionary<string, IObjectIdProvider>();

        return services;
    }

    /// <summary>
    /// Registers a keyed dictionary mapping service keys to resolved services of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the service key.</typeparam>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <param name="serviceCollection">The service collection to read existing keyed registrations from.</param>
    private static void RegisterKeyedDictionary<TKey, T>(this IServiceCollection serviceCollection) where TKey : notnull where T : notnull
    {
        var keys = serviceCollection
            .Where(sd => sd.IsKeyedService && sd.ServiceType == typeof(T) && sd.ServiceKey is TKey)
            .Select(sd => (TKey)sd.ServiceKey!)
            .Distinct()
            .ToList();

        serviceCollection.AddTransient<IDictionary<TKey, T>>(p => keys
            .ToDictionary(k => k, k => p.GetRequiredKeyedService<T>(k)));
    }
}
