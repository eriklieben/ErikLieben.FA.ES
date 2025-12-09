using System.Reflection;
using ErikLieben.FA.ES.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Middleware that processes <see cref="ProjectionOutputAttribute"/> and <see cref="ProjectionOutputAttribute{T}"/>
/// attributes on functions to update projections to their latest state after successful function execution.
/// </summary>
internal class ProjectionOutputMiddleware : IFunctionsWorkerMiddleware
{
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
        // Get the function's method info to read attributes
        var entryPoint = context.FunctionDefinition.EntryPoint;
        var targetMethod = ProjectionOutputProcessor.ResolveTargetMethod(entryPoint);

        var outputAttributes = ProjectionOutputProcessor.GetProjectionOutputAttributes(targetMethod);
        if (outputAttributes.Count == 0)
        {
            return;
        }

        var logger = context.GetLogger<ProjectionOutputMiddleware>();
        var serviceProvider = context.InstanceServices;
        var objectDocumentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

        var processor = new ProjectionOutputProcessor(
            serviceProvider,
            objectDocumentFactory,
            eventStreamFactory,
            logger);

        await processor.ProcessProjectionOutputsAsync(
            outputAttributes,
            context.FunctionDefinition.Name);
    }
}
