namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies a custom name for an event type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class EventNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventNameAttribute"/> class with the specified event name.
    /// </summary>
    /// <param name="name">The custom name for the event.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public EventNameAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }

    /// <summary>
    /// Gets the custom name for the event.
    /// </summary>
    public string Name { get; init; }
}