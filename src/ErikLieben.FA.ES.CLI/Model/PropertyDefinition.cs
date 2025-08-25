namespace ErikLieben.FA.ES.CLI.Model;

public record PropertyDefinition
{
    public required string Name { get; init; }

    public required bool IsNullable { get; init; }

    public required string Namespace { get; init; }

    public required string Type { get; init; }

    public bool IsGeneric => GenericTypes.Count > 0;

    public List<PropertyGenericTypeDefinition> GenericTypes { get; init; } = [];

    public List<PropertyGenericTypeDefinition> SubTypes { get; init; } = [];


}

public record PropertyGenericTypeDefinition(
    string Name,
    string Namespace,
    List<PropertyGenericTypeDefinition> GenericTypes,
    List<PropertyGenericTypeDefinition> SubTypes);
