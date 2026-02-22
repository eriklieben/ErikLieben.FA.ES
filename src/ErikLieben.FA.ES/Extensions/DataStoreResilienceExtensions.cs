using System.Diagnostics.CodeAnalysis;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Polly;

namespace ErikLieben.FA.ES.Extensions;

/// <summary>
/// Extension methods for configuring resilient data stores in dependency injection.
/// </summary>
public static class DataStoreResilienceExtensions
{
    /// <summary>
    /// Decorates an existing <see cref="IDataStore"/> registration with resilience (retry) behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure the resilience options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method wraps the existing IDataStore registration with a <see cref="ResilientDataStore"/>
    /// that automatically retries transient failures.
    /// </para>
    /// <para>
    /// If no IDataStore is registered, this method has no effect.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddDataStoreResilience(
        this IServiceCollection services,
        Action<DataStoreResilienceOptions>? configure = null)
    {
        var options = new DataStoreResilienceOptions();
        configure?.Invoke(options);

        // Find existing IDataStore registration
        var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataStore));
        if (existingDescriptor == null)
        {
            // No IDataStore registered, nothing to wrap
            return services;
        }

        // Remove the existing registration
        services.Remove(existingDescriptor);

        // Create the pipeline once
        var pipeline = ResilientDataStore.CreateDefaultPipeline(options);

        // Re-register as the inner implementation with a unique key
        if (existingDescriptor.ImplementationType != null)
        {
            services.Add(new ServiceDescriptor(
                existingDescriptor.ImplementationType,
                existingDescriptor.ImplementationType,
                existingDescriptor.Lifetime));

            // Register the resilient wrapper
            services.Add(new ServiceDescriptor(
                typeof(IDataStore),
                sp =>
                {
                    var inner = (IDataStore)sp.GetRequiredService(existingDescriptor.ImplementationType);
                    var logger = sp.GetService<ILogger<ResilientDataStore>>();
                    return new ResilientDataStore(inner, pipeline, logger);
                },
                existingDescriptor.Lifetime));
        }
        else if (existingDescriptor.ImplementationFactory != null)
        {
            // Factory-based registration
            services.Add(new ServiceDescriptor(
                typeof(IDataStore),
                sp =>
                {
                    var inner = (IDataStore)existingDescriptor.ImplementationFactory(sp);
                    var logger = sp.GetService<ILogger<ResilientDataStore>>();
                    return new ResilientDataStore(inner, pipeline, logger);
                },
                existingDescriptor.Lifetime));
        }
        else if (existingDescriptor.ImplementationInstance != null)
        {
            // Instance-based registration (singleton)
            var inner = (IDataStore)existingDescriptor.ImplementationInstance;
            services.AddSingleton<IDataStore>(sp =>
            {
                var logger = sp.GetService<ILogger<ResilientDataStore>>();
                return new ResilientDataStore(inner, pipeline, logger);
            });
        }

        return services;
    }

    /// <summary>
    /// Adds a resilient data store of the specified type with retry behavior.
    /// </summary>
    /// <typeparam name="TDataStore">The concrete data store type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure the resilience options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilientDataStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDataStore>(
        this IServiceCollection services,
        Action<DataStoreResilienceOptions>? configure = null)
        where TDataStore : class, IDataStore
    {
        var options = new DataStoreResilienceOptions();
        configure?.Invoke(options);

        var pipeline = ResilientDataStore.CreateDefaultPipeline(options);

        // Register the concrete type
        services.TryAddSingleton<TDataStore>();

        // Register the resilient wrapper as IDataStore
        services.AddSingleton<IDataStore>(sp =>
        {
            var inner = sp.GetRequiredService<TDataStore>();
            var logger = sp.GetService<ILogger<ResilientDataStore>>();
            return new ResilientDataStore(inner, pipeline, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a resilient data store with a custom resilience pipeline.
    /// </summary>
    /// <typeparam name="TDataStore">The concrete data store type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="pipelineFactory">A factory function that creates the resilience pipeline.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilientDataStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDataStore>(
        this IServiceCollection services,
        Func<IServiceProvider, ResiliencePipeline> pipelineFactory)
        where TDataStore : class, IDataStore
    {
        ArgumentNullException.ThrowIfNull(pipelineFactory);

        // Register the concrete type
        services.TryAddSingleton<TDataStore>();

        // Register the resilient wrapper as IDataStore
        services.AddSingleton<IDataStore>(sp =>
        {
            var inner = sp.GetRequiredService<TDataStore>();
            var pipeline = pipelineFactory(sp);
            var logger = sp.GetService<ILogger<ResilientDataStore>>();
            return new ResilientDataStore(inner, pipeline, logger);
        });

        return services;
    }
}
