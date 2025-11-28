using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Provides extension methods to configure the Event Store bindings for Azure Functions Worker.
/// </summary>
public static class FunctionsEventStoreExtensions
{
    /// <summary>
    /// Registers the Event Store and Projection input converters for Azure Functions.
    /// </summary>
    /// <remarks>
    /// This registers the converters needed for the [EventStreamInput] and [ProjectionInput] bindings to work.
    /// You should also call <c>services.ConfigureEventStore()</c> from the core ErikLieben.FA.ES package
    /// to register the core event store services.
    /// </remarks>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ConfigureEventStoreBindings(this IServiceCollection services)
    {
        // Register converters as IInputConverter for Azure Functions runtime discovery
        services.AddSingleton<IInputConverter, EventStreamConverter>();
        services.AddSingleton<IInputConverter, ProjectionConverter>();
        return services;
    }

    /// <summary>
    /// Configures the Azure Functions Worker to use the Event Store bindings, including
    /// input converters and output middleware.
    /// Call this method on <see cref="IFunctionsWorkerApplicationBuilder"/> during startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This registers:
    /// <list type="bullet">
    /// <item><description>Input converters for [EventStreamInput] and [ProjectionInput] bindings</description></item>
    /// <item><description>Middleware for [ProjectionOutput&lt;T&gt;] to update projections after function execution</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// You should also call <c>services.ConfigureEventStore()</c> from the core ErikLieben.FA.ES package
    /// to register the core event store services.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var builder = FunctionsApplication.CreateBuilder(args);
    /// builder.ConfigureEventStoreBindings();
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="builder">The Functions Worker application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsWorkerApplicationBuilder ConfigureEventStoreBindings(this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.Services.ConfigureEventStoreBindings();
        builder.UseMiddleware<ProjectionOutputMiddleware>();
        return builder;
    }
}
