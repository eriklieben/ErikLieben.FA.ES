using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public class GenerateInheritedAggregateCode
{
    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    public GenerateInheritedAggregateCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var aggregate in project.InheritedAggregates)
            {
                var currentFile = Path.Combine(solutionPath,aggregate.FileLocations.FirstOrDefault() ?? throw new InvalidOperationException());
                if (currentFile is null || currentFile.ToLowerInvariant().Contains(".generated"))
                {
                    continue;
                }
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [green]{aggregate.IdentifierName}[/]");
                var path = solutionPath + aggregate.FileLocations.FirstOrDefault()?.Replace(".cs", ".Generated.cs") ??
                           throw new InvalidOperationException();
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

                var actualAgregate = project.Aggregates.FirstOrDefault(a => a.IdentifierName == aggregate.InheritedIdentifierName && a.Namespace == aggregate.InheritedNamespace);

                try
                {
                    await GenerateAggregate(aggregate, path, config, actualAgregate);
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

    private async Task GenerateAggregate(InheritedAggregateDefinition aggregate, string? path, Config config, AggregateDefinition? actualAgregate)
    {
        var code = new StringBuilder();
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





        var get = "";
        var ctorInput = "";
        var mostParams = aggregate.Constructors.OrderByDescending(c => c.Parameters.Count).First();
        foreach (var param in mostParams.Parameters.Where(param => param.Type != "IEventStream"))
        {
            get += $"var {param.Name} = serviceProvider.GetService(typeof({param.Type})) as {param.Type};\n";
            ctorInput += $", {param.Name}!";
        }

        string codeGetById = "";

        var propertyCode = new StringBuilder();
        foreach (var cmdMethod in aggregate.Commands)
        {

            if (!usings.Contains(cmdMethod.ReturnType.Namespace))
            {
                usings.Add(cmdMethod.ReturnType.Namespace);
            }

            var text = $"{cmdMethod.ReturnType.Type} {cmdMethod.CommandName}(";

            foreach (var param in cmdMethod.Parameters)
            {
                if (!usings.Contains(param.Namespace))
                {
                    usings.Add(param.Namespace);
                }

                if (param.IsGeneric && param.GenericTypes != null)
                {
                    text += param.Type;
                    text += "<";
                    foreach (var generic in param.GenericTypes)
                    {
                        text += generic.Namespace + "." + generic.Name;
                        if (param.GenericTypes.Last() != generic)
                        {
                            text += ",";
                        }
                    }

                    text += ">";

                    text += $" {param.Name}, ";
                }
                else
                {
                    text += $"{param.Type} {param.Name}, ";
                }


            }

            if (text.EndsWith(", "))
            {
                text = text.Remove(text.Length - 2);
            }

            text += ");";
            propertyCode.AppendLine(text);
        }

        
        foreach (var namespaceName in usings.Order())
        {
            code.AppendLine($"using {namespaceName};");
        }
        code.AppendLine("");
        
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
                              {{get}}
                          
                              return new {{aggregate.IdentifierName}}(eventStream{{ctorInput}});
                            }
                          
                            public {{aggregate.IdentifierName}} Create(IObjectDocument document)
                            {
                              ArgumentNullException.ThrowIfNull(document);
                          
                              // get the params required from DI
                              {{get}}
                          
                              var eventStream = eventStreamFactory.Create(document);
                              return new {{aggregate.IdentifierName}}(eventStream{{ctorInput}});
                            }
                            
                            
                             public async Task<{{aggregate.IdentifierName}}> CreateAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString());
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }
                          
                             public async Task<{{aggregate.IdentifierName}}> GetAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString());
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }
                          
                             public async Task<({{aggregate.IdentifierName}}, IObjectDocument)> GetWithDocumentAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString());
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return (obj, document);
                             }
                             
                            public async Task<{{aggregate.IdentifierName}}> GetFirstByDocumentTag(string tag)
                            {
                                var document = await this.objectDocumentFactory.GetFirstByObjectDocumentTag(ObjectName, tag);
                                if (document == null)
                                {
                                    return null!;
                                }
                                var obj = Create(document);
                                await obj.Fold();
                                return obj;
                            }
                            
                            public async Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTag(string tag)
                            {
                                var documents = (await this.objectDocumentFactory.GetByObjectDocumentTag(ObjectName, tag));
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
                                {{propertyCode.ToString()}}
                          }
                          """);

        await File.WriteAllTextAsync(path!, FormatCode(code.ToString()));
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
