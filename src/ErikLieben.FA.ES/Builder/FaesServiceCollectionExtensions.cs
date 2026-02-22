using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Builder;

/// <summary>
/// Extension methods for configuring FAES event sourcing services using a fluent builder.
/// </summary>
public static class FaesServiceCollectionExtensions
{
    /// <summary>
    /// Adds FAES event sourcing services to the service collection using a fluent configuration builder.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure the FAES builder.</param>
    /// <returns>The service collection for further chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFaes(faes => faes
    ///     .UseDefaultStorage("blob")
    ///     .UseBlobStorage(settings)
    ///     .WithHealthChecks()
    ///     .WithOpenTelemetry());
    /// </code>
    /// </example>
    public static IServiceCollection AddFaes(
        this IServiceCollection services,
        Action<IFaesBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new FaesBuilder(services);
        configure(builder);
        builder.Build();

        return services;
    }

    /// <summary>
    /// Adds FAES event sourcing services with default configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for further chaining.</returns>
    /// <remarks>
    /// This registers core services with "blob" as the default storage type.
    /// You must still register a storage provider (Blob, Table, or CosmosDB).
    /// </remarks>
    public static IServiceCollection AddFaes(this IServiceCollection services)
    {
        return services.AddFaes(_ => { });
    }
}
