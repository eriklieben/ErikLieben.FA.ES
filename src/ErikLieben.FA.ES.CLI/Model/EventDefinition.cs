namespace ErikLieben.FA.ES.CLI.Model;

public record EventDefinition
{
    public required string TypeName { get; init; }

    public required string Namespace { get; init; }

    public string File { get; init; } = string.Empty;

    public required string EventName { get; init; }

    /// <summary>
    /// The schema version of the event. Defaults to 1 if not specified via [EventVersion] attribute.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    public required string ActivationType { get; init; }

    public required bool ActivationAwaitRequired { get; init; }

    public List<ParameterDefinition> Parameters { get; init; } = [];

    public List<PropertyDefinition> Properties { get; init; } = [];
}
