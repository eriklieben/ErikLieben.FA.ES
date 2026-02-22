using System.Reflection;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Http;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis;

/// <summary>
/// Provides helper methods for binding event streams and projections in Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This class provides static methods that can be used directly in endpoint handlers
/// when attribute-based binding is not sufficient or when more control is needed.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// app.MapPost("/orders/{id}/items", async (HttpContext context) =>
/// {
///     var order = await EventStoreEndpointExtensions.BindEventStreamAsync&lt;Order&gt;(context, "id");
///     // Use order...
/// });
/// </code>
/// </para>
/// </remarks>
public static class EventStoreEndpointExtensions
{
    /// <summary>
    /// Binds an aggregate from the current HTTP context using the specified route parameter.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="routeParameterName">The name of the route parameter containing the object ID.</param>
    /// <param name="createIfNotExists">Whether to create the aggregate if it doesn't exist.</param>
    /// <param name="objectType">Optional object type override.</param>
    /// <param name="store">Optional store name.</param>
    /// <returns>The bound aggregate instance.</returns>
    public static async Task<TAggregate> BindEventStreamAsync<TAggregate>(
        HttpContext context,
        string routeParameterName = "id",
        bool createIfNotExists = false,
        string? objectType = null,
        string? store = null)
        where TAggregate : class, IBase
    {
        var objectId = RouteValueResolver.GetRouteValue(context.Request.RouteValues, routeParameterName);

        if (string.IsNullOrEmpty(objectId))
        {
            throw new ArgumentException(
                $"Route parameter '{routeParameterName}' not found or is empty.",
                nameof(routeParameterName));
        }

        return await EventStreamBinder.BindCoreAsync<TAggregate>(
            context.RequestServices,
            objectId,
            objectType,
            createIfNotExists,
            store);
    }

    /// <summary>
    /// Binds an aggregate from the current HTTP context using a specific object ID.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="createIfNotExists">Whether to create the aggregate if it doesn't exist.</param>
    /// <param name="objectType">Optional object type override.</param>
    /// <param name="store">Optional store name.</param>
    /// <returns>The bound aggregate instance.</returns>
    public static async Task<TAggregate> BindEventStreamByIdAsync<TAggregate>(
        HttpContext context,
        string objectId,
        bool createIfNotExists = false,
        string? objectType = null,
        string? store = null)
        where TAggregate : class, IBase
    {
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        return await EventStreamBinder.BindCoreAsync<TAggregate>(
            context.RequestServices,
            objectId,
            objectType,
            createIfNotExists,
            store);
    }

    /// <summary>
    /// Binds a projection from the current HTTP context.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="context">The HTTP context.</param>
    /// <param name="blobNamePattern">Optional blob name pattern (supports route parameter substitution).</param>
    /// <param name="createIfNotExists">Whether to create the projection if it doesn't exist.</param>
    /// <returns>The bound projection instance.</returns>
    public static async Task<TProjection> BindProjectionAsync<TProjection>(
        HttpContext context,
        string? blobNamePattern = null,
        bool createIfNotExists = true)
        where TProjection : Projection
    {
        var blobName = RouteValueResolver.SubstituteRouteValues(
            blobNamePattern,
            context.Request.RouteValues);

        return await ProjectionBinder.BindCoreAsync<TProjection>(
            context.RequestServices,
            blobName,
            createIfNotExists);
    }

    /// <summary>
    /// Creates a parameter binding delegate for use with Minimal API parameter binding.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <returns>A delegate that can be used for parameter binding.</returns>
    /// <remarks>
    /// <para>
    /// This method returns a delegate compatible with Minimal API's custom binding pattern.
    /// </para>
    /// <para>
    /// Example registration:
    /// <code>
    /// // In a partial class for your aggregate:
    /// public partial class Order
    /// {
    ///     public static ValueTask&lt;Order?&gt; BindAsync(HttpContext context, ParameterInfo parameter)
    ///         => EventStoreEndpointExtensions.CreateAggregateBindingDelegate&lt;Order&gt;()(context, parameter);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static Func<HttpContext, ParameterInfo, ValueTask<TAggregate?>> CreateAggregateBindingDelegate<TAggregate>()
        where TAggregate : class, IBase
    {
        return EventStreamBinder.BindAsync<TAggregate>;
    }

    /// <summary>
    /// Creates a parameter binding delegate for projections.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <returns>A delegate that can be used for parameter binding.</returns>
    public static Func<HttpContext, ParameterInfo, ValueTask<TProjection?>> CreateProjectionBindingDelegate<TProjection>()
        where TProjection : Projection
    {
        return ProjectionBinder.BindAsync<TProjection>;
    }
}
