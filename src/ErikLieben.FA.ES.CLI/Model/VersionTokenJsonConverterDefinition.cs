namespace ErikLieben.FA.ES.CLI.Model;

public record VersionTokenJsonConverterDefinition
{
    public required string Name { get; init; }
    
    public required string Namespace { get; init; }
    
    public List<string> FileLocations { get; init; } = [];
    
    public bool IsPartialClass { get; set; } = false;
}