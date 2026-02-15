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

    /// <summary>
    /// Gets or sets the schema version for the projection (from [ProjectionVersion] attribute).
    /// Defaults to 1 if not specified.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public BlobProjectionDefinition? BlobProjection { get; set; }

    public CosmosDbProjectionDefinition? CosmosDbProjection { get; set; }

    public PostWhenDeclaration? PostWhen { get; set; }

    public bool HasPostWhenAllMethod { get; set; }
}
