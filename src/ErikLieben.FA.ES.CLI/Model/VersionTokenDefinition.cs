namespace ErikLieben.FA.ES.CLI.Model;

public record VersionTokenDefinition
{
    public required string Name { get; init; }

    public required string Namespace { get; init; }

    public List<string> FileLocations { get; init; } = [];

    public required string GenericType { get; init; }

    public required string NamespaceOfType { get; init; }

    public bool IsPartialClass { get; set; } = false;
}
