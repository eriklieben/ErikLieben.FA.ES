using ErikLieben.FA.ES.Upcasting;

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies an <see cref="IUpcastEvent"/> implementation to register with the aggregate's event stream.
/// </summary>
/// <typeparam name="TUpcaster">The type that implements <see cref="IUpcastEvent"/>.</typeparam>
/// <remarks>
/// <para>
/// Apply this attribute to an aggregate class to register upcasters that migrate events
/// from old schema versions to new ones. The upcaster class must implement <see cref="IUpcastEvent"/>.
/// </para>
/// <para>
/// The CLI code generator will detect this attribute and generate the appropriate
/// <c>RegisterUpcast</c> call in the aggregate's Setup method.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Define an upcaster class
/// public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
/// {
///     public bool CanUpcast(IEvent @event)
///         => @event.EventType == "order.created" &amp;&amp; @event.SchemaVersion == 1;
///
///     public IEnumerable&lt;IEvent&gt; UpCast(IEvent @event)
///     {
///         var v1 = JsonEvent.To(@event, OrderCreatedV1Context.Default.OrderCreatedV1);
///         yield return new JsonEvent
///         {
///             EventType = "order.created",
///             SchemaVersion = 2,
///             Payload = JsonSerializer.Serialize(new OrderCreatedV2(v1.OrderId, ""))
///         };
///     }
/// }
///
/// // Register it on the aggregate
/// [UseUpcaster&lt;OrderCreatedV1ToV2Upcaster&gt;]
/// public partial class Order : AggregateBase&lt;Guid&gt;
/// {
///     // ...
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class UseUpcasterAttribute<TUpcaster> : Attribute
    where TUpcaster : IUpcastEvent, new()
{
    /// <summary>
    /// Gets the type that implements <see cref="IUpcastEvent"/>.
    /// </summary>
    public Type UpcasterType { get; } = typeof(TUpcaster);
}
