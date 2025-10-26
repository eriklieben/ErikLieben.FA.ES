namespace ErikLieben.FA.ES.Upcasting;

/// <summary>
/// Interface for event upcasters that migrate events from old versions to new versions.
/// </summary>
public interface IEventUpcaster
{
    /// <summary>
    /// Determines whether the upcaster can handle the specified event.
    /// </summary>
    /// <param name="event">The event to check.</param>
    /// <returns>True if the upcaster can upcast this event; otherwise, false.</returns>
    public bool CanUpcast(IEvent @event);

    /// <summary>
    /// Upcasts an event to one or more newer event versions.
    /// </summary>
    /// <param name="event">The event to upcast.</param>
    /// <returns>A collection of upcasted events (may be one or more events).</returns>
    public IEnumerable<IEvent> UpCast(IEvent @event);
}