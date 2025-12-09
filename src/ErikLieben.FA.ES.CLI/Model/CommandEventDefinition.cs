namespace ErikLieben.FA.ES.CLI.Model;

public record CommandEventDefinition
{
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string File { get; init; }
    public required string EventName { get; init; }
    public int SchemaVersion { get; init; } = 1;
}