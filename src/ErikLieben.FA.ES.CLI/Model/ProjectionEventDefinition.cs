namespace ErikLieben.FA.ES.CLI.Model;

public record ProjectionEventDefinition : EventDefinition
{
    public List<WhenParameterValueFactory> WhenParameterValueFactories { get; init; } = [];
    
    public List<WhenParameterDeclaration> WhenParameterDeclarations { get; init; } = [];
}

public record WhenParameterValueFactory
{
    public required WhenParameterValueItem Type { get; init; }
    public required WhenParameterValueItem ForType { get; init; }

    public string? EventType { get; init; }
}

public record WhenParameterValueItem
{
    public required string Type { get; init; }
    
    public required string Namespace { get; init; }
}

public record WhenParameterDeclaration
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Namespace { get; init; }

    public required List<GenericArgument> GenericArguments { get; init; } = [];

    /// <summary>
    /// Indicates if this parameter type implements IExecutionContext.
    /// </summary>
    public bool IsExecutionContext { get; init; }
}

public record GenericArgument
{
    public required string Type { get; init; }
    
    public required string Namespace { get; init; }
}
