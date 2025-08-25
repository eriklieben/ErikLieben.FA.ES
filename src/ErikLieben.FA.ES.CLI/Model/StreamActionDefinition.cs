namespace ErikLieben.FA.ES.CLI.Model;

public record StreamActionDefinition
{
    public required string Namespace { get; init; }

    public required string Type { get; init; }

    public required List<string> StreamActionInterfaces { get; init; } = new();
}
