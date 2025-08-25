namespace ErikLieben.FA.ES.CLI.Model;

public record ConstructorParameter
{

    public required string Name { get; init; }

    public required bool IsNullable { get; init; }

    public required string Namespace { get; init; }

    public required string Type { get; init; }

    public bool IsGeneric => GenericTypes.Count > 0;
    public List<ParameterGenericTypeDefinition> GenericTypes { get; init; } = [];
}
