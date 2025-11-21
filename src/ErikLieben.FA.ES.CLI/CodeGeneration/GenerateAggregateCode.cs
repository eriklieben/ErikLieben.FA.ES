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

                await GenerateAggregate(aggregate, path);
            }
        }
    }

    private static async Task GenerateAggregate(AggregateDefinition aggregate, string? path)
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
        var code = AssembleAggregateCode(aggregate, usings, postWhenCode, foldCode, serializableCode, propertyCode, propertySnapshotCode, get, ctorInput, setupCode);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString()));
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
            "System.Diagnostics.CodeAnalysis"
        };

        usings.AddRange(aggregate.Properties
            .Where(p => !usings.Contains(p.Namespace))
            .Select(p => p.Namespace));

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
        foreach (var @event in aggregate.Events)
        {
            if (@event.ActivationType != "When")
            {
                continue;
            }

            if (!usings.Contains(@event.Namespace))
            {
                usings.Add(@event.Namespace);
            }

            if (@event.Parameters.Count > 1)
            {
                GenerateFoldCodeWithMultipleParameters(@event, foldCode);
            }
            else
            {
                GenerateFoldCodeWithSingleParameter(@event, foldCode);
            }
        }
        return foldCode;
    }

    internal static void GenerateFoldCodeWithMultipleParameters(EventDefinition @event, StringBuilder foldCode)
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
                                  When(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
                              break;
                              """);
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
        if (!property.IsGeneric)
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
            setupCode.AppendLine($$"""
                 Stream.RegisterEvent<{{usedEvent.TypeName}}>(
                     "{{usedEvent.EventName}}",
                     {{usedEvent.TypeName}}JsonSerializerContext.Default.{{usedEvent.TypeName}});
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
        StringBuilder setupCode)
    {
        var code = new StringBuilder();
        string codeGetById = "";

        foreach (var namespaceName in usings.Order())
        {
            code.AppendLine($"using {namespaceName};");
        }

        code.AppendLine("");
        code.AppendLine("#nullable enable");
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

                          // <auto-generated />
                          public interface I{{aggregate.IdentifierName}} {
                                {{propertyCode}}
                          }

                          // <auto-generated />
                          public record {{aggregate.IdentifierName}}Snapshot : I{{aggregate.IdentifierName}} {
                                {{propertySnapshotCode}}
                          }

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


                             {{(aggregate.HasUserDefinedFactoryPartial ? "[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]" : "")}}
                             public async Task<{{aggregate.IdentifierName}}> CreateAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                             {{GenerateSettingsApplicationCode(aggregate)}}
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return obj;
                             }

                             protected async Task<{{aggregate.IdentifierName}}> CreateAsync<T>({{aggregate.IdentifierType}} id, T firstEvent, ActionMetadata? metadata = null) where T : class
                             {
                                var document = await this.objectDocumentFactory.GetOrCreateAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                            {{GenerateSettingsApplicationCode(aggregate)}}
                                var eventStream = eventStreamFactory.Create(document);
                                var obj = new {{aggregate.IdentifierName}}(eventStream);
                                await eventStream.Session(context => context.Append(firstEvent, metadata));
                                await obj.Fold();
                                return obj;
                             }

                             [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetByIdAsync instead. This method will be removed in a future version.")]
                             public async Task<{{aggregate.IdentifierName}}> GetAsync({{aggregate.IdentifierType}} id, int? upToVersion = null)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});

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

                             [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetByIdWithDocumentAsync instead. This method will be removed in a future version.")]
                             public async Task<({{aggregate.IdentifierName}}, IObjectDocument)> GetWithDocumentAsync({{aggregate.IdentifierType}} id)
                             {
                                 var document = await this.objectDocumentFactory.GetAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                                 var obj = Create(document);
                                 await obj.Fold();
                                 return (obj, document);
                             }

                            [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetFirstByDocumentTagAsync instead. This method will be removed in a future version.")]
                            public async Task<{{aggregate.IdentifierName}}?> GetFirstByDocumentTag(string tag)
                            {
                                var document = await this.objectDocumentFactory.GetFirstByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentTagStoreFromAttribute(aggregate)}\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
                                if (document == null)
                                {
                                    return null;
                                }
                                var obj = Create(document);
                                await obj.Fold();
                                return obj;
                            }

                            [Obsolete("Use I{{aggregate.IdentifierName}}Repository.GetAllByDocumentTagAsync instead. This method will be removed in a future version.")]
                            public async Task<IEnumerable<{{aggregate.IdentifierName}}>> GetAllByDocumentTag(string tag)
                            {
                                var documents = (await this.objectDocumentFactory.GetByObjectDocumentTag(ObjectName, tag{{(GetDocumentTagStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentTagStoreFromAttribute(aggregate)}\"" : "")}}{{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}}));
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
                          public partial class {{aggregate.IdentifierName}}Repository : I{{aggregate.IdentifierName}}Repository
                          {
                              private readonly I{{aggregate.IdentifierName}}Factory {{aggregate.IdentifierName.ToLowerInvariant()}}Factory;
                              private readonly IObjectDocumentFactory objectDocumentFactory;
                              private readonly IObjectIdProvider objectIdProvider;

                              public static string ObjectName => "{{aggregate.ObjectName}}";

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
                                      var document = await objectDocumentFactory.GetAsync(ObjectName, id.ToString(){{(GetDocumentStoreFromAttribute(aggregate) != null ? $", \"{GetDocumentStoreFromAttribute(aggregate)}\"" : "")}});
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
