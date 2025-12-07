namespace ErikLieben.FA.ES.CLI.Model;

public record StreamActionDefinition
{
    public required string Namespace { get; init; }

    public required string Type { get; init; }

    public required List<string> StreamActionInterfaces { get; init; } = [];

    /// <summary>
    /// How the stream action was registered: "Attribute" or "Manual"
    /// </summary>
    public string RegistrationType { get; init; } = "Attribute";
}
