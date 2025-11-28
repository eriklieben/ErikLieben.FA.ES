using System.Diagnostics;
using System.Reflection;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;
using ErikLieben.FA.ES.Processors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;

/// <summary>
/// Provides parameter binding functionality for event stream aggregates in Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This binder loads aggregates from the event store by:
/// <list type="number">
/// <item>Extracting the object ID from route parameters</item>
/// <item>Resolving the aggregate factory from DI</item>
/// <item>Loading or creating the document from the document store</item>
/// <item>Creating an event stream and aggregate instance</item>
/// <item>Folding all events to rebuild the aggregate state</item>
/// </list>
/// </para>
/// </remarks>
public static class EventStreamBinder
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AspNetCore.MinimalApis");

    /// <summary>
    /// Binds an aggregate from an HTTP context for a Minimal API endpoint.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type to bind.</typeparam>
    /// <param name="context">The HTTP context containing route values and services.</param>
    /// <param name="parameter">The parameter info for the aggregate parameter.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> containing the bound aggregate,
    /// or <c>null</c> if the route parameter is missing or empty.
    /// </returns>
    /// <exception cref="BindingException">
    /// Thrown when the required route parameter is not found or binding fails.
    /// </exception>
    /// <exception cref="AggregateNotFoundException">
    /// Thrown when the aggregate doesn't exist and <see cref="EventStreamAttribute.CreateIfNotExists"/> is <c>false</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no aggregate factory is registered for the requested type.
    /// </exception>
    public static async ValueTask<TAggregate?> BindAsync<TAggregate>(
        HttpContext context,
        ParameterInfo parameter)
        where TAggregate : class, IBase
    {
        using var activity = ActivitySource.StartActivity($"EventStreamBinder.BindAsync<{typeof(TAggregate).Name}>");

        var attribute = parameter.GetCustomAttribute<EventStreamAttribute>();
        var routeParamName = attribute?.RouteParameterName ?? "id";

        // Get object ID from route
        var objectId = RouteValueResolver.GetRouteValue(context.Request.RouteValues, routeParamName);

        if (string.IsNullOrEmpty(objectId))
        {
            throw new BindingException(
                parameter.Name ?? "unknown",
                typeof(TAggregate),
                $"Route parameter '{routeParamName}' not found or is empty.");
        }

        activity?.SetTag("objectId", objectId);
        activity?.SetTag("routeParameter", routeParamName);

        return await BindCoreAsync<TAggregate>(
            context.RequestServices,
            objectId,
            attribute?.ObjectType,
            attribute?.CreateIfNotExists ?? false,
            attribute?.Store);
    }

    /// <summary>
    /// Core binding logic that loads an aggregate from the event store.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type to load.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="objectType">Optional object type override.</param>
    /// <param name="createIfNotExists">Whether to create the aggregate if it doesn't exist.</param>
    /// <param name="store">Optional store name.</param>
    /// <returns>The loaded aggregate instance.</returns>
    internal static async Task<TAggregate> BindCoreAsync<TAggregate>(
        IServiceProvider serviceProvider,
        string objectId,
        string? objectType,
        bool createIfNotExists,
        string? store)
        where TAggregate : class, IBase
    {
        using var activity = ActivitySource.StartActivity($"EventStreamBinder.BindCoreAsync<{typeof(TAggregate).Name}>");

        // Resolve required services
        var aggregateFactory = serviceProvider.GetRequiredService<IAggregateFactory>();
        var documentFactory = serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var streamFactory = serviceProvider.GetRequiredService<IEventStreamFactory>();

        // Get factory for the aggregate type
        var factory = aggregateFactory.GetFactory(typeof(TAggregate));
        if (factory == null)
        {
            throw new InvalidOperationException(
                $"No aggregate factory registered for type '{typeof(TAggregate).Name}'. " +
                "Ensure the aggregate is properly configured in the DI container.");
        }

        // Determine object type name
        var effectiveObjectType = objectType ?? factory.GetObjectName();
        activity?.SetTag("objectType", effectiveObjectType);

        try
        {
            // Load or create document
            var document = createIfNotExists
                ? await documentFactory.GetOrCreateAsync(effectiveObjectType, objectId, store)
                : await documentFactory.GetAsync(effectiveObjectType, objectId, store);

            // Create event stream and aggregate
            var eventStream = streamFactory.Create(document);
            var aggregate = factory.Create(eventStream);

            // Fold all events to rebuild state
            await aggregate.Fold();

            return (TAggregate)aggregate;
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not AggregateNotFoundException)
        {
            // Check if this is a "not found" scenario
            if (!createIfNotExists && IsNotFoundError(ex))
            {
                throw new AggregateNotFoundException(typeof(TAggregate), objectId, effectiveObjectType);
            }

            throw;
        }
    }

    /// <summary>
    /// Determines if an exception represents a "not found" error from the underlying store.
    /// </summary>
    private static bool IsNotFoundError(Exception ex)
    {
        // Check common patterns for "not found" errors
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("not found")
            || message.Contains("does not exist")
            || message.Contains("404");
    }
}
