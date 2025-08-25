using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
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
        // declaration.Constructors.AddRange(ConstructorHelper.GetConstructors(classSymbol));
        // declaration.Properties.AddRange(PropertyHelper.GetPublicGetterProperties(typeSymbol));
        // declaration.Events.AddRange(WhenMethodHelper.GetEventDefinitions(typeSymbol, compilation, roslyn));
        // declaration.Commands.AddRange(CommandHelper.GetCommandMethods(typeSymbol, roslyn));
        // declaration.PostWhen =  PostWhenHelper.GetPostWhenMethod(typeSymbol);
        // declaration.StreamActions.AddRange(GetStreamActions(classSymbol));
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
        if (declaration.PostWhen == null)
        {
            declaration.PostWhen = PostWhenHelper.GetPostWhenMethod(typeSymbol);
        }
        var newStreamActions = GetStreamActions(classSymbol)
            .Where(sa => declaration.StreamActions.All(existing => !AreStreamActionsEqual(existing, sa)));
        declaration.StreamActions.AddRange(newStreamActions);
        
        AppendIdentifierTypeFromMetadata(declaration);
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

    private static readonly List<string> StreamInterfaces =
    [
        "IAsyncPostCommitAction",
        "IPostAppendAction",
        "IPostReadAction",
        "IPreAppendAction",
        "IPreReadAction"
    ];

    private static List<StreamActionDefinition> GetStreamActions(INamedTypeSymbol parameterTypeSymbol)
    {
        var streamActions = new List<StreamActionDefinition>();

        var attributes = parameterTypeSymbol.GetAttributes();
        foreach (var attribute in attributes)
        {
            var attributeClassType = attribute.AttributeClass;
            if (attributeClassType is not { TypeArguments.Length: > 0 })
            {
                continue;
            }

            foreach (var typeArgument in attributeClassType.TypeArguments)
            {
                var typeArgumentType = typeArgument.TypeKind;
                if (typeArgumentType == TypeKind.Error)
                {
                    continue;
                }

                streamActions.Add(new StreamActionDefinition
                {
                    Namespace = RoslynHelper.GetFullNamespace(typeArgument),
                    Type = RoslynHelper.GetFullTypeName(typeArgument),
                    StreamActionInterfaces = typeArgument.AllInterfaces
                        .Where(i => StreamInterfaces.Contains(i.Name))
                        .Select(i => i.Name)
                        .ToList()
                });
            }
        }

        return streamActions;
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
}
