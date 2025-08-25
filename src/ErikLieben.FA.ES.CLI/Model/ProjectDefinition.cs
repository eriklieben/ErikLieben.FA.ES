namespace ErikLieben.FA.ES.CLI.Model;

public record ProjectDefinition
{
    public required string Name { get; init; }

    public List<AggregateDefinition> Aggregates { get; init; } = [];

    public List<InheritedAggregateDefinition> InheritedAggregates { get; init; } = [];

    public List<ProjectionDefinition> Projections { get; init; } = [];

    public List<VersionTokenDefinition> VersionTokens { get; init; } = [];

    public List<VersionTokenJsonConverterDefinition> VersionTokenJsonConverterDefinitions { get; init; } = [];

    public required string FileLocation { get; init; }

    public required string Namespace { get; init; }
}
