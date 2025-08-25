namespace ErikLieben.FA.ES.CLI.Model;

public record ProjectionDefinition
{
    public required string Name { get; init; }

    public required string Namespace { get; init; }


    public List<ConstructorDefinition> Constructors { get; init; } = [];

    public List<ProjectionEventDefinition> Events { get; init; } = [];

    public List<PropertyDefinition> Properties { get; init; } = [];

    public List<string> FileLocations { get; init; } = [];

    public bool ExternalCheckpoint { get; set; }

    public BlobProjectionDefinition? BlobProjection { get; set; }

    public PostWhenDeclaration? PostWhen { get; set; }

    public bool HasPostWhenAllMethod { get; set; }
}
