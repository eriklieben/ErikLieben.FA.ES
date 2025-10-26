namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a class or struct to be ignored during processing.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class IgnoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreAttribute"/> class with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for ignoring this type.</param>
    public IgnoreAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the reason why this type should be ignored.
    /// </summary>
    public string Reason { get; init; }
}
