using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using static ErikLieben.FA.ES.CodeAnalysis.TypeConstants;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.Analyze;

public class AnalyzeAggregates
{
    private readonly INamedTypeSymbol? classSymbol;
    private readonly ClassDeclarationSyntax classDeclaration;
    private readonly SemanticModel semanticModel;
    private readonly Compilation compilation;
    private readonly string solutionRootPath;
    private readonly RoslynHelper roslyn;

    public AnalyzeAggregates(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        Compilation compilation,
        string solutionRootPath)
    {
        ArgumentNullException.ThrowIfNull(classSymbol);
        ArgumentNullException.ThrowIfNull(classDeclaration);
        ArgumentNullException.ThrowIfNull(semanticModel);
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(solutionRootPath);

        this.classSymbol = classSymbol;
        this.classDeclaration = classDeclaration;
        this.semanticModel = semanticModel;
        this.compilation = compilation;
        this.solutionRootPath = solutionRootPath;

        roslyn = new RoslynHelper(semanticModel, solutionRootPath);
    }

    public void Run(List<AggregateDefinition> aggregates)
    {
        if (!roslyn.IsProcessableAggregate(classSymbol))
        {
            return;
        }

        AnsiConsole.MarkupLine("Analyzing aggregate: [yellow]" + classSymbol!.Name + "[/]");
        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        var declaration = GetOrCreateAggregateDefinition(aggregates);
        var newConstructors = ConstructorHelper.GetConstructors(classSymbol)
            .Where(c => declaration.Constructors.All(existing => !AreConstructorsEqual(existing, c)));
        declaration.Constructors.AddRange(newConstructors);
        var newProperties = PropertyHelper.GetPublicGetterProperties(typeSymbol)
            .Where(p => declaration.Properties.All(existing => !ArePropertiesEqual(existing, p)));
        declaration.Properties.AddRange(newProperties);
        var newEvents = WhenMethodHelper.GetEventDefinitions(typeSymbol, compilation, roslyn)
            .Where(e => declaration.Events.All(existing => !AreEventsEqual(existing, e)));
        declaration.Events.AddRange(newEvents);
        var newCommands = CommandHelper.GetCommandMethods(typeSymbol, roslyn)
            .Where(c => declaration.Commands.All(existing => !AreCommandsEqual(existing, c)));
        declaration.Commands.AddRange(newCommands);

        // Merge events from commands into the aggregate's event list (for events without When handlers, like legacy events that get upcasted)
        foreach (var command in declaration.Commands)
        {
            foreach (var commandEvent in command.ProducesEvents)
            {
                // Convert CommandEventDefinition to EventDefinition
                var eventDef = new EventDefinition
                {
                    EventName = commandEvent.EventName,
                    TypeName = commandEvent.TypeName,
                    Namespace = commandEvent.Namespace,
                    File = commandEvent.File,
                    SchemaVersion = commandEvent.SchemaVersion,
                    ActivationType = "Command", // Mark as coming from a command, not a When method
                    ActivationAwaitRequired = false, // Event registration doesn't require await
                    Parameters = [], // Commands don't have When parameters
                    Properties = [] // Will be populated from event type
                };

                // Only add if not already in the events list
                if (declaration.Events.All(existing => !AreEventsEqual(existing, eventDef)))
                {
                    declaration.Events.Add(eventDef);
                }
            }
        }

        if (declaration.PostWhen == null)
        {
            declaration.PostWhen = PostWhenHelper.GetPostWhenMethod(typeSymbol);
        }
        var newStreamActions = GetStreamActions(classSymbol)
            .Where(sa => declaration.StreamActions.All(existing => !AreStreamActionsEqual(existing, sa)));
        declaration.StreamActions.AddRange(newStreamActions);

        // Extract attribute-based settings
        declaration.EventStreamTypeAttribute = AttributeExtractor.ExtractEventStreamTypeAttribute(classSymbol);
        declaration.EventStreamBlobSettingsAttribute = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(classSymbol);

        // Extract upcasters
        var newUpcasters = AttributeExtractor.ExtractUseUpcasterAttributes(classSymbol)
            .Where(u => declaration.Upcasters.All(existing => existing.TypeName != u.TypeName || existing.Namespace != u.Namespace));
        declaration.Upcasters.AddRange(newUpcasters);

        AppendIdentifierTypeFromMetadata(declaration);

        // Detect if user has defined their own partial factory
        declaration.HasUserDefinedFactoryPartial = DetectUserDefinedFactoryPartial(declaration);

        // Detect if user has defined their own partial repository
        declaration.HasUserDefinedRepositoryPartial = DetectUserDefinedRepositoryPartial(declaration);
    }


    private static bool AreConstructorsEqual(ConstructorDefinition existing, ConstructorDefinition newItem)
    {
        return existing.Parameters.Count == newItem.Parameters.Count &&
               existing.Parameters.Zip(newItem.Parameters, (e, n) => e.Name == n.Name && e.Type == n.Type).All(x => x);
    }

    private static bool ArePropertiesEqual(PropertyDefinition existing, PropertyDefinition newItem)
    {
        return existing.Name == newItem.Name && existing.Type == newItem.Type && existing.Namespace == newItem.Namespace;
    }

