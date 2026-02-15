using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Builder;

/// <summary>
/// Builder interface for configuring FAES (ErikLieben.FA.ES) event sourcing services.
/// </summary>
/// <remarks>
/// Use this builder through the <see cref="FaesServiceCollectionExtensions.AddFaes"/> extension method
/// to configure event sourcing with a fluent API.
/// </remarks>
public interface IFaesBuilder
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Sets the default storage provider type used when no specific type is specified on an aggregate.
    /// </summary>
    /// <param name="storageType">The storage type key (e.g., "blob", "table", "cosmosdb").</param>
    /// <returns>The builder instance for chaining.</returns>
    IFaesBuilder UseDefaultStorage(string storageType);

    /// <summary>
    /// Sets the default storage provider type with additional configuration.
    /// </summary>
    /// <param name="storageType">The storage type key.</param>
    /// <param name="documentType">The document type key.</param>
    /// <returns>The builder instance for chaining.</returns>
    IFaesBuilder UseDefaultStorage(string storageType, string documentType);
}
