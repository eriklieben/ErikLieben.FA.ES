using System.Diagnostics;
using System.Reflection;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Middleware that processes <see cref="ProjectionOutputAttribute"/> and <see cref="ProjectionOutputAttribute{T}"/>
/// attributes on functions to update projections to their latest state after successful function execution.
/// </summary>
internal class ProjectionOutputMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");

    /// <inheritdoc />
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Execute the function first
        await next(context);

        // Only process projection outputs if the function succeeded (no exception)
        // Note: If an exception was thrown, it will propagate and we won't reach here
        await ProcessProjectionOutputsAsync(context);
    }

    private static async Task ProcessProjectionOutputsAsync(FunctionContext context)
    {
        using var activity = ActivitySource.StartActivity($"ProjectionOutputMiddleware.{nameof(ProcessProjectionOutputsAsync)}");

        // Get the function's method info to read attributes
        var targetMethod = GetTargetMethod(context);
        if (targetMethod == null)
        {
            return;
        }

        // Get all ProjectionOutput attributes (both generic and non-generic)
        var outputAttributes = targetMethod.GetCustomAttributes<ProjectionOutputAttribute>(inherit: true).ToList();
        if (outputAttributes.Count == 0)
        {
            return;
        }

        var logger = context.GetLogger<ProjectionOutputMiddleware>();
        var serviceProvider = context.InstanceServices;
        var objectDocumentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

        logger.LogDebug("Processing {Count} projection output(s) for function {FunctionName}",
            outputAttributes.Count, context.FunctionDefinition.Name);

        // Process all projection outputs - if any fail, throw an aggregate exception
        var exceptions = new List<Exception>();
        var updatedProjections = new List<(Projection projection, ProjectionOutputAttribute attribute)>();

        foreach (var attribute in outputAttributes)
        {
            try
            {
                var projection = await LoadAndUpdateProjectionAsync(
                    serviceProvider,
                    objectDocumentFactory,
                    eventStreamFactory,
                    attribute,
                    logger);

                if (projection != null)
                {
                    updatedProjections.Add((projection, attribute));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update projection {ProjectionType}", attribute.ProjectionType.Name);
                exceptions.Add(new InvalidOperationException(
                    $"Failed to update projection '{attribute.ProjectionType.Name}': {ex.Message}", ex));
            }
        }

        // If any updates failed, throw before saving
        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to update {exceptions.Count} projection(s). No projections were saved.",
                exceptions);
        }

        // All updates succeeded, now save all projections
        foreach (var (projection, attribute) in updatedProjections)
        {
            if (attribute.SaveAfterUpdate)
            {
                try
                {
                    await SaveProjectionAsync(serviceProvider, projection, attribute, logger);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save projection {ProjectionType}", attribute.ProjectionType.Name);
                    exceptions.Add(new InvalidOperationException(
                        $"Failed to save projection '{attribute.ProjectionType.Name}': {ex.Message}", ex));
                }
            }
        }

        // If any saves failed, throw
        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to save {exceptions.Count} projection(s) after update.",
                exceptions);
        }

        logger.LogDebug("Successfully updated and saved {Count} projection(s)", updatedProjections.Count);
    }

    private static async Task<Projection?> LoadAndUpdateProjectionAsync(
        IServiceProvider serviceProvider,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory,
        ProjectionOutputAttribute attribute,
        ILogger logger)
    {
        using var activity = ActivitySource.StartActivity($"ProjectionOutputMiddleware.LoadAndUpdate.{attribute.ProjectionType.Name}");

        // Get the factory for this projection type
        var factoryType = typeof(IProjectionFactory<>).MakeGenericType(attribute.ProjectionType);
        var factory = serviceProvider.GetService(factoryType);

        Projection? projection = null;

        if (factory is IProjectionFactory projectionFactory)
        {
            projection = await projectionFactory.GetOrCreateProjectionAsync(
                objectDocumentFactory,
                eventStreamFactory,
                attribute.BlobName) as Projection;
        }
        else
        {
            // Fall back to looking for IProjectionFactory implementations
            var factories = serviceProvider.GetServices<IProjectionFactory>();
            var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == attribute.ProjectionType);

            if (matchingFactory != null)
            {
                projection = await matchingFactory.GetOrCreateProjectionAsync(
                    objectDocumentFactory,
                    eventStreamFactory,
                    attribute.BlobName) as Projection;
            }
        }

        if (projection == null)
        {
            throw new InvalidOperationException(
                $"No projection factory registered for type '{attribute.ProjectionType.Name}'. " +
                $"Register IProjectionFactory<{attribute.ProjectionType.Name}> or IProjectionFactory in the service collection.");
        }

        logger.LogDebug("Updating projection {ProjectionType} to latest version", attribute.ProjectionType.Name);

        // Update the projection to the latest version
        await projection.UpdateToLatestVersion();

        return projection;
    }

    private static async Task SaveProjectionAsync(
        IServiceProvider serviceProvider,
        Projection projection,
        ProjectionOutputAttribute attribute,
        ILogger logger)
    {
        using var activity = ActivitySource.StartActivity($"ProjectionOutputMiddleware.Save.{attribute.ProjectionType.Name}");

        // Get the factory for this projection type
        var factoryType = typeof(IProjectionFactory<>).MakeGenericType(attribute.ProjectionType);
        var factory = serviceProvider.GetService(factoryType);

        if (factory == null)
        {
            // Fall back to looking for IProjectionFactory implementations
            var factories = serviceProvider.GetServices<IProjectionFactory>();
            factory = factories.FirstOrDefault(f => f.ProjectionType == attribute.ProjectionType);
        }

        if (factory == null)
        {
            throw new InvalidOperationException(
                $"No projection factory found to save projection '{attribute.ProjectionType.Name}'.");
        }

        // Use reflection to call SaveAsync on the generic factory
        var saveMethod = factory.GetType().GetMethod("SaveAsync");
        if (saveMethod != null)
        {
            var task = saveMethod.Invoke(factory, [projection, attribute.BlobName, CancellationToken.None]) as Task;
            if (task != null)
            {
                await task;
            }
        }

        logger.LogDebug("Saved projection {ProjectionType}", attribute.ProjectionType.Name);
    }

    private static MethodInfo? GetTargetMethod(FunctionContext context)
    {
        // Get the entry point from the function definition
        var entryPoint = context.FunctionDefinition.EntryPoint;
        if (string.IsNullOrEmpty(entryPoint))
        {
            return null;
        }

        // Entry point format: "Namespace.ClassName.MethodName"
        var lastDotIndex = entryPoint.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            return null;
        }

        var typeName = entryPoint.Substring(0, lastDotIndex);
        var methodName = entryPoint.Substring(lastDotIndex + 1);

        // Try to find the type in all loaded assemblies
        Type? targetType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            targetType = assembly.GetType(typeName);
            if (targetType != null)
            {
                break;
            }
        }

        if (targetType == null)
        {
            return null;
        }

        // Get the method
        return targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    }
}
