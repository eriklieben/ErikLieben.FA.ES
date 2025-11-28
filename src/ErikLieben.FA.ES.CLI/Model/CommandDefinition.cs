namespace ErikLieben.FA.ES.CLI.Model;

public record CommandDefinition
{
    public required CommandReturnType ReturnType { get; init; }
    public required string CommandName { get; init; }
    
    public required bool RequiresAwait { get; init; }

    public required List<CommandParameter> Parameters { get; init; } = [];
    
    public List<CommandEventDefinition> ProducesEvents { get; init; } = [];
}

public record CommandParameter
{
    public required string Name { get; init; }
    
    public required string Type { get; init; }
    
    public required string Namespace { get; init; }
    
    public required bool IsGeneric { get; init; }
    
    public required List<PropertyGenericTypeDefinition> GenericTypes { get; init; } = [];
}

public record CommandReturnType
{
    
    public required string Namespace { get; init; }
    
    public required string Type { get; init; }
}