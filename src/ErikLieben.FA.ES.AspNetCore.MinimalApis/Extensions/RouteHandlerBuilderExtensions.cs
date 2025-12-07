using ErikLieben.FA.ES.AspNetCore.MinimalApis.Filters;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Extensions;

/// <summary>
/// Extension methods for <see cref="RouteHandlerBuilder"/> to add projection output processing.
/// </summary>
public static class RouteHandlerBuilderExtensions
{
    /// <summary>
    /// Adds a projection output filter that updates and saves the specified projection type
    /// after successful endpoint execution.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to update.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method adds an endpoint filter that:
    /// <list type="number">
    /// <item>Executes the endpoint handler</item>
    /// <item>Loads the projection from storage</item>
    /// <item>Updates the projection to the latest version by folding new events</item>
    /// <item>Saves the projection back to storage</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// app.MapPost("/orders/{id}/items", CreateOrderItem)
    ///     .WithProjectionOutput&lt;OrderSummaryProjection&gt;();
    /// </code>
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder WithProjectionOutput<TProjection>(this RouteHandlerBuilder builder)
        where TProjection : Projection
    {
        return builder.AddEndpointFilter(new ProjectionOutputFilter<TProjection>());
    }

    /// <summary>
    /// Adds a projection output filter that updates and saves a routed projection
    /// after successful endpoint execution.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to update.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="blobNamePattern">
    /// The blob name pattern for the routed projection. Supports route parameter substitution.
    /// For example, "{id}" will be replaced with the value of the "id" route parameter.
    /// </param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// app.MapPost("/orders/{id}/complete", CompleteOrder)
    ///     .WithProjectionOutput&lt;OrderSummaryProjection&gt;("{id}");
    /// </code>
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder WithProjectionOutput<TProjection>(
        this RouteHandlerBuilder builder,
        string blobNamePattern)
        where TProjection : Projection
    {
        return builder.AddEndpointFilter(new ProjectionOutputFilter<TProjection>(blobNamePattern));
    }

    /// <summary>
    /// Adds a projection output filter with full configuration options.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to update.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="blobNamePattern">
    /// The blob name pattern for the routed projection, or <c>null</c> for the default blob name.
    /// </param>
    /// <param name="saveAfterUpdate">
    /// Whether to save the projection after updating. Defaults to <c>true</c>.
    /// Set to <c>false</c> if you want to handle saving manually.
    /// </param>
    /// <returns>The route handler builder for chaining.</returns>
    public static RouteHandlerBuilder WithProjectionOutput<TProjection>(
        this RouteHandlerBuilder builder,
        string? blobNamePattern,
        bool saveAfterUpdate)
        where TProjection : Projection
    {
        return builder.AddEndpointFilter(new ProjectionOutputFilter<TProjection>(blobNamePattern, saveAfterUpdate));
    }

    /// <summary>
    /// Updates and saves the specified projection to its latest version after successful endpoint execution.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to update.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is an alias for <see cref="WithProjectionOutput{TProjection}(RouteHandlerBuilder)"/>
    /// with a more descriptive name that emphasizes the "update to latest" behavior.
    /// </para>
    /// <para>
    /// After the endpoint handler completes successfully, the projection is:
    /// <list type="number">
    /// <item>Loaded from storage</item>
    /// <item>Updated to the latest version by folding any new events</item>
    /// <item>Saved back to storage</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// app.MapPost("/orders/{id}/items", CreateOrderItem)
    ///     .AndUpdateProjectionToLatest&lt;OrderSummaryProjection&gt;();
    /// </code>
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder AndUpdateProjectionToLatest<TProjection>(this RouteHandlerBuilder builder)
        where TProjection : Projection
    {
        return builder.WithProjectionOutput<TProjection>();
    }

    /// <summary>
    /// Updates and saves a routed projection to its latest version after successful endpoint execution.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to update.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="blobNamePattern">
    /// The blob name pattern for the routed projection. Supports route parameter substitution.
    /// </param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// app.MapPost("/orders/{id}/complete", CompleteOrder)
    ///     .AndUpdateProjectionToLatest&lt;OrderSummaryProjection&gt;("{id}");
    /// </code>
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder AndUpdateProjectionToLatest<TProjection>(
        this RouteHandlerBuilder builder,
        string blobNamePattern)
        where TProjection : Projection
    {
        return builder.WithProjectionOutput<TProjection>(blobNamePattern);
    }
}
