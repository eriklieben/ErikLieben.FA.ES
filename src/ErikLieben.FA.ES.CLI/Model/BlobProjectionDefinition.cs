namespace ErikLieben.FA.ES.CLI.Model;

public record BlobProjectionDefinition
{
    public required string Container { get; init; }
    
    public required string Connection { get; init; }
}