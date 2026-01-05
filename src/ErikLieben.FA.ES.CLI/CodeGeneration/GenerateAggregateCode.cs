#pragma warning disable S2589 // Boolean expressions should not be gratuitous - defensive length checks after StringBuilder modification
#pragma warning disable S3267 // Loops should be simplified - explicit loops improve debuggability
#pragma warning disable S1192 // String literals should not be duplicated - code generation templates

using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
                var rel = (aggregate.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized);
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

                await GenerateAggregate(aggregate, path, solution.Generator?.Version ?? "1.0.0");
            }
        }
    }

    private static async Task GenerateAggregate(AggregateDefinition aggregate, string? path, string version)
    {
        if (!aggregate.IsPartialClass)
        {
            AnsiConsole.MarkupLine($"[red][bold]ERROR:[/] Skipping [underline]{aggregate.IdentifierName}[/] class; it needs to be partial to support generated code, make it partial please.[/]");
            return;
        }

        var usings = BuildUsings(aggregate);
        var postWhenCode = GeneratePostWhenCode(aggregate, usings);
        var foldCode = GenerateFoldCode(aggregate, usings);
        var serializableCode = GenerateJsonSerializableCode(aggregate, usings);
        var (propertyCode, propertySnapshotCode) = GeneratePropertyCode(aggregate, serializableCode);

        // Remove trailing newline from serializableCode to avoid blank line before JsonSourceGenerationOptions
        if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\n')
        {
            serializableCode.Length--;
            if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\r')
            {
                serializableCode.Length--;
            }
        }

        var (get, ctorInput) = GenerateConstructorParameters(aggregate);
        var setupCode = GenerateSetupCode(aggregate);
        var code = AssembleAggregateCode(aggregate, usings, postWhenCode, foldCode, serializableCode, propertyCode, propertySnapshotCode, get, ctorInput, setupCode, version);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        var projectDir = CodeFormattingHelper.FindProjectDirectory(path!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString(), projectDir));
    }

    internal static List<string> BuildUsings(AggregateDefinition aggregate)
    {
        var usings = new List<string>
        {
            "System.Collections.Generic",
            "System.Text.Json.Serialization",
            "System.Threading",
            "System.Threading.Tasks",
            "ErikLieben.FA.ES",
            "ErikLieben.FA.ES.Processors",
            "ErikLieben.FA.ES.Aggregates",
            "ErikLieben.FA.ES.Documents",
            "System.Diagnostics.CodeAnalysis",
            "System.CodeDom.Compiler"
        };

        usings.AddRange(aggregate.Properties
            .Where(p => !usings.Contains(p.Namespace))
            .Select(p => p.Namespace));

        // Add identifier type namespace if it's not a built-in type
        if (aggregate.IdentifierType != "string" &&
            !string.IsNullOrWhiteSpace(aggregate.IdentifierTypeNamespace) &&
            !usings.Contains(aggregate.IdentifierTypeNamespace))
        {
            usings.Add(aggregate.IdentifierTypeNamespace);
        }

        // Add upcaster namespaces
        if (aggregate.Upcasters != null)
        {
            usings.AddRange(aggregate.Upcasters
                .Where(u => !string.IsNullOrWhiteSpace(u.Namespace) && !usings.Contains(u.Namespace))
                .Select(u => u.Namespace));
        }

        return usings;
    }

    internal static StringBuilder GeneratePostWhenCode(AggregateDefinition aggregate, List<string> usings)
    {
        var postWhenCode = new StringBuilder();
        if (aggregate.PostWhen == null)
        {
            return postWhenCode;
        }

        postWhenCode.Append("PostWhen(");
        usings.AddRange(aggregate.PostWhen.Parameters
            .Where(p => !usings.Contains(p.Namespace))
            .Select(p => p.Namespace));

        foreach (var param in aggregate.PostWhen.Parameters)
        {
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
        return postWhenCode;
    }

    internal static StringBuilder GenerateFoldCode(AggregateDefinition aggregate, List<string> usings)
    {
        var foldCode = new StringBuilder();

        // Group events by EventName to handle multiple schema versions
        var eventsByName = aggregate.Events
            .Where(e => e.ActivationType != "Command") // Skip events from Command methods
            .GroupBy(e => e.EventName)
            .ToList();

        foreach (var eventGroup in eventsByName)
        {
            var events = eventGroup.OrderBy(e => e.SchemaVersion).ToList();

            foreach (var @event in events)
            {
                if (!usings.Contains(@event.Namespace))
                {
                    usings.Add(@event.Namespace);
                }
            }

            if (events.Count == 1)
            {
                // Single version - generate simple case
                var @event = events[0];
                if (@event.Parameters.Count == 0)
                {
                    GenerateFoldCodeWithNoParameters(@event, foldCode);
                }
                else if (@event.Parameters.Count > 1)
                {
                    GenerateFoldCodeWithMultipleParameters(@event, foldCode);
                }
                else
                {
                    GenerateFoldCodeWithSingleParameter(@event, foldCode);
                }
            }
            else
            {
                // Multiple versions - generate schema version dispatch
                GenerateFoldCodeWithSchemaVersionDispatch(events, foldCode);
            }
        }
        return foldCode;
    }

    internal static void GenerateFoldCodeWithSchemaVersionDispatch(List<EventDefinition> events, StringBuilder foldCode)
    {
        var eventName = events[0].EventName;
        foldCode.AppendLine($$"""
                              case "{{eventName}}":
                          """);

        for (int i = 0; i < events.Count; i++)
        {
            var @event = events[i];
            var isLast = i == events.Count - 1;
            var condition = GetSchemaVersionCondition(i, isLast, @event.SchemaVersion);

            if (@event.Parameters.Count == 0)
            {
                foldCode.AppendLine($$"""
                                          {{condition}}
                                              {{@event.ActivationType}}();
                                      """);
            }
            else if (@event.Parameters.Count > 1)
            {
                var paramBuilder = new StringBuilder();
                foreach (var p in @event.Parameters.Skip(1))
                {
                    switch (p.Type)
                    {
                        case "IObjectDocument":
                            paramBuilder.Append($", Stream.Document");
                            break;
                        case "IEvent":
                            paramBuilder.Append($", @event");
                            break;
                    }
                }
                foldCode.AppendLine($$"""
                                          {{condition}}
                                              {{@event.ActivationType}}(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}){{paramBuilder}});
                                      """);
            }
            else
            {
                foldCode.AppendLine($$"""
                                          {{condition}}
                                              {{@event.ActivationType}}(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
                                      """);
            }
        }

        foldCode.AppendLine("    break;");
    }

    internal static void GenerateFoldCodeWithNoParameters(EventDefinition @event, StringBuilder foldCode)
    {
        foldCode.AppendLine($$"""
                              case "{{@event.EventName}}":
                                  {{@event.ActivationType}}();
                              break;
                              """);
    }

    internal static void GenerateFoldCodeWithMultipleParameters(EventDefinition @event, StringBuilder foldCode)
    {
        foldCode.Append($$$"""
                               case "{{{@event.EventName}}}":
                                    {{{@event.ActivationType}}}(JsonEvent.To(@event, {{{@event.TypeName}}}JsonSerializerContext.Default.{{{@event.TypeName}}}),
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

            if (p.Type != @event.Parameters[^1].Type)
            {
                foldCode.AppendLine(",");
            }
        }

        foldCode.Append(");");
        foldCode.AppendLine();
        foldCode.AppendLine("break;");
    }

    internal static void GenerateFoldCodeWithSingleParameter(EventDefinition @event, StringBuilder foldCode)
    {
        foldCode.AppendLine($$"""
                              case "{{@event.EventName}}":
                                  {{@event.ActivationType}}(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
                              break;
                              """);
    }

    private static string GetSchemaVersionCondition(int index, bool isLast, int schemaVersion)
    {
        if (isLast)
        {
            return "else";
        }

        if (index == 0)
        {
            return $"if (@event.SchemaVersion == {schemaVersion})";
        }

        return $"else if (@event.SchemaVersion == {schemaVersion})";
    }

    internal static StringBuilder GenerateJsonSerializableCode(AggregateDefinition aggregate, List<string> usings)
    {
        var serializableCode = new StringBuilder();

        foreach (var usedEvent in aggregate.Events)
        {
            serializableCode.AppendLine($"[JsonSerializable(typeof({usedEvent.TypeName}))]");
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

        return serializableCode;
    }

    internal static (StringBuilder propertyCode, StringBuilder propertySnapshotCode) GeneratePropertyCode(
        AggregateDefinition aggregate, StringBuilder serializableCode)
    {
        var propertyCode = new StringBuilder();
        var propertySnapshotCode = new StringBuilder();
        var propertySubTypes = new List<string>();

        foreach (var subType in aggregate.Properties.SelectMany(p => p.SubTypes)
            .Where(st => !propertySubTypes.Contains(st.Namespace + "." + st.Name)))
        {
            propertySubTypes.Add(subType.Namespace + "." + subType.Name);
        }

        foreach (var property in aggregate.Properties)
        {
            // Skip ObjectName - it's a static member on the aggregate class, not an instance property
            if (property.Name == "ObjectName")
            {
                continue;
            }

            var type = BuildPropertyType(property);
            propertyCode.AppendLine($"public {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get;}}");
            propertySnapshotCode.AppendLine($"public required {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get; init; }}");
        }

        foreach (var subType in propertySubTypes.Order())
        {
            serializableCode.AppendLine($"[JsonSerializable(typeof({subType}))]");
        }

        return (propertyCode, propertySnapshotCode);
    }

    internal static string BuildPropertyType(PropertyDefinition property)
    {
        var typeBuilder = new StringBuilder(property.Type);

        // Check if property.Type already includes generic parameters (e.g., "StronglyTypedId<Guid>")
        // If so, don't append them again to avoid duplication like "StronglyTypedId<Guid>< Guid >"
        var alreadyHasGenerics = property.Type.Contains('<') && property.Type.Contains('>');

        if (!property.IsGeneric || alreadyHasGenerics)
        {
            return typeBuilder.ToString();
        }

        typeBuilder.Append('<');
        foreach (var generic in property.GenericTypes)
        {
            typeBuilder.Append(generic.Namespace).Append('.').Append(generic.Name);
            if (property.GenericTypes[^1] != generic)
            {
                typeBuilder.Append(',');
            }
        }
        typeBuilder.Append('>');

        return typeBuilder.ToString();
    }

    internal static (string get, string ctorInput) GenerateConstructorParameters(AggregateDefinition aggregate)
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

    internal static StringBuilder GenerateSetupCode(AggregateDefinition aggregate)
    {
        var setupCode = new StringBuilder();
        foreach (var usedEvent in aggregate.Events)
        {
            if (usedEvent.SchemaVersion == 1)
            {
                setupCode.AppendLine($$"""
                     Stream.RegisterEvent<{{usedEvent.TypeName}}>(
                         "{{usedEvent.EventName}}",
                         {{usedEvent.TypeName}}JsonSerializerContext.Default.{{usedEvent.TypeName}});
                 """);
            }
            else
            {
                setupCode.AppendLine($$"""
                     Stream.RegisterEvent<{{usedEvent.TypeName}}>(
                         "{{usedEvent.EventName}}",
                         {{usedEvent.SchemaVersion}},
                         {{usedEvent.TypeName}}JsonSerializerContext.Default.{{usedEvent.TypeName}});
                 """);
            }
        }

        // Register upcasters
        foreach (var upcaster in aggregate.Upcasters)
        {
            setupCode.AppendLine($$"""
                 Stream.RegisterUpcast(new {{upcaster.TypeName}}());
             """);
        }

        setupCode.AppendLine($$"""
             Stream.SetSnapShotType({{aggregate.IdentifierName}}JsonSerializerContext.Default.{{aggregate.IdentifierName}}Snapshot);
             Stream.SetAggregateType({{aggregate.IdentifierName}}JsonSerializerContext.Default.{{aggregate.IdentifierName}});

             // Freeze the EventTypeRegistry for optimized lookups (~50% faster)
             Stream.EventTypeRegistry.Freeze();
        """);

        return setupCode;
    }

    /// <summary>
    /// Gets the DocumentStore value from the EventStreamBlobSettings attribute, or null if not configured.
    /// </summary>
    internal static string? GetDocumentStoreFromAttribute(AggregateDefinition aggregate)
    {
        return aggregate.EventStreamBlobSettingsAttribute?.DocumentStore;
    }

    /// <summary>
    /// Gets the DocumentType value from the EventStreamType attribute, or null if not configured.
    /// </summary>
    internal static string? GetDocumentTypeFromAttribute(AggregateDefinition aggregate)
    {
        return aggregate.EventStreamTypeAttribute?.DocumentType;
    }

    /// <summary>
    /// Gets the DocumentTagStore value from the EventStreamBlobSettings attribute, or null if not configured.
    /// </summary>
    internal static string? GetDocumentTagStoreFromAttribute(AggregateDefinition aggregate)
    {
        return aggregate.EventStreamBlobSettingsAttribute?.DocumentTagStore;
    }

    /// <summary>
    /// Generates code that applies attribute-based settings to new documents.
    /// </summary>
    internal static string GenerateSettingsApplicationCode(AggregateDefinition aggregate)
    {
        var assignments = new List<string>();

        AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(aggregate.EventStreamTypeAttribute, assignments);
        AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(aggregate.EventStreamBlobSettingsAttribute, assignments);

        if (assignments.Count == 0)
            return string.Empty;

        return AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);
    }

    internal static StringBuilder AssembleAggregateCode(
        AggregateDefinition aggregate,
        List<string> usings,
        StringBuilder postWhenCode,
        StringBuilder foldCode,
        StringBuilder serializableCode,
        StringBuilder propertyCode,
        StringBuilder propertySnapshotCode,
        string get,
        string ctorInput,
        StringBuilder setupCode,
        string version)
    {
        var code = new StringBuilder();
        string codeGetById = "";

        foreach (var namespaceName in usings.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Order())
        {
            code.AppendLine($"using {namespaceName};");
        }

        code.AppendLine("");
        code.AppendLine("#nullable enable");
        code.AppendLine("");

        // Generate Create method (required by ITestableAggregate)
        var testableAggregateMethods = $$"""

                              /// <summary>
                              /// Creates a new instance of the aggregate from an event stream (AOT-friendly factory).
                              /// </summary>
                              /// <param name="stream">The event stream for the aggregate.</param>
                              /// <returns>A new instance of the aggregate.</returns>
                              public static {{aggregate.IdentifierName}} Create(IEventStream stream) => new {{aggregate.IdentifierName}}(stream);
                """;

        code.AppendLine($$"""
                          namespace {{aggregate.Namespace}};

                          // <auto-generated />
                          /// <summary>
                          /// {{aggregate.IdentifierName}} aggregate root implementing event sourcing patterns.
                          /// </summary>
                          [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                          [ExcludeFromCodeCoverage]
                          public partial class {{aggregate.IdentifierName}} : Aggregate, IBase, I{{aggregate.IdentifierName}} {

                              /// <summary>
                              /// Gets the logical object name for this aggregate type (AOT-friendly static member).
                              /// </summary>
                              public static string ObjectName => "{{aggregate.ObjectName}}";
                          {{testableAggregateMethods}}

                              /// <summary>
                              /// Applies an event to the aggregate state by dispatching to the appropriate When method.
                              /// </summary>
                              /// <param name="event">The event to apply to the aggregate.</param>
                              public override void Fold(IEvent @event)
                              {
                                  switch (@event.EventType)
                                  {
                                      {{foldCode}}
                                  }

                                  {{postWhenCode}}

                              }

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              protected override void GeneratedSetup()
                              {
                                {{setupCode}}
                              }

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              public override void ProcessSnapshot(object snapshot)
                              {
                                  throw new NotImplementedException();
                              }

                          }

                          // <auto-generated />
                          /// <summary>
                          /// Interface defining the public state properties of {{aggregate.IdentifierName}}.
                          /// </summary>
                          public interface I{{aggregate.IdentifierName}} {
                                {{propertyCode}}
                          }

                          // <auto-generated />
                          /// <summary>
                          /// Snapshot record for persisting {{aggregate.IdentifierName}} aggregate state.
                          /// </summary>
                          [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                          [ExcludeFromCodeCoverage]
                          public record {{aggregate.IdentifierName}}Snapshot : I{{aggregate.IdentifierName}} {
                                {{propertySnapshotCode}}
                          }

                          {{serializableCode}}
                          // <auto-generated />
                          /// <summary>
                          /// JSON serializer context for {{aggregate.IdentifierName}} types.
                          /// </summary>
                          internal partial class {{aggregate.IdentifierName}}JsonSerializerContext : JsonSerializerContext
                          {
                          }

                          //<auto-generated />
                          /// <summary>
                          /// Factory interface for creating {{aggregate.IdentifierName}} aggregate instances.
                          /// </summary>
                          public partial interface I{{aggregate.IdentifierName}}Factory : IAggregateFactory<{{aggregate.IdentifierName}}, {{aggregate.IdentifierType}}>
                          {
                          }

                          //<auto-generated />
                          /// <summary>
                          /// Factory for creating and loading {{aggregate.IdentifierName}} aggregate instances from documents and event streams.
                          /// </summary>
                          [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                          [ExcludeFromCodeCoverage]
                          public partial class {{aggregate.IdentifierName}}Factory : I{{aggregate.IdentifierName}}Factory
                          {
                            private readonly IEventStreamFactory eventStreamFactory;
                            private readonly IObjectDocumentFactory objectDocumentFactory;
                            private readonly IServiceProvider serviceProvider;

                            /// <summary>
                            /// Gets the object name used for document storage.
                            /// </summary>
                            public static string ObjectName => "{{aggregate.ObjectName}}";

                            /// <summary>
                            /// Gets the object name used for document storage.
                            /// </summary>
                            /// <returns>The object name.</returns>
                            public string GetObjectName()
                            {
                                return ObjectName;
                            }

                            /// <summary>
                            /// Initializes a new instance of the {{aggregate.IdentifierName}}Factory class.
                            /// </summary>
                            /// <param name="serviceProvider">Service provider for dependency injection.</param>
                            /// <param name="eventStreamFactory">Factory for creating event streams.</param>
                            /// <param name="objectDocumentFactory">Factory for creating and managing object documents.</param>
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

                            /// <summary>
                            /// Creates a {{aggregate.IdentifierName}} instance from an event stream.
                            /// </summary>
                            /// <param name="eventStream">The event stream to create the aggregate from.</param>
                            /// <returns>A new {{aggregate.IdentifierName}} instance.</returns>
                            public {{aggregate.IdentifierName}} Create(IEventStream eventStream)
                            {
                              ArgumentNullException.ThrowIfNull(eventStream);

                              // get the params required from DI
                              {{get}}

                              return new {{aggregate.IdentifierName}}(eventStream{{ctorInput}});
                            }

                            /// <summary>
                            /// Creates a {{aggregate.IdentifierName}} instance from an object document.
                            /// </summary>
                            /// <param name="document">The object document to create the aggregate from.</param>
                            /// <returns>A new {{aggregate.IdentifierName}} instance.</returns>
                            public {{aggregate.IdentifierName}} Create(IObjectDocument document)
                            {
                              ArgumentNullException.ThrowIfNull(document);

                              // get the params required from DI
                              {{get}}

                              var eventStream = eventStreamFactory.Create(document);
                              return new {{aggregate.IdentifierName}}(eventStream{{ctorInput}});
                            }


                             {{(aggregate.HasUserDefinedFactoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                             /// <summary>
                             /// Creates a new {{aggregate.IdentifierName}} aggregate with the specified identifier.
                             /// </summary>
                             /// <param name="id">The identifier for the new aggregate.</param>
                             /// <returns>A new {{aggregate.IdentifierName}} instance.</returns>
                             public async Task<{{aggregate.IdentifierName}}> CreateAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString(), {{(GetDocumentStoreFromAttribute(aggregate) != null ? $"\"{GetDocumentStoreFromAttribute(aggregate)}\"" : "null")}}, {{(GetDocumentTypeFromAttribute(aggregate) != null ? $"\"{GetDocumentTypeFromAttribute(aggregate)}\"" : "null")}});
                             {{GenerateSettingsApplicationCode(aggregate)}}
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }

                             /// <summary>
                             /// Creates a new {{aggregate.IdentifierName}} aggregate with the specified identifier and first event.
                             /// </summary>
                             /// <typeparam name="T">The type of the first event.</typeparam>
                             /// <param name="id">The identifier for the new aggregate.</param>
                             /// <param name="firstEvent">The first event to append to the aggregate's event stream.</param>
                             /// <param name="metadata">Optional metadata to attach to the event.</param>
                             /// <returns>A new {{aggregate.IdentifierName}} instance with the event applied.</returns>
                             protected async Task<{{aggregate.IdentifierName}}> CreateAsync<T>({{aggregate.IdentifierType}} id, T firstEvent, ActionMetadata? metadata = null) where T : class
                             {
                                var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString(), {{(GetDocumentStoreFromAttribute(aggregate) != null ? $"\"{GetDocumentStoreFromAttribute(aggregate)}\"" : "null")}}, {{(GetDocumentTypeFromAttribute(aggregate) != null ? $"\"{GetDocumentTypeFromAttribute(aggregate)}\"" : "null")}});
                            {{GenerateSettingsApplicationCode(aggregate)}}
                                var eventStream = eventStreamFactory.Create(document);
                                var obj = new {{aggregate.IdentifierName}}(eventStream);
                                await eventStream.Session(context => context.Append(firstEvent, metadata));
                                await obj.Fold();
                                return obj;
                             }

                             /// <summary>
                             /// Gets an existing {{aggregate.IdentifierName}} aggregate by identifier.
                             /// </summary>
                             /// <param name="id">The identifier of the aggregate to retrieve.</param>
                             /// <param name="upToVersion">Optional maximum event version to fold. If null, loads to current state.</param>
                             /// <returns>The loaded {{aggregate.IdentifierName}} instance.</returns>
                             [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetByIdAsync instead. This method will be removed in a future version.")]
                             public async Task<{{aggregate.IdentifierName}}> GetAsync({{aggregate.IdentifierType}} id, int? upToVersion = null)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(), {{(GetDocumentStoreFromAttribute(aggregate) != null ? $"\"{GetDocumentStoreFromAttribute(aggregate)}\"" : "null")}}, {{(GetDocumentTypeFromAttribute(aggregate) != null ? $"\"{GetDocumentTypeFromAttribute(aggregate)}\"" : "null")}});

                                 // Create event stream
                                 var eventStream = eventStreamFactory.Create(document);

                                 // Create aggregate FIRST to register upcasters and event handlers
                                 var obj = new {{aggregate.IdentifierName}}(eventStream);

                                 // Read events up to version WITH upcasting applied
                                 var events = await eventStream.ReadAsync(0, upToVersion);

                                 // Fold events into the aggregate
                                 foreach (var e in events)
                                 {
                                     obj.Fold(e);
                                 }

                                 return obj;
                             }

                             /// <summary>
                             /// Gets an existing {{aggregate.IdentifierName}} aggregate by identifier along with its document.
                             /// </summary>
                             /// <param name="id">The identifier of the aggregate to retrieve.</param>
                             /// <returns>A tuple containing the loaded {{aggregate.IdentifierName}} instance and its document.</returns>
                             [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetByIdWithDocumentAsync instead. This method will be removed in a future version.")]
                             public async Task<({{aggregate.IdentifierName}}, IObjectDocument)> GetWithDocumentAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(), {{(GetDocumentStoreFromAttribute(aggregate) != null ? $"\"{GetDocumentStoreFromAttribute(aggregate)}\"" : "null")}}, {{(GetDocumentTypeFromAttribute(aggregate) != null ? $"\"{GetDocumentTypeFromAttribute(aggregate)}\"" : "null")}});
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return (obj, document);
                             }

                            /// <summary>
                            /// Gets the first {{aggregate.IdentifierName}} aggregate with the specified document tag.
                            /// </summary>
                            /// <param name="tag">The document tag to search for.</param>
                            /// <returns>The first matching {{aggregate.IdentifierName}} instance, or null if not found.</returns>
                            [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetFirstByDocumentTagAsync instead. This method will be removed in a future version.")]
                            public async Task<{{aggregate.IdentifierName}}?> GetFirstByDocumentTag(string tag)
                            {
                                var document = await this.objectDocumentFactory.GetFirstByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? ", \"" + GetDocumentTagStoreFromAttribute(aggregate) + "\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? ", \"" + GetDocumentStoreFromAttribute(aggregate) + "\"" : "")}});
                                if (document == null)
                                {
                                    return null;
                                }
                                var obj = Create(document);
                                await obj.Fold();
                                return obj;
                            }

                            /// <summary>
                            /// Gets all {{aggregate.IdentifierName}} aggregates with the specified document tag.
                            /// </summary>
                            /// <param name="tag">The document tag to search for.</param>
                            /// <returns>A collection of all matching {{aggregate.IdentifierName}} instances.</returns>
                            [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetAllByDocumentTagAsync instead. This method will be removed in a future version.")]
                            public async Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTag(string tag)
                            {
                                var documents = (await this.objectDocumentFactory.GetByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? ", \"" + GetDocumentTagStoreFromAttribute(aggregate) + "\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? ", \"" + GetDocumentStoreFromAttribute(aggregate) + "\"" : "")}}));
                                var items = new List<{{aggregate.IdentifierName}}>();
                                foreach (var document in documents)
                                {
                                    var obj = Create(document);
                                    await obj.Fold();
                                    items.Add(obj);
                                }
                                return items;
                            }

                          {{codeGetById}}

                          }

                          //<auto-generated />
                          public partial interface I{{aggregate.IdentifierName}}Repository
                          {
                              /// <summary>
                              /// Gets a paginated list of all {{aggregate.IdentifierName}} object IDs using continuation tokens.
                              /// </summary>
                              Task<PagedResult<string>> GetObjectIdsAsync(
                                  string? continuationToken = null,
                                  int pageSize = 100,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Gets a single aggregate by ID.
                              /// </summary>
                              /// <param name="upToVersion">Optional: The maximum event version to fold. If null, loads to current state.</param>
                              Task<{{aggregate.IdentifierName}}?> GetByIdAsync(
                                  {{aggregate.IdentifierType}} id,
                                  int? upToVersion = null,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Gets a single aggregate by ID along with its document.
                              /// </summary>
                              Task<({{aggregate.IdentifierName}}?, IObjectDocument?)> GetByIdWithDocumentAsync(
                                  {{aggregate.IdentifierType}} id,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Gets the first aggregate with the specified document tag.
                              /// </summary>
                              Task<{{aggregate.IdentifierName}}?> GetFirstByDocumentTagAsync(
                                  string tag,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Gets all aggregates with the specified document tag.
                              /// </summary>
                              Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTagAsync(
                                  string tag,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Checks if an aggregate with the given ID exists.
                              /// </summary>
                              Task<bool> ExistsAsync(
                                  {{aggregate.IdentifierType}} id,
                                  CancellationToken cancellationToken = default);

                              /// <summary>
                              /// Gets the total count of aggregates.
                              /// Warning: This may be expensive for large datasets.
                              /// </summary>
                              Task<long> CountAsync(CancellationToken cancellationToken = default);
                          }

                          //<auto-generated />
                          /// <summary>
                          /// Repository for querying and managing {{aggregate.IdentifierName}} aggregates.
                          /// </summary>
                          public partial class {{aggregate.IdentifierName}}Repository : I{{aggregate.IdentifierName}}Repository
                          {
                              private readonly I{{aggregate.IdentifierName}}Factory {{aggregate.IdentifierName.ToLowerInvariant()}}Factory;
                              private readonly IObjectDocumentFactory objectDocumentFactory;
                              private readonly IObjectIdProvider objectIdProvider;

                              /// <summary>
                              /// Gets the object name used for document storage.
                              /// </summary>
                              public static string ObjectName => "{{aggregate.ObjectName}}";

                              /// <summary>
                              /// Initializes a new instance of the {{aggregate.IdentifierName}}Repository class.
                              /// </summary>
                              /// <param name="{{aggregate.IdentifierName.ToLowerInvariant()}}Factory">Factory for creating {{aggregate.IdentifierName}} instances.</param>
                              /// <param name="objectDocumentFactory">Factory for managing object documents.</param>
                              /// <param name="objectIdProvider">Provider for querying object identifiers.</param>
                              public {{aggregate.IdentifierName}}Repository(
                                  I{{aggregate.IdentifierName}}Factory {{aggregate.IdentifierName.ToLowerInvariant()}}Factory,
                                  IObjectDocumentFactory objectDocumentFactory,
                                  IObjectIdProvider objectIdProvider)
                              {
                                  ArgumentNullException.ThrowIfNull({{aggregate.IdentifierName.ToLowerInvariant()}}Factory);
                                  ArgumentNullException.ThrowIfNull(objectDocumentFactory);
                                  ArgumentNullException.ThrowIfNull(objectIdProvider);

                                  this.{{aggregate.IdentifierName.ToLowerInvariant()}}Factory = {{aggregate.IdentifierName.ToLowerInvariant()}}Factory;
                                  this.objectDocumentFactory = objectDocumentFactory;
                                  this.objectIdProvider = objectIdProvider;
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<PagedResult<string>> GetObjectIdsAsync(
                                  string? continuationToken = null,
                                  int pageSize = 100,
                                  CancellationToken cancellationToken = default)
                              {
                                  ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
                                  ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);

                                  return await objectIdProvider.GetObjectIdsAsync(
                                      ObjectName,
                                      continuationToken,
                                      pageSize,
                                      cancellationToken);
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<{{aggregate.IdentifierName}}?> GetByIdAsync(
                                  {{aggregate.IdentifierType}} id,
                                  int? upToVersion = null,
                                  CancellationToken cancellationToken = default)
                              {
                                  try
                                  {
                                      return await {{aggregate.IdentifierName.ToLowerInvariant()}}Factory.GetAsync(id, upToVersion);
                                  }
                                  catch (Exception)
                                  {
                                      return null;
                                  }
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<({{aggregate.IdentifierName}}?, IObjectDocument?)> GetByIdWithDocumentAsync(
                                  {{aggregate.IdentifierType}} id,
                                  CancellationToken cancellationToken = default)
                              {
                                  try
                                  {
                                      var document = await objectDocumentFactory.GetAsync(ObjectName, id.ToString(), {{(GetDocumentStoreFromAttribute(aggregate) != null ? $"\"{GetDocumentStoreFromAttribute(aggregate)}\"" : "null")}}, {{(GetDocumentTypeFromAttribute(aggregate) != null ? $"\"{GetDocumentTypeFromAttribute(aggregate)}\"" : "null")}});
                                      var obj = {{aggregate.IdentifierName.ToLowerInvariant()}}Factory.Create(document);
                                      await obj.Fold();
                                      return (obj, document);
                                  }
                                  catch (Exception)
                                  {
                                      return (null, null);
                                  }
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<{{aggregate.IdentifierName}}?> GetFirstByDocumentTagAsync(
                                  string tag,
                                  CancellationToken cancellationToken = default)
                              {
                                  ArgumentException.ThrowIfNullOrWhiteSpace(tag);

                                  var document = await objectDocumentFactory.GetFirstByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentTagStoreFromAttribute(aggregate)}\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                                  if (document == null)
                                  {
                                      return null;
                                  }

                                  var obj = {{aggregate.IdentifierName.ToLowerInvariant()}}Factory.Create(document);
                                  await obj.Fold();
                                  return obj;
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTagAsync(
                                  string tag,
                                  CancellationToken cancellationToken = default)
                              {
                                  ArgumentException.ThrowIfNullOrWhiteSpace(tag);

                                  var documents = await objectDocumentFactory.GetByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentTagStoreFromAttribute(aggregate)}\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                                  var items = new List<{{aggregate.IdentifierName}}>();

                                  foreach (var document in documents)
                                  {
                                      var obj = {{aggregate.IdentifierName.ToLowerInvariant()}}Factory.Create(document);
                                      await obj.Fold();
                                      items.Add(obj);
                                  }

                                  return items;
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<bool> ExistsAsync(
                                  {{aggregate.IdentifierType}} id,
                                  CancellationToken cancellationToken = default)
                              {
                                  return await objectIdProvider.ExistsAsync(
                                      ObjectName,
                                      id.ToString(),
                                      cancellationToken);
                              }

                              {{(aggregate.HasUserDefinedRepositoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                              public async Task<long> CountAsync(CancellationToken cancellationToken = default)
                              {
                                  return await objectIdProvider.CountAsync(ObjectName, cancellationToken);
                              }
                          }
                          """);

        return code;
    }
}
