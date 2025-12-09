namespace ErikLieben.FA.ES.CLI.Model;

public record CosmosDbProjectionDefinition
{
    public required string Container { get; init; }

    public required string Connection { get; init; }

    public string PartitionKeyPath { get; init; } = "/projectionName";
}
