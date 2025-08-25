using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

public static class FunctionsEventStoreExtensions
{
    public static IServiceCollection ConfigureEventStore(this IServiceCollection services, EventStreamDefaultTypeSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IObjectDocumentFactory, ObjectDocumentFactory>();
        services.RegisterKeyedDictionary<string, IObjectDocumentFactory>();
        services.AddSingleton<IDocumentTagDocumentFactory, DocumentTagDocumentFactory>();
        services.RegisterKeyedDictionary<string, IDocumentTagDocumentFactory>();
        services.AddSingleton<IEventStreamFactory, EventStreamFactory>();
        services.RegisterKeyedDictionary<string, IEventStreamFactory>();
        
        return services;
    }

    private static void RegisterKeyedDictionary<TKey, T>(this IServiceCollection serviceCollection)
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        var keys = serviceCollection
            .Where(sd => sd.IsKeyedService && sd.ServiceType == typeof(T))
            .Select(d => d.ServiceKey)
            .Distinct()
            .Select(k => (TKey)k)
            .ToList();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.


#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
        serviceCollection.AddTransient<IDictionary<TKey, T>>(p => keys
            .ToDictionary(k => k, k => p.GetKeyedService<T>(k)));
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
}