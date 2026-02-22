using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Represents a routing decision - which destination should receive an event.
/// </summary>
public class RouteTarget
{
    /// <summary>
    /// Destination key to route to.
    /// </summary>
    public string DestinationKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Override event to send to this destination.
    /// If null, sends the original event.
    /// </summary>
    public IEvent? CustomEvent { get; set; }

    /// <summary>
    /// Optional: Custom metadata for this routing.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Optional: Custom execution context for this destination.
    /// If null, uses the parent context passed to Fold.
    /// </summary>
    public IExecutionContext? Context { get; set; }
}
