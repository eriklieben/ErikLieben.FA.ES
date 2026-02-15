using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Builder;

/// <summary>
/// Default implementation of <see cref="IFaesBuilder"/> for configuring FAES event sourcing services.
/// </summary>
public sealed class FaesBuilder : IFaesBuilder
{
    private string _defaultStorageType = "blob";

    /// <summary>
    /// Initializes a new instance of the <see cref="FaesBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public FaesBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <inheritdoc />
    public IFaesBuilder UseDefaultStorage(string storageType)
    {
        ArgumentNullException.ThrowIfNull(storageType);
        _defaultStorageType = storageType;
        return this;
    }

    /// <inheritdoc />
    public IFaesBuilder UseDefaultStorage(string storageType, string documentType)
    {
        // For backwards compatibility - both parameters set the same default
        ArgumentNullException.ThrowIfNull(storageType);
        ArgumentNullException.ThrowIfNull(documentType);
        _defaultStorageType = storageType;
        return this;
    }

    /// <summary>
    /// Builds and registers all configured services.
    /// Called internally after the configuration action completes.
    /// </summary>
    internal void Build()
    {
        // Register core services with the configured defaults
        // Use the single-parameter constructor which sets all types to the same value
        var settings = new EventStreamDefaultTypeSettings(_defaultStorageType);
        Services.AddSingleton(settings);
        Services.AddSingleton<IObjectDocumentFactory, ObjectDocumentFactory>();
        RegisterKeyedDictionary<string, IObjectDocumentFactory>();
        Services.AddSingleton<IDocumentTagDocumentFactory, DocumentTagDocumentFactory>();
        RegisterKeyedDictionary<string, IDocumentTagDocumentFactory>();
        Services.AddSingleton<IEventStreamFactory, EventStreamFactory>();
        RegisterKeyedDictionary<string, IEventStreamFactory>();
        Services.AddSingleton<IObjectIdProvider, ObjectIdProvider>();
        RegisterKeyedDictionary<string, IObjectIdProvider>();
    }

    /// <summary>
    /// Registers a keyed dictionary mapping service keys to resolved services.
    /// </summary>
    private void RegisterKeyedDictionary<TKey, T>() where TKey : notnull where T : notnull
    {
        var keys = Services
            .Where(sd => sd.IsKeyedService && sd.ServiceType == typeof(T) && sd.ServiceKey is TKey)
            .Select(sd => (TKey)sd.ServiceKey!)
            .Distinct()
            .ToList();

        Services.AddTransient<IDictionary<TKey, T>>(p => keys
            .ToDictionary(k => k, k => p.GetRequiredKeyedService<T>(k)));
    }
}