    private static bool AreEventsEqual(EventDefinition existing, EventDefinition newItem)
    {
        return existing.EventName == newItem.EventName && existing.TypeName == newItem.TypeName && existing.Namespace == newItem.Namespace;
    }

    private static bool AreCommandsEqual(CommandDefinition existing, CommandDefinition newItem)
    {
        return existing.CommandName == newItem.CommandName &&
               existing.ReturnType.Type == newItem.ReturnType.Type &&
               existing.ReturnType.Namespace == newItem.ReturnType.Namespace;
    }

    private static bool AreStreamActionsEqual(StreamActionDefinition existing, StreamActionDefinition newItem)
    {
        return existing.Type == newItem.Type && existing.Namespace == newItem.Namespace;
    }


    private static List<StreamActionDefinition> GetStreamActions(INamedTypeSymbol parameterTypeSymbol)
    {
        return parameterTypeSymbol.GetAttributes()
            .Where(a => a.AttributeClass is { TypeArguments.Length: > 0 } &&
                       a.AttributeClass.Name != "UseUpcasterAttribute") // Exclude upcaster attributes
            .SelectMany(attribute => attribute.AttributeClass!.TypeArguments)
            .Where(typeArgument => typeArgument.TypeKind != TypeKind.Error)
            .Select(typeArgument => new StreamActionDefinition
            {
                Namespace = RoslynHelper.GetFullNamespace(typeArgument),
                Type = RoslynHelper.GetFullTypeName(typeArgument),
                StreamActionInterfaces = typeArgument.AllInterfaces
                    .Where(i => StreamActionInterfaceNames.Contains(i.Name))
                    .Select(i => i.Name)
                    .ToList(),
                RegistrationType = "Attribute"
            })
            .ToList();
    }


    private AggregateDefinition GetOrCreateAggregateDefinition(List<AggregateDefinition> aggregates)
    {
        var aggregate = aggregates.FirstOrDefault(a =>
            a.IdentifierName == classSymbol?.Name && a.Namespace == RoslynHelper.GetFullNamespace(classSymbol));

        if (aggregate != null)
        {
            return aggregate;
        }

        aggregate = new AggregateDefinition
        {
            IdentifierName = classSymbol!.Name,
            ObjectName = RoslynHelper.GetObjectName(classSymbol),
            Namespace = RoslynHelper.GetFullNamespace(classSymbol),
            FileLocations = classSymbol.DeclaringSyntaxReferences
                .Select(r => r.SyntaxTree.FilePath?.Replace(solutionRootPath, string.Empty) ?? string.Empty)
                .ToList(),
            // Default to string, we will later on replace this with the detected type
            IdentifierType = "String",
            IdentifierTypeNamespace = "System",
            IsPartialClass = RoslynHelper.IsPartial(classDeclaration)
        };
        aggregates.Add(aggregate);

        return aggregate;
    }

    private static void AppendIdentifierTypeFromMetadata(AggregateDefinition aggregate)
    {
        var metadataProperty = aggregate.Properties
            .FirstOrDefault(p => p.Namespace == "ErikLieben.FA.ES" &&
                                 p.Type.StartsWith("ObjectMetadata"));

        var genericType = metadataProperty?.GenericTypes.FirstOrDefault();
        if (genericType == null)
        {
            return;
        }
        aggregate.IdentifierType = genericType.Name;
        aggregate.IdentifierTypeNamespace = genericType.Namespace;
    }

    private bool DetectUserDefinedFactoryPartial(AggregateDefinition aggregate)
    {
        var factoryName = $"{aggregate.IdentifierName}Factory";

        // Search for all types with the factory name in the compilation
        var factoryTypes = compilation.GetSymbolsWithName(factoryName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => t.ContainingNamespace?.ToDisplayString() == aggregate.Namespace);

        foreach (var factoryType in factoryTypes)
        {
            // Check if any of the partial declarations are in non-generated files
            var hasUserDefinedPartial = factoryType.DeclaringSyntaxReferences
                .Any(syntaxRef =>
                {
                    var filePath = syntaxRef.SyntaxTree.FilePath ?? string.Empty;
                    return !filePath.Contains(".Generated.cs", StringComparison.OrdinalIgnoreCase);
                });

            if (hasUserDefinedPartial)
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] Detected user-defined partial factory for {aggregate.IdentifierName}");
                return true;
            }
        }

        return false;
    }

    private bool DetectUserDefinedRepositoryPartial(AggregateDefinition aggregate)
    {
        var repositoryName = $"{aggregate.IdentifierName}Repository";

        // Search for all types with the repository name in the compilation
        var repositoryTypes = compilation.GetSymbolsWithName(repositoryName, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(t => t.ContainingNamespace?.ToDisplayString() == aggregate.Namespace);

        foreach (var repositoryType in repositoryTypes)
        {
            // Check if any of the partial declarations are in non-generated files
            var hasUserDefinedPartial = repositoryType.DeclaringSyntaxReferences
                .Any(syntaxRef =>
                {
                    var filePath = syntaxRef.SyntaxTree.FilePath ?? string.Empty;
                    return !filePath.Contains(".Generated.cs", StringComparison.OrdinalIgnoreCase);
                });

            if (hasUserDefinedPartial)
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] Detected user-defined partial repository for {aggregate.IdentifierName}");
                return true;
            }
        }

        return false;
    }
}
