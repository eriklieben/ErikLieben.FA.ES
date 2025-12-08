#pragma warning disable S2326 // TEvent is used at compile-time for type-safe event binding

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a method as a When handler for a specific event type in a projection.
/// This attribute allows flexible method naming and avoids unused parameter warnings
/// when the event data isn't directly used in the method body.
/// </summary>
/// <typeparam name="TEvent">The event type that this When method handles.</typeparam>
/// <example>
/// <code>
/// [When&lt;ProjectDeleted&gt;]
/// public void MarkAsDeleted()
/// {
///     IsDeleted = true;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WhenAttribute<TEvent> : Attribute where TEvent : class
{
}
