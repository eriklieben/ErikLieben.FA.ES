namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies a custom name for an object type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ObjectNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectNameAttribute"/> class with the specified object name.
    /// </summary>
    /// <param name="name">The custom name for the object.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public ObjectNameAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }

    /// <summary>
    /// Gets the custom name for the object.
    /// </summary>
    public string Name { get; init; }
}
    