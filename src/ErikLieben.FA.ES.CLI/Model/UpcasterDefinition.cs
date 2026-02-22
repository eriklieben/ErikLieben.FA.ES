namespace ErikLieben.FA.ES.CLI.Model;

/// <summary>
/// Represents an upcaster type to register with the aggregate, extracted from [UseUpcaster] attribute.
/// </summary>
public record UpcasterDefinition
{
    /// <summary>
    /// The type name of the IUpcastEvent implementation (without namespace).
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// The namespace of the IUpcastEvent implementation.
    /// </summary>
    public required string Namespace { get; init; }
}
