namespace ErikLieben.FA.ES.AspNetCore.MinimalApis;

/// <summary>
/// Marks a parameter for automatic event stream/aggregate binding in Minimal API endpoints.
/// The aggregate will be loaded from the event store, with all events folded into state.
/// </summary>
/// <remarks>
/// <para>
/// Usage examples:
/// <code>
/// // Uses "id" route parameter by default
/// app.MapPost("/orders/{id}/items", async ([EventStream] Order order) => { });
///
/// // Specify custom route parameter name
/// app.MapPost("/orders/{orderId}/ship", async ([EventStream("orderId")] Order order) => { });
///
/// // Create new aggregate if doesn't exist
/// app.MapPost("/orders", async ([EventStream(CreateIfNotExists = true)] Order order) => { });
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class EventStreamAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the route parameter that contains the object identifier.
    /// </summary>
    /// <remarks>
    /// Defaults to "id" if not specified. The value from this route parameter
    /// will be used to load the aggregate from the event store.
    /// </remarks>
    public string RouteParameterName { get; }

    /// <summary>
    /// Gets or sets the object type name used for document storage.
    /// </summary>
    /// <remarks>
    /// If not specified, the aggregate type name will be used.
    /// This determines the container/path in the underlying document store.
    /// </remarks>
    public string? ObjectType { get; set; }

    /// <summary>
    /// Gets or sets whether to create a new aggregate if it doesn't exist.
    /// </summary>
    /// <remarks>
    /// When <c>false</c> (default), an exception is thrown if the aggregate doesn't exist.
    /// When <c>true</c>, a new empty aggregate is created and returned.
    /// </remarks>
    public bool CreateIfNotExists { get; set; }

    /// <summary>
    /// Gets or sets the store name for document resolution.
    /// </summary>
    /// <remarks>
    /// Optional. If not specified, the default store is used.
    /// Use this when working with multiple document stores.
    /// </remarks>
    public string? Store { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamAttribute"/> class
    /// using "id" as the default route parameter name.
    /// </summary>
    public EventStreamAttribute() : this("id")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamAttribute"/> class
    /// with the specified route parameter name.
    /// </summary>
    /// <param name="routeParameterName">
    /// The name of the route parameter containing the object identifier.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="routeParameterName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="routeParameterName"/> is empty or whitespace.
    /// </exception>
    public EventStreamAttribute(string routeParameterName)
    {
        ArgumentNullException.ThrowIfNull(routeParameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeParameterName);
        RouteParameterName = routeParameterName;
    }
}
