using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Generates supporting partial classes and factories for aggregates that inherit from base Aggregate types, based on the solution model.
/// </summary>
public class GenerateInheritedAggregateCode
{
    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateInheritedAggregateCode"/> class.
    /// </summary>
    /// <param name="solution">The parsed solution model that contains projects, aggregates, and inheritance relationships.</param>
    /// <param name="config">The CLI configuration values that influence code generation.</param>
    /// <param name="solutionPath">The absolute path to the solution root used to resolve and write files.</param>
    public GenerateInheritedAggregateCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    /// <summary>
    /// Scans the solution model and generates partial classes and factories for all inherited aggregates into .Generated.cs files.
    /// </summary>
    /// <returns>A task that represents the asynchronous generation operation.</returns>
    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var aggregate in project.InheritedAggregates)
            {
                var currentFile = Path.Combine(solutionPath,aggregate.FileLocations.FirstOrDefault() ?? throw new InvalidOperationException());
                if (currentFile is null || currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [green]{aggregate.IdentifierName}[/]");
                var rel = (aggregate.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized) ??
                           throw new InvalidOperationException();
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

                try
                {
                    await GenerateAggregate(aggregate, path);
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLine($"[red][bold]ERROR:[/] Skipping [underline]{aggregate.IdentifierName}[/] class.[/]");
                    AnsiConsole.MarkupLine($"[red][bold]ERROR:[/] {e.Message}[/]");
                    AnsiConsole.MarkupLine($"[red][bold]ERROR:[/] {e.StackTrace}[/]");
                }

            }
        }
    }

    private static async Task GenerateAggregate(InheritedAggregateDefinition aggregate, string? path)
    {
        var usings = BuildUsings(aggregate);
        var (diCode, ctorParams) = BuildConstructorDependencyCode(aggregate);
        var commandMethodSignatures = BuildCommandMethodSignatures(aggregate, usings);
        var codeContent = GenerateCodeContent(aggregate, usings, diCode, ctorParams, commandMethodSignatures);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        await File.WriteAllTextAsync(path!, FormatCode(codeContent));
    }

    private static List<string> BuildUsings(InheritedAggregateDefinition aggregate)
    {
        var usings = new List<string>
        {
            "System.Text.Json.Serialization",
            "ErikLieben.FA.ES",
            "ErikLieben.FA.ES.Processors",
            "ErikLieben.FA.ES.Aggregates",
            "ErikLieben.FA.ES.Documents",
            "System.Diagnostics.CodeAnalysis"
        };

        if (!string.IsNullOrWhiteSpace(aggregate.ParentInterfaceNamespace))
        {
            usings.Add(aggregate.ParentInterfaceNamespace);
        }

        return usings;
    }

    private static (string DiCode, string CtorParams) BuildConstructorDependencyCode(InheritedAggregateDefinition aggregate)
    {
        var getBuilder = new StringBuilder();
        var ctorInputBuilder = new StringBuilder();
        var orderedConstructors = aggregate.Constructors.OrderByDescending(c => c.Parameters.Count).ToList();
        var mostParams = orderedConstructors[0];

        foreach (var param in mostParams.Parameters.Where(param => param.Type != "IEventStream"))
        {
            getBuilder.Append($"var {param.Name} = serviceProvider.GetService(typeof({param.Type})) as {param.Type};\n");
            ctorInputBuilder.Append($", {param.Name}!");
        }

        return (getBuilder.ToString(), ctorInputBuilder.ToString());
    }

    private static string BuildCommandMethodSignatures(InheritedAggregateDefinition aggregate, List<string> usings)
    {
        var propertyCode = new StringBuilder();

        foreach (var cmdMethod in aggregate.Commands)
        {
            if (!usings.Contains(cmdMethod.ReturnType.Namespace))
            {
                usings.Add(cmdMethod.ReturnType.Namespace);
            }

            var methodSignature = BuildMethodSignature(cmdMethod, usings);
            propertyCode.AppendLine(methodSignature + ");");
        }

        return propertyCode.ToString();
    }

    /// <summary>
    /// Gets the DocumentStore value from the EventStreamBlobSettings attribute, or null if not configured.
    /// </summary>
    private static string? GetDocumentStoreFromAttribute(InheritedAggregateDefinition aggregate)
    {
        return aggregate.EventStreamBlobSettingsAttribute?.DocumentStore;
    }

    /// <summary>
    /// Gets the DocumentTagStore value from the EventStreamBlobSettings attribute, or null if not configured.
    /// </summary>
    private static string? GetDocumentTagStoreFromAttribute(InheritedAggregateDefinition aggregate)
    {
        return aggregate.EventStreamBlobSettingsAttribute?.DocumentTagStore;
    }

    /// <summary>
    /// Generates code that applies attribute-based settings to new documents.
    /// </summary>
    private static string GenerateSettingsApplicationCode(InheritedAggregateDefinition aggregate)
    {
        var assignments = new List<string>();

        AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(aggregate.EventStreamTypeAttribute, assignments);
        AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(aggregate.EventStreamBlobSettingsAttribute, assignments);

        if (assignments.Count == 0)
            return string.Empty;

        return AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);
    }

    private static string BuildMethodSignature(CommandDefinition cmdMethod, List<string> usings)
    {
        var textBuilder = new StringBuilder();
        textBuilder.Append($"{cmdMethod.ReturnType.Type} {cmdMethod.CommandName}(");

        foreach (var param in cmdMethod.Parameters)
        {
            if (!usings.Contains(param.Namespace))
            {
                usings.Add(param.Namespace);
            }

            textBuilder.Append(BuildParameterSignature(param));
            textBuilder.Append(", ");
        }

        var text = textBuilder.ToString();
        if (text.EndsWith(", "))
        {
            text = text.Remove(text.Length - 2);
        }

        return text;
    }

    private static string BuildParameterSignature(CommandParameter param)
    {
        if (param.IsGeneric && param.GenericTypes != null)
        {
            return BuildGenericParameterSignature(param);
        }

        return $"{param.Type} {param.Name}";
    }

    private static string BuildGenericParameterSignature(CommandParameter param)
    {
        var builder = new StringBuilder();
        builder.Append(param.Type);
        builder.Append('<');

        for (int i = 0; i < param.GenericTypes!.Count; i++)
        {
            var generic = param.GenericTypes[i];
            builder.Append(generic.Namespace).Append('.').Append(generic.Name);

            if (i < param.GenericTypes.Count - 1)
            {
                builder.Append(',');
            }
        }

        builder.Append('>');
        builder.Append($" {param.Name}");

        return builder.ToString();
    }

    private static string GenerateCodeContent(
        InheritedAggregateDefinition aggregate,
        List<string> usings,
        string diCode,
        string ctorParams,
        string commandMethodSignatures)
    {
        var code = new StringBuilder();

        foreach (var namespaceName in usings.Order())
        {
            code.AppendLine($"using {namespaceName};");
        }
        code.AppendLine("");
        code.AppendLine("#nullable enable");
        code.AppendLine("");

        string codeGetById = "";

        code.AppendLine($$"""

                          namespace {{aggregate.Namespace}};

                          //<auto-generated />
                          public interface I{{aggregate.IdentifierName}}Factory : IAggregateFactory<{{aggregate.IdentifierName}}, {{aggregate.IdentifierType}}>
                          {
                          }

                          //<auto-generated />
                          public class {{aggregate.IdentifierName}}Factory : I{{aggregate.IdentifierName}}Factory
                          {
                            private readonly IEventStreamFactory eventStreamFactory;
                            private readonly IObjectDocumentFactory objectDocumentFactory;
                            private readonly IServiceProvider serviceProvider;

                            public static string ObjectName => "{{aggregate.ObjectName}}";

                            public string GetObjectName()
                            {
                                return ObjectName;
                            }

                            public {{aggregate.IdentifierName}}Factory(
                              IServiceProvider serviceProvider,
                              IEventStreamFactory eventStreamFactory,
                              IObjectDocumentFactory objectDocumentFactory)
                            {
                              ArgumentNullException.ThrowIfNull(serviceProvider);
                              ArgumentNullException.ThrowIfNull(eventStreamFactory);
                              ArgumentNullException.ThrowIfNull(objectDocumentFactory);

                              this.serviceProvider = serviceProvider;
                              this.eventStreamFactory = eventStreamFactory;
                              this.objectDocumentFactory = objectDocumentFactory;
                            }

                            public {{aggregate.IdentifierName}} Create(IEventStream eventStream)
                            {
                              ArgumentNullException.ThrowIfNull(eventStream);

                              // get the params required from DI
                              {{diCode}}

                              return new {{aggregate.IdentifierName}}(eventStream{{ctorParams}});
                            }

                            public {{aggregate.IdentifierName}} Create(IObjectDocument document)
                            {
                              ArgumentNullException.ThrowIfNull(document);

                              // get the params required from DI
                              {{diCode}}

                              var eventStream = eventStreamFactory.Create(document);
                              return new {{aggregate.IdentifierName}}(eventStream{{ctorParams}});
                            }


                             public async Task<{{aggregate.IdentifierName}}> CreateAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                             {{GenerateSettingsApplicationCode(aggregate)}}
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }

                             public async Task<{{aggregate.IdentifierName}}> GetAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentStoreFromAttribute(aggregate)}\\\"" : "")}});
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }

                             public async Task<({{aggregate.IdentifierName}}, IObjectDocument)> GetWithDocumentAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentStoreFromAttribute(aggregate)}\\\"" : "")}});
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return (obj, document);
                             }

                            public async Task<{{aggregate.IdentifierName}}?> GetFirstByDocumentTag(string tag)
                            {
                                var document = await this.objectDocumentFactory.GetFirstByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentTagStoreFromAttribute(aggregate)}\\\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentStoreFromAttribute(aggregate)}\\\"" : "")}});
                                if (document == null)
                                {
                                    return null;
                                }
                                var obj = Create(document);
                                await obj.Fold();
                                return obj;
                            }

                            public async Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTag(string tag)
                            {
                                var documents = (await this.objectDocumentFactory.GetByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentTagStoreFromAttribute(aggregate)}\\\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \\\"{GetDocumentStoreFromAttribute(aggregate)}\\\"" : "")}}));
                                var items = new List<{{aggregate.IdentifierName}}>();
                                foreach (var document in documents)
                                {
                                    var obj = Create(document);
                                    await obj.Fold();
                                }
                                return items;
                            }

                          {{codeGetById}}

                          }

                          public interface I{{aggregate.IdentifierName}} : {{aggregate.ParentInterface}} {
                                {{commandMethodSignatures}}
                          }
                          """);

        return code.ToString();
    }

    private static string FormatCode(string code, CancellationToken cancelToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancelToken);
        var syntaxNode = syntaxTree.GetRoot(cancelToken);

        using var workspace = new AdhocWorkspace();
        var options = workspace.Options
            .WithChangedOption(FormattingOptions.SmartIndent, LanguageNames.CSharp,
                FormattingOptions.IndentStyle.Smart);

        var formattedNode = Formatter.Format(syntaxNode, workspace, options, cancellationToken: cancelToken);
        return formattedNode.ToFullString();
    }

}
