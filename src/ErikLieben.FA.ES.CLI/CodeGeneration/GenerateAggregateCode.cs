using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public class GenerateAggregateCode
{
    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    public GenerateAggregateCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var aggregate in project.Aggregates)
            {
                var currentFile = Path.Combine(solutionPath,aggregate.FileLocations.FirstOrDefault() ?? throw new InvalidOperationException());
                if (currentFile.Contains(".generated"))
                {
                    continue;
                }
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [green]{aggregate.IdentifierName}[/]");
                var path = solutionPath + aggregate.FileLocations.FirstOrDefault()?.Replace(".cs", ".Generated.cs") ??
                           throw new InvalidOperationException();
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

                await GenerateAggregate(aggregate, path, config);
            }
        }
    }

    private async Task GenerateAggregate(AggregateDefinition aggregate, string? path, Config config)
    {
        if (!aggregate.IsPartialClass)
        {
            AnsiConsole.MarkupLine($"[red][bold]ERROR:[/] Skipping [underline]{aggregate.IdentifierName}[/] class; it needs to be partial to support generated code, make it partial please.[/]");
            return;
        }


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

        foreach (var property in aggregate.Properties)
        {
            if (!usings.Contains(property.Namespace))
            {
                usings.Add(property.Namespace);
            }
        }

        var postWhenCode = new StringBuilder();
        if (aggregate.PostWhen != null)
        {
            postWhenCode.Append("PostWhen(");
            foreach (var param in aggregate.PostWhen.Parameters)
            {
                if (!usings.Contains(param.Namespace))
                {
                    usings.Add(param.Namespace);
                };

                switch (param.Type)
                {
                    case "IObjectDocument":
                        postWhenCode.Append($"Stream.Document, ");
                        break;
                    case "IEvent":
                        postWhenCode.Append($"@event, ");
                        break;
                }
            }
            var fullPostWhenCode = postWhenCode.ToString();
            if (fullPostWhenCode.EndsWith(", "))
            {
                fullPostWhenCode = fullPostWhenCode.Remove(fullPostWhenCode.Length - 2);
                postWhenCode.Clear();
                postWhenCode.Append(fullPostWhenCode);
            }
            postWhenCode.Append(");");
            postWhenCode.AppendLine();
        }

        var foldCode = new StringBuilder();
        foreach (var @event in aggregate.Events)
        {
            if (@event.ActivationType == "When")
            {
                // When
                if (!usings.Contains(@event.Namespace))
                {
                    usings.Add(@event.Namespace);
                }

                if (@event.Parameters.Count > 1)
                {
                    foldCode.Append($$$"""
                                           case "{{{@event.EventName}}}":
                                                When(JsonEvent.To(@event, {{{@event.TypeName}}}JsonSerializerContext.Default.{{{@event.TypeName}}}),
                                           """);
                    foreach (var p in @event.Parameters.Skip(1))
                    {
                        switch (p.Type)
                        {
                            case "IObjectDocument":
                                foldCode.Append($"Stream.Document");
                                break;
                            case "IEvent":
                                foldCode.Append($"@event");
                                break;
                        }

                        if (p.Type != @event.Parameters.Last().Type)
                        {
                            foldCode.AppendLine(",");
                        }
                    }
                    foldCode.Append(");");
                    foldCode.AppendLine();
                    foldCode.AppendLine("break;");
                }
                else
                {

                    foldCode.AppendLine($$"""
                                          case "{{@event.EventName}}":
                                              When(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
                                          break;
                                          """);
                }
            }
        }




        var serializableCode = new StringBuilder();

        var propertyTypes = new List<string>();

        var newJsonSerializableCode = new StringBuilder();

        foreach (var usedEvent in aggregate.Events)
        {
            serializableCode.AppendLine($"[JsonSerializable(typeof({usedEvent.TypeName}))]");
            newJsonSerializableCode.AppendLine("// <auto-generated />");

            var subTypes = new List<string>();

            // HACK:
            subTypes.Add("System.String");
            subTypes.Add("System.Guid");

            foreach (var property in usedEvent.Properties)
            {
                foreach (var subType in property.SubTypes)
                {
                    var fullSubTypeDef = subType.Namespace + "." + subType.Name;
                    if (!subTypes.Contains(fullSubTypeDef))
                    {
                        subTypes.Add(fullSubTypeDef);
                    }
                }

                var fullTypeDef = property.Namespace + "." + property.Type;

                if (property.IsGeneric)
                {
                    fullTypeDef += "<";
                    foreach (var generic in property.GenericTypes)
                    {
                        fullTypeDef += generic.Namespace + "." + generic.Name;
                        if (property.GenericTypes.Last() != generic)
                        {
                            fullTypeDef += ",";
                        }
                    }
                    fullTypeDef += ">";
                }

                if (!propertyTypes.Contains(fullTypeDef))
                {
                    propertyTypes.Add(fullTypeDef);
                }

                if (!subTypes.Contains(fullTypeDef))
                {
                    subTypes.Add(fullTypeDef);
                }
                //newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof({fullTypeDef}))]");
            }

            foreach (var subtype in subTypes.Order())
            {
                newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof({subtype}))]");
            }



            newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof({@usedEvent.TypeName}))]");
            newJsonSerializableCode.AppendLine(
                "internal partial class " + usedEvent.TypeName + "JsonSerializerContext : JsonSerializerContext { }");
            newJsonSerializableCode.AppendLine("");
        }

        serializableCode.AppendLine($"[JsonSerializable(typeof({aggregate.IdentifierName}Snapshot))]");
        serializableCode.AppendLine($"[JsonSerializable(typeof({aggregate.IdentifierName}))]");
        if (aggregate.IdentifierType != "string")
        {
            if (!usings.Contains(aggregate.IdentifierTypeNamespace))
            {
                usings.Add(aggregate.IdentifierTypeNamespace);
            }
            serializableCode.AppendLine($"[JsonSerializable(typeof({aggregate.IdentifierType}))]");
        }

        var propertyCode = new StringBuilder();
        var propertySnapshotCode = new StringBuilder();
        var propertySubTypes = new List<string>();
        foreach (var property in aggregate.Properties)
        {
            var type = property.Type;
            if (property.IsGeneric)
            {
                type += "<";
                foreach (var generic in property.GenericTypes)
                {
                    type += generic.Namespace + "." + generic.Name;
                    if (property.GenericTypes.Last() != generic)
                    {
                        type += ",";
                    }
                }
                type += ">";
            }

            propertyCode.AppendLine($"public {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get;}}");
            propertySnapshotCode.AppendLine($"public required {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get; init; }}");

            //

            foreach (var subtype in property.SubTypes)
            {
                if (!propertySubTypes.Contains(subtype.Namespace + "." + subtype.Name))
                {
                    propertySubTypes.Add(subtype.Namespace + "." + subtype.Name);
                }
            }
        }

        //
            foreach (var subType in propertySubTypes.Order())
            {
                serializableCode.AppendLine($"[JsonSerializable(typeof({subType}))]");
            }



        // TODO: These probably need to be set to something
        var get = "";
        var ctorInput = "";
        var mostParams = aggregate.Constructors.OrderByDescending(c => c.Parameters.Count).First();
        foreach (var param in mostParams.Parameters.Where(param => param.Type != "IEventStream"))
        {
            get += $"var {param.Name} = serviceProvider.GetService(typeof({param.Type})) as {param.Type};\n";
            ctorInput += $", {param.Name}!";
        }

        string codeGetById = "";

        var setupCode = new StringBuilder();
        foreach (var usedEvent in aggregate.Events)
        {
//                  setupCode.AppendLine($$"""
//                      Stream.RegisterEvent<{{usedEvent.TypeName}}>("{{usedEvent.EventName}}");
//                  """);
                 setupCode.AppendLine($$"""
                     Stream.RegisterEvent<{{usedEvent.TypeName}}>(
                         "{{usedEvent.EventName}}",
                         {{usedEvent.TypeName}}JsonSerializerContext.Default.{{usedEvent.TypeName}});
                 """);
        }

        setupCode.AppendLine($$"""
             Stream.SetSnapShotType({{aggregate.IdentifierName}}JsonSerializerContext.Default.{{aggregate.IdentifierName}}Snapshot);
             Stream.SetAggregateType({{aggregate.IdentifierName}}JsonSerializerContext.Default.{{aggregate.IdentifierName}});
        """);

        foreach (var namespaceName in usings.Order())
        {
            code.AppendLine($"using {namespaceName};");
        }
        code.AppendLine("");
        code.AppendLine($$"""
                          namespace {{aggregate.Namespace}};

                          // <auto-generated />
                          public partial class {{aggregate.IdentifierName}} : Aggregate, IBase, I{{aggregate.IdentifierName}} {

                              public override void Fold(IEvent @event)
                              {
                                  switch (@event.EventType)
                                  {
                                      {{foldCode}}
                                  }
                                  
                                  {{postWhenCode}}   
                              
                              }
                              
                              protected override void GeneratedSetup()
                              {
                                {{setupCode}}
                              }

                              public override void ProcessSnapshot(object snapshot)
                              {
                                  throw new NotImplementedException();
                              }
                              
                          }


                          #nullable enable

                          // <auto-generated />
                          public interface I{{aggregate.IdentifierName}} {
                                {{propertyCode}}
                          }

                          // <auto-generated />
                          public record {{aggregate.IdentifierName}}Snapshot : I{{aggregate.IdentifierName}} {
                                {{propertySnapshotCode}}
                          }

                          #nullable restore


                          {{serializableCode}}
                          // <auto-generated />
                          internal partial class {{aggregate.IdentifierName}}JsonSerializerContext : JsonSerializerContext
                          {
                          }

                          //<auto-generated />
                          public partial interface I{{aggregate.IdentifierName}}Factory : IAggregateFactory<{{aggregate.IdentifierName}}, {{aggregate.IdentifierType}}>
                          {
                          }

                          //<auto-generated />
                          public partial class {{aggregate.IdentifierName}}Factory : I{{aggregate.IdentifierName}}Factory
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
                             
                             protected async Task<{{aggregate.IdentifierName}}> CreateAsync<T>({{aggregate.IdentifierType}} id, T firstEvent) where T : class
                             {
                                var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString());
                                var eventStream = eventStreamFactory.Create(document);
                                var obj = new {{aggregate.IdentifierName}}(eventStream);
                                await eventStream.Session(context => context.Append(firstEvent));
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
