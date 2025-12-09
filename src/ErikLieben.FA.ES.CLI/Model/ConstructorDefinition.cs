namespace ErikLieben.FA.ES.CLI.Model;

public record ConstructorDefinition
{
    public List<ConstructorParameter> Parameters { get; init; } = [];
}