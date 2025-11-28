using System.Diagnostics;
using System.Reflection;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;

/// <summary>
/// Provides parameter binding functionality for projections in Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This binder loads projections from storage by:
/// <list type="number">
/// <item>Resolving the blob name (optionally from route parameters)</item>
/// <item>Finding a projection factory for the requested type</item>
/// <item>Loading or creating the projection from storage</item>
/// </list>
/// </para>
/// </remarks>
public static class ProjectionBinder
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AspNetCore.MinimalApis");

    /// <summary>
    /// Binds a projection from an HTTP context for a Minimal API endpoint.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to bind.</typeparam>
    /// <param name="context">The HTTP context containing route values and services.</param>
    /// <param name="parameter">The parameter info for the projection parameter.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> containing the bound projection,
    /// or <c>null</c> if binding fails.
    /// </returns>
    /// <exception cref="ProjectionNotFoundException">
    /// Thrown when the projection doesn't exist and <see cref="ProjectionAttribute.CreateIfNotExists"/> is <c>false</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no projection factory is registered for the requested type.
    /// </exception>
    public static async ValueTask<TProjection?> BindAsync<TProjection>(
        HttpContext context,
        ParameterInfo parameter)
        where TProjection : Projection
    {
        using var activity = ActivitySource.StartActivity($"ProjectionBinder.BindAsync<{typeof(TProjection).Name}>");

        var attribute = parameter.GetCustomAttribute<ProjectionAttribute>();

        // Resolve blob name from route parameters if pattern is provided
        var blobName = RouteValueResolver.SubstituteRouteValues(
            attribute?.BlobNamePattern,
            context.Request.RouteValues);

        activity?.SetTag("blobName", blobName ?? "(default)");
        activity?.SetTag("createIfNotExists", attribute?.CreateIfNotExists ?? true);

        return await BindCoreAsync<TProjection>(
            context.RequestServices,
            blobName,
            attribute?.CreateIfNotExists ?? true);
    }

    /// <summary>
    /// Core binding logic that loads a projection from storage.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to load.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="blobName">Optional blob name for routed projections.</param>
    /// <param name="createIfNotExists">Whether to create the projection if it doesn't exist.</param>
    /// <returns>The loaded projection instance.</returns>
    internal static async Task<TProjection> BindCoreAsync<TProjection>(
        IServiceProvider serviceProvider,
        string? blobName,
        bool createIfNotExists)
        where TProjection : Projection
    {
        using var activity = ActivitySource.StartActivity($"ProjectionBinder.BindCoreAsync<{typeof(TProjection).Name}>");

        var documentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var streamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

        // Try generic factory first: IProjectionFactory<TProjection>
        var genericFactory = serviceProvider.GetService<IProjectionFactory<TProjection>>();
        if (genericFactory != null)
        {
            activity?.SetTag("factoryType", "generic");

            if (!createIfNotExists)
            {
                var exists = await genericFactory.ExistsAsync(blobName);
                if (!exists)
                {
                    throw new ProjectionNotFoundException(typeof(TProjection), blobName);
                }
            }

            return await genericFactory.GetOrCreateAsync(documentFactory, streamFactory, blobName);
        }

        // Fall back to non-generic factory resolution
        var projection = await ResolveFromNonGenericFactoryAsync<TProjection>(
            serviceProvider,
            documentFactory,
            streamFactory,
            blobName,
            createIfNotExists);

        if (projection != null)
        {
            activity?.SetTag("factoryType", "non-generic");
            return projection;
        }

        throw new InvalidOperationException(
            $"No projection factory registered for type '{typeof(TProjection).Name}'. " +
            $"Register IProjectionFactory<{typeof(TProjection).Name}> or IProjectionFactory in the service collection.");
    }

    /// <summary>
    /// Attempts to resolve a projection using non-generic <see cref="IProjectionFactory"/> implementations.
    /// </summary>
    private static async Task<TProjection?> ResolveFromNonGenericFactoryAsync<TProjection>(
        IServiceProvider serviceProvider,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory streamFactory,
        string? blobName,
        bool createIfNotExists)
        where TProjection : Projection
    {
        var factories = serviceProvider.GetServices<IProjectionFactory>();
        var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == typeof(TProjection));

        if (matchingFactory == null)
        {
            return null;
        }

        // Note: Non-generic factory doesn't have ExistsAsync, so we can't check existence
        // The factory's GetOrCreateProjectionAsync will handle this internally
        if (!createIfNotExists)
        {
            // For non-generic factories, we have to try loading and catch the exception
            // This is less efficient but maintains compatibility
            try
            {
                var projection = await matchingFactory.GetOrCreateProjectionAsync(
                    documentFactory,
                    streamFactory,
                    blobName);

                return (TProjection)projection;
            }
            catch (Exception ex) when (IsNotFoundError(ex))
            {
                throw new ProjectionNotFoundException(typeof(TProjection), blobName);
            }
        }

        var result = await matchingFactory.GetOrCreateProjectionAsync(
            documentFactory,
            streamFactory,
            blobName);

        return (TProjection)result;
    }

    /// <summary>
    /// Determines if an exception represents a "not found" error from the underlying store.
    /// </summary>
    private static bool IsNotFoundError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("not found")
            || message.Contains("does not exist")
            || message.Contains("404");
    }
}
