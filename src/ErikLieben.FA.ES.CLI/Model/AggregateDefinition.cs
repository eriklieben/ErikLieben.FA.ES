namespace ErikLieben.FA.ES.CLI.Model;

public record AggregateDefinition
{
    public required string IdentifierName { get; init; }

    public required string ObjectName { get; set; }

    public required string IdentifierType { get; set; }

    public required string IdentifierTypeNamespace { get; set; }

    /// <summary>
    /// The inner/underlying type of a strongly-typed ID (e.g., "Guid" for StronglyTypedId&lt;Guid&gt;, "string" for StronglyTypedId&lt;string&gt;).
    /// </summary>
    public string? IdentifierInnerType { get; set; }

    public required string Namespace { get; init; }

    public List<ConstructorDefinition> Constructors { get; init; } = [];

    public List<EventDefinition> Events { get; init; } = [];

    public List<CommandDefinition> Commands { get; init; } = [];

     public List<PropertyDefinition> Properties { get; init; } = [];

    public List<string> FileLocations { get; init; } = [];

    public PostWhenDeclaration? PostWhen { get; set; }

    public List<StreamActionDefinition> StreamActions { get; init; } = [];

    /// <summary>
    /// Upcasters to register, extracted from [UseUpcaster] attributes.
    /// </summary>
    public List<UpcasterDefinition> Upcasters { get; init; } = [];

    public bool IsPartialClass { get; init; }

    /// <summary>
    /// Indicates whether the user has defined their own partial factory class.
    /// When true, generated CreateAsync methods will be protected instead of public.
    /// </summary>
    public bool HasUserDefinedFactoryPartial { get; set; }

    /// <summary>
    /// Indicates whether the user has defined their own partial repository class.
    /// When true, generated repository methods will be hidden from IntelliSense.
    /// </summary>
    public bool HasUserDefinedRepositoryPartial { get; set; }

    /// <summary>
    /// Indicates whether the user has defined their own ProcessSnapshot method override.
    /// When true, the code generator will not emit a default ProcessSnapshot implementation.
    /// </summary>
    public bool HasUserDefinedProcessSnapshot { get; set; }

    /// <summary>
    /// Settings extracted from [EventStreamType] attribute if present.
    /// </summary>
    public EventStreamTypeAttributeData? EventStreamTypeAttribute { get; set; }

    /// <summary>
    /// Settings extracted from [EventStreamBlobSettings] attribute if present.
    /// </summary>
    public EventStreamBlobSettingsAttributeData? EventStreamBlobSettingsAttribute { get; set; }
}

public record PostWhenDeclaration
{
    public List<PostWhenParameterDeclaration> Parameters { get; init; } = [];
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

    public List<CommandDefinition> Commands { get; init; } = [];

    public List<PropertyDefinition> Properties { get; init; } = [];

    public List<string> FileLocations { get; init; } = [];

    public required string ParentInterface { get; init; } = string.Empty;

    public required string ParentInterfaceNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Settings extracted from [EventStreamType] attribute if present.
    /// </summary>
    public EventStreamTypeAttributeData? EventStreamTypeAttribute { get; set; }

    /// <summary>
    /// Settings extracted from [EventStreamBlobSettings] attribute if present.
    /// </summary>
    public EventStreamBlobSettingsAttributeData? EventStreamBlobSettingsAttribute { get; set; }
}

/// <summary>
/// Data extracted from [EventStreamType] attribute at compile-time.
/// </summary>
public record EventStreamTypeAttributeData
{
    public string? StreamType { get; init; }
    public string? DocumentType { get; init; }
    public string? DocumentTagType { get; init; }
    public string? EventStreamTagType { get; init; }
    public string? DocumentRefType { get; init; }
}

/// <summary>
/// Data extracted from [EventStreamBlobSettings] attribute at compile-time.
/// </summary>
public record EventStreamBlobSettingsAttributeData
{
    public string? DataStore { get; init; }
    public string? DocumentStore { get; init; }
    public string? DocumentTagStore { get; init; }
    public string? StreamTagStore { get; init; }
    public string? SnapShotStore { get; init; }
}
