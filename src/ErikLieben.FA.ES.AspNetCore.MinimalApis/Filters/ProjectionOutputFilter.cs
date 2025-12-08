using System.Diagnostics;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Filters;

/// <summary>
/// Shared resources for projection output filters.
/// </summary>
internal static class ProjectionOutputFilterResources
{
    /// <summary>
    /// The activity source for all projection output filter operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AspNetCore.MinimalApis");
}

/// <summary>
/// Endpoint filter that updates and saves a projection after successful endpoint execution.
/// </summary>
/// <typeparam name="TProjection">The projection type to update.</typeparam>
/// <remarks>
/// <para>
/// This filter is applied to Minimal API endpoints to automatically update projections
/// after the endpoint handler completes successfully. The projection is loaded, updated
/// to the latest version by folding new events, and then saved back to storage.
/// </para>
/// <para>
/// Usage:
/// <code>
/// app.MapPost("/orders/{id}/items", CreateOrderItem)
///     .WithProjectionOutput&lt;OrderSummaryProjection&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class ProjectionOutputFilter<TProjection> : IEndpointFilter
    where TProjection : Projection
{

    private readonly string? _blobNamePattern;
    private readonly bool _saveAfterUpdate;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionOutputFilter{TProjection}"/> class.
    /// </summary>
    /// <param name="blobNamePattern">
    /// Optional blob name pattern for routed projections. Supports route parameter substitution.
    /// </param>
    /// <param name="saveAfterUpdate">
    /// Whether to save the projection after updating. Defaults to <c>true</c>.
    /// </param>
    public ProjectionOutputFilter(string? blobNamePattern = null, bool saveAfterUpdate = true)
    {
        _blobNamePattern = blobNamePattern;
        _saveAfterUpdate = saveAfterUpdate;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Execute the endpoint first
        var result = await next(context);

        // Only process projection output if the endpoint succeeded (no exception thrown)
        await UpdateProjectionAsync(context.HttpContext);

        return result;
    }

    private async Task UpdateProjectionAsync(HttpContext httpContext)
    {
        using var activity = ProjectionOutputFilterResources.ActivitySource.StartActivity($"ProjectionOutputFilter<{typeof(TProjection).Name}>.UpdateProjection");

        var serviceProvider = httpContext.RequestServices;
        var logger = serviceProvider.GetService<ILogger<ProjectionOutputFilter<TProjection>>>();
        var documentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var streamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

        // Resolve blob name from route parameters if pattern is provided
        var blobName = RouteValueResolver.SubstituteRouteValues(
            _blobNamePattern,
            httpContext.Request.RouteValues);

        activity?.SetTag("projectionType", typeof(TProjection).Name);
        activity?.SetTag("blobName", blobName ?? "(default)");

        logger?.LogDebug("Updating projection {ProjectionType} with blob name {BlobName}",
            typeof(TProjection).Name, blobName ?? "(default)");

        // Load the projection
        var projection = await LoadProjectionAsync(serviceProvider, documentFactory, streamFactory, blobName);

        // Update to latest version
        await projection.UpdateToLatestVersion();

        // Save if configured to do so
        if (_saveAfterUpdate)
        {
            await SaveProjectionAsync(serviceProvider, projection, blobName, logger);
        }

        logger?.LogDebug("Successfully updated projection {ProjectionType}", typeof(TProjection).Name);
    }

    private static async Task<TProjection> LoadProjectionAsync(
        IServiceProvider serviceProvider,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory streamFactory,
        string? blobName)
    {
        // Try generic factory first
        var genericFactory = serviceProvider.GetService<IProjectionFactory<TProjection>>();
        if (genericFactory != null)
        {
            return await genericFactory.GetOrCreateAsync(documentFactory, streamFactory, blobName);
        }

        // Fall back to non-generic factory
        var factories = serviceProvider.GetServices<IProjectionFactory>();
        var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == typeof(TProjection));

        if (matchingFactory != null)
        {
            var projection = await matchingFactory.GetOrCreateProjectionAsync(
                documentFactory,
                streamFactory,
                blobName);

            return (TProjection)projection;
        }

        throw new InvalidOperationException(
            $"No projection factory registered for type '{typeof(TProjection).Name}'. " +
            $"Register IProjectionFactory<{typeof(TProjection).Name}> or IProjectionFactory in the service collection.");
    }

    private static async Task SaveProjectionAsync(
        IServiceProvider serviceProvider,
        TProjection projection,
        string? blobName,
        ILogger? logger)
    {
        using var activity = ProjectionOutputFilterResources.ActivitySource.StartActivity($"ProjectionOutputFilter<{typeof(TProjection).Name}>.Save");

        // Try generic factory first
        var genericFactory = serviceProvider.GetService<IProjectionFactory<TProjection>>();
        if (genericFactory != null)
        {
            await genericFactory.SaveAsync(projection, blobName);
            logger?.LogDebug("Saved projection {ProjectionType} using generic factory", typeof(TProjection).Name);
            return;
        }

        // Fall back to non-generic factory
        var factories = serviceProvider.GetServices<IProjectionFactory>();
        var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == typeof(TProjection));

        if (matchingFactory != null)
        {
            await matchingFactory.SaveProjectionAsync(projection, blobName);
            logger?.LogDebug("Saved projection {ProjectionType} using non-generic factory", typeof(TProjection).Name);
            return;
        }

        throw new InvalidOperationException(
            $"No projection factory found to save projection '{typeof(TProjection).Name}'.");
    }
}
