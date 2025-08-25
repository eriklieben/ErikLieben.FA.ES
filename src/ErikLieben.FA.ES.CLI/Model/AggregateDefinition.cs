namespace ErikLieben.FA.ES.CLI.Model;

public record AggregateDefinition
{
    public required string IdentifierName { get; init; }

    public required string ObjectName { get; set; }

    public required string IdentifierType { get; set; }

    public required string IdentifierTypeNamespace { get; set; }

    public required string Namespace { get; init; }

    public List<ConstructorDefinition> Constructors { get; init; } = [];

    public List<EventDefinition> Events { get; init; } = [];

    public List<CommandDefinition> Commands { get; init; } = [];

     public List<PropertyDefinition> Properties { get; init; } = [];

    public List<string> FileLocations { get; init; } = [];

    public PostWhenDeclaration? PostWhen { get; set; }

    public List<StreamActionDefinition> StreamActions { get; init; } = [];

    public bool IsPartialClass { get; init; }
}

public record PostWhenDeclaration
{
    public List<PostWhenParameterDeclaration> Parameters { get; init; } = new();
}

public record PostWhenParameterDeclaration
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Namespace { get; init; }
}




public record InheritedAggregateDefinition
{
    public required string InheritedIdentifierName { get; init; }

    public required string InheritedNamespace { get; init; }

    public required string IdentifierName { get; init; }

    public required string ObjectName { get; set; }

    public required string IdentifierType { get; set; }

    public required string IdentifierTypeNamespace { get; set; }

    public required string Namespace { get; init; }

    public List<ConstructorDefinition> Constructors { get; init; } = [];

    public List<CommandDefinition> Commands { get; init; } = new();

    public List<PropertyDefinition> Properties { get; init; } = new();

    public List<string> FileLocations { get; init; } = new();

    public required string ParentInterface { get; init; } = string.Empty;

    public required string ParentInterfaceNamespace { get; init; } = string.Empty;
}
