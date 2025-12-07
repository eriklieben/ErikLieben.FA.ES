namespace ErikLieben.FA.ES.CLI.Model;

public record ParameterDefinition
{

    public required string Name { get; init; }

    public bool IsNullable { get; init; } = false;

    public required string Namespace { get; init; }

    public required string Type { get; init; }

    public bool IsGeneric => GenericTypes.Count > 0;

    public List<ParameterGenericTypeDefinition> GenericTypes { get; init; } = [];

    public List<ParameterGenericTypeDefinition> SubTypes { get; init; } = [];
}

public record ParameterGenericTypeDefinition(
    string Name,
    string Namespace,
    List<ParameterGenericTypeDefinition> GenericTypes,
    List<ParameterGenericTypeDefinition> SubTypes);
