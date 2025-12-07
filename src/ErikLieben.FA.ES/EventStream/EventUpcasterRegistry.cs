using System.Collections.Frozen;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Represents an upcaster that transforms an event from one schema version to another.
/// </summary>
/// <param name="FromVersion">The source schema version.</param>
/// <param name="ToVersion">The target schema version.</param>
/// <param name="Upcast">The function that performs the transformation. Takes the source event and returns the upcasted event.</param>
public record EventUpcaster(int FromVersion, int ToVersion, Func<object, object> Upcast);

/// <summary>
/// Key for looking up upcasters by event name and source version.
/// </summary>
/// <param name="EventName">The event name.</param>
/// <param name="FromVersion">The source schema version.</param>
public readonly record struct EventUpcasterKey(string EventName, int FromVersion);

/// <summary>
/// Registry for managing event upcasters that transform events between schema versions.
/// Supports AOT compilation by using pre-registered delegate functions.
/// </summary>
public class EventUpcasterRegistry
{
    private readonly Dictionary<EventUpcasterKey, EventUpcaster> upcastersMutable = new();
    private FrozenDictionary<EventUpcasterKey, EventUpcaster>? upcastersFrozen;
    private bool isFrozen = false;

    /// <summary>
    /// Registers an upcaster for transforming events from one schema version to another.
    /// </summary>
    /// <typeparam name="TFrom">The source event type.</typeparam>
    /// <typeparam name="TTo">The target event type.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="fromVersion">The source schema version.</param>
    /// <param name="toVersion">The target schema version.</param>
    /// <param name="upcast">The function that transforms the event.</param>
    /// <exception cref="InvalidOperationException">Thrown when attempting to add to a frozen registry.</exception>
    public void Add<TFrom, TTo>(string eventName, int fromVersion, int toVersion, Func<TFrom, TTo> upcast)
        where TFrom : class
        where TTo : class
    {
        if (isFrozen)
        {
            throw new InvalidOperationException("Cannot add upcasters to a frozen registry. Call Add before calling Freeze().");
        }

        var key = new EventUpcasterKey(eventName, fromVersion);
        var upcaster = new EventUpcaster(fromVersion, toVersion, obj => upcast((TFrom)obj));
        upcastersMutable[key] = upcaster;
    }

    /// <summary>
    /// Freezes the registry to use optimized frozen collections for lookups.
    /// After freezing, no more upcasters can be added.
    /// </summary>
    public void Freeze()
    {
        if (isFrozen)
        {
            return;
        }

        upcastersFrozen = upcastersMutable.ToFrozenDictionary();
        isFrozen = true;
    }

    /// <summary>
    /// Tries to get an upcaster for the specified event name and source version.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="fromVersion">The source schema version.</param>
    /// <param name="upcaster">The upcaster if found; otherwise, null.</param>
    /// <returns>True if an upcaster was found; otherwise, false.</returns>
    public bool TryGetUpcaster(string eventName, int fromVersion, out EventUpcaster? upcaster)
    {
        var key = new EventUpcasterKey(eventName, fromVersion);
        if (isFrozen)
        {
            return upcastersFrozen!.TryGetValue(key, out upcaster);
        }
        return upcastersMutable.TryGetValue(key, out upcaster);
    }

    /// <summary>
    /// Upcasts an event to the latest schema version by applying all registered upcasters in sequence.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="currentVersion">The current schema version of the event.</param>
    /// <param name="targetVersion">The target schema version to upcast to.</param>
    /// <param name="eventData">The event data to upcast.</param>
    /// <returns>The upcasted event data and the final schema version.</returns>
    public (object Data, int SchemaVersion) UpcastToVersion(string eventName, int currentVersion, int targetVersion, object eventData)
    {
        var data = eventData;
        var version = currentVersion;

        while (version < targetVersion)
        {
            if (!TryGetUpcaster(eventName, version, out var upcaster) || upcaster == null)
            {
                // No upcaster found for this version, return current state
                break;
            }

            data = upcaster.Upcast(data);
            version = upcaster.ToVersion;
        }

        return (data, version);
    }
}
