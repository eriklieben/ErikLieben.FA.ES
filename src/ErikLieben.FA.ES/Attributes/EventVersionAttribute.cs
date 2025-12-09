namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies the schema version for an event type.
/// </summary>
/// <remarks>
/// Use this attribute to indicate when an event's schema has changed in a breaking way.
/// The version is stored separately from the event name, allowing you to track schema evolution
/// without modifying the event name. If not specified, the schema version defaults to 1.
/// </remarks>
/// <example>
/// <code>
/// [EventName("work.item.created")]
/// [EventVersion(2)]
/// public record WorkItemCreated(string Title, string Description, Priority Priority);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class EventVersionAttribute : Attribute
{
    /// <summary>
    /// The default schema version used when no attribute is specified.
    /// </summary>
    public const int DefaultVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventVersionAttribute"/> class with the specified version.
    /// </summary>
    /// <param name="version">The schema version for the event. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 1.</exception>
    public EventVersionAttribute(int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Schema version must be at least 1.");
        }

        Version = version;
    }

    /// <summary>
    /// Gets the schema version for the event.
    /// </summary>
    public int Version { get; init; }
}
