using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CLI.Model;

public record SolutionDefinition
{
    [JsonPropertyOrder(1)]
    public required string SolutionName { get; init; }
 
    [JsonPropertyOrder(2)]
    public GeneratorInformation? Generator { get; init; }
    
    [JsonPropertyOrder(3)]
    public List<ProjectDefinition> Projects { get; init; } = new();
}

public record GeneratorInformation
{
    public string? Version { get; init; }
}