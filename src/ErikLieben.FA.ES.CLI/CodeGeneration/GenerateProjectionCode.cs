using System.Text;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using ErikLieben.FA.ES.Projections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Holds the generated code components for a projection class.
/// </summary>
internal record ProjectionCodeComponents(
    string FoldMethod,
    string WhenParameterValueBindingCode,
    StringBuilder CtorCode,
    string CheckpointJsonAnnotation,
    string JsonBlobFactoryCode,
    StringBuilder PropertyCode,
    StringBuilder SerializableCode,
    string DeserializationCode,
    string CreateDestinationInstanceCode,
    string RoutedProjectionSerializationCode);

public class GenerateProjectionCode
{
    private const string IEventTypeName = "IEvent";

    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    public GenerateProjectionCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var projection in project.Projections)
            {
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [yellow]{projection.Name}[/]");
                var currentFile = projection.FileLocations.FirstOrDefault();
                if (currentFile is null || currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rel = (projection.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized);
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");
                await GenerateProjection(projection, path);
            }
        }
    }

    private async Task GenerateProjection(ProjectionDefinition projection, string? path)
    {
        var usings = InitializeUsings(projection);
        var generatorVersion = solution.Generator?.Version ?? "1.0.0";
        var (_, whenParameterDeclarations) = GenerateWhenMethodsForProjection(projection, usings);
        var (serializableCode, _) = GenerateJsonSerializationCode(projection);
        var (propertyCode, _) = GeneratePropertyCode(projection);
        var (get, ctorInput) = GenerateConstructorParametersForFactory(projection);
        var jsonBlobFactoryCode = GenerateBlobFactoryCode(projection, usings, get, ctorInput, generatorVersion);
        var cosmosDbFactoryCode = GenerateCosmosDbFactoryCode(projection, usings, get, ctorInput);
        // Combine factory codes - a projection can have either Blob or CosmosDB (or neither)
        var combinedFactoryCode = !string.IsNullOrEmpty(jsonBlobFactoryCode)
            ? jsonBlobFactoryCode
            : cosmosDbFactoryCode;
        var whenParameterValueBindingCode = GenerateWhenParameterBindingCode(whenParameterDeclarations);
        var postWhenAllDummyCode = GeneratePostWhenAllDummyCode(projection, generatorVersion);
        var foldMethod = GenerateFoldMethod(projection, postWhenAllDummyCode);
        var ctorCode = SelectBestConstructorAndGenerateCode(projection);
        var checkpointJsonAnnotation = projection.ExternalCheckpoint ? "[JsonIgnore]" : "[JsonPropertyName(\"$checkpoint\")]";

        var deserializationCode = GenerateDeserializationCode(projection, ctorCode.ToString());
        var createDestinationInstanceCode = GenerateCreateDestinationInstanceMethod(projection);
        var routedProjectionSerializationCode = GenerateRoutedProjectionSerializationMethods();

        var codeComponents = new ProjectionCodeComponents(
            foldMethod, whenParameterValueBindingCode, ctorCode, checkpointJsonAnnotation,
            combinedFactoryCode, propertyCode, serializableCode, deserializationCode, createDestinationInstanceCode,
            routedProjectionSerializationCode);

        await AssembleAndWriteCode(projection, usings, codeComponents, path, generatorVersion);
    }

    private static List<string> InitializeUsings(ProjectionDefinition projection)
    {
        var usings = new List<string>
        {
            "System.Collections.Generic",
            "System.Text.Json.Serialization",
            "System.Threading",
            "System.Threading.Tasks",
            "ErikLieben.FA.ES",
            "ErikLieben.FA.ES.Projections",
            "ErikLieben.FA.ES.Documents",
            "System.Text.Json",
            "ErikLieben.FA.ES.VersionTokenParts",
            "System.CodeDom.Compiler",
            "System.Diagnostics.CodeAnalysis"
        };

        usings.AddRange(projection.Properties
            .Where(p => !usings.Contains(p.Namespace))
            .Select(p => p.Namespace));

        return usings;
    }

    private static (StringBuilder foldCode, List<string> whenParameterDeclarations) GenerateWhenMethodsForProjection(
        ProjectionDefinition projection, List<string> usings)
    {
        var foldCode = new StringBuilder();
        var whenParameterDeclarations = new List<string>();

        foreach (var @event in projection.Events)
        {
            GenerateWhenMethods(@event, usings, whenParameterDeclarations, foldCode);
        }

        return (foldCode, whenParameterDeclarations);
    }

    private static (StringBuilder serializableCode, StringBuilder newJsonSerializableCode) GenerateJsonSerializationCode(
        ProjectionDefinition projection)
    {
        var serializableCode = new StringBuilder();
        var newJsonSerializableCode = new StringBuilder();
        var propertyTypes = new List<string>();

        foreach (var usedEvent in projection.Events)
        {
            newJsonSerializableCode.AppendLine("// <auto-generated />");
            if (usedEvent.Properties.Any(p => p.Type.EndsWith("Identifier")))
            {
                newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof(System.Guid))]");
            }
            serializableCode.AppendLine($"[JsonSerializable(typeof({usedEvent.TypeName}))]");

            foreach (var property in usedEvent.Properties)
            {
                var fullTypeDef = BuildFullTypeDefinition(property);
                if (!propertyTypes.Contains(fullTypeDef))
                {
                    propertyTypes.Add(fullTypeDef);
                }
                newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof({fullTypeDef}))]");
            }

            newJsonSerializableCode.AppendLine($"[JsonSerializable(typeof({@usedEvent.TypeName}))]");
            newJsonSerializableCode.AppendLine(
                "internal partial class " + usedEvent.TypeName + "JsonSerializerContext : JsonSerializerContext { }");
            newJsonSerializableCode.AppendLine("");
        }

        // Add serialization for projection's own properties
        foreach (var property in projection.Properties)
        {
            AddPropertySerializationAttributes(property, serializableCode, propertyTypes);
        }

        serializableCode.AppendLine($"[JsonSerializable(typeof({projection.Name}))]");

        // Remove trailing newline from serializableCode to avoid blank line before JsonSourceGenerationOptions
        if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\n')
        {
            serializableCode.Length--;
            if (serializableCode.Length > 0 && serializableCode[serializableCode.Length - 1] == '\r')
            {
                serializableCode.Length--;
            }
        }

        return (serializableCode, newJsonSerializableCode);
    }

    private static void AddPropertySerializationAttributes(
        PropertyDefinition property,
        StringBuilder serializableCode,
        List<string> processedTypes)
    {
        var fullTypeDef = BuildFullTypeDefinition(property);

        // Add the main type
        if (!processedTypes.Contains(fullTypeDef))
        {
            processedTypes.Add(fullTypeDef);
            serializableCode.AppendLine($"[JsonSerializable(typeof({fullTypeDef}))]");
        }

        // If it's a generic collection type, also add the inner type
        if (property.IsGeneric && property.GenericTypes.Count > 0)
        {
            foreach (var genericType in property.GenericTypes)
            {
                var genericTypeDef = $"{genericType.Namespace}.{genericType.Name}";
                if (!processedTypes.Contains(genericTypeDef))
                {
                    processedTypes.Add(genericTypeDef);
                    serializableCode.AppendLine($"[JsonSerializable(typeof({genericTypeDef}))]");
                }

                // Process nested generic types recursively
                if (genericType.GenericTypes.Count > 0)
                {
                    ProcessNestedGenericType(genericType, serializableCode, processedTypes);
                }

                // Process SubTypes of the generic type (e.g., TextItem within QuestionItem)
                foreach (var subType in genericType.SubTypes)
                {
                    ProcessSubType(subType, serializableCode, processedTypes);
                }
            }
        }

        // Process SubTypes (nested complex types within the property)
        foreach (var subType in property.SubTypes)
        {
            ProcessSubType(subType, serializableCode, processedTypes);
        }
    }

    private static void ProcessNestedGenericType(
        PropertyGenericTypeDefinition genericType,
        StringBuilder serializableCode,
        List<string> processedTypes)
    {
        var genericTypeDef = BuildGenericTypeDefinition(genericType);
        if (!processedTypes.Contains(genericTypeDef))
        {
            processedTypes.Add(genericTypeDef);
            serializableCode.AppendLine($"[JsonSerializable(typeof({genericTypeDef}))]");
        }

        // Recursively process subtypes
        foreach (var subType in genericType.SubTypes)
        {
            ProcessSubType(subType, serializableCode, processedTypes);
        }
    }

    private static void ProcessSubType(
        PropertyGenericTypeDefinition subType,
        StringBuilder serializableCode,
        List<string> processedTypes)
    {
        var subTypeDef = BuildGenericTypeDefinition(subType);
        if (!processedTypes.Contains(subTypeDef))
        {
            processedTypes.Add(subTypeDef);
            serializableCode.AppendLine($"[JsonSerializable(typeof({subTypeDef}))]");
        }

        // Recursively process nested subtypes
        foreach (var nestedSubType in subType.SubTypes)
        {
            ProcessSubType(nestedSubType, serializableCode, processedTypes);
        }
    }

    private static string BuildGenericTypeDefinition(PropertyGenericTypeDefinition type)
    {
        var builder = new StringBuilder();
        builder.Append(type.Namespace).Append('.').Append(type.Name);

        if (type.GenericTypes.Count > 0)
        {
            builder.Append('<');
            for (int i = 0; i < type.GenericTypes.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(BuildGenericTypeDefinition(type.GenericTypes[i]));
            }
            builder.Append('>');
        }

        return builder.ToString();
    }

    private static string BuildFullTypeDefinition(PropertyDefinition property)
    {
        var fullTypeDefBuilder = new StringBuilder();
        fullTypeDefBuilder.Append(property.Namespace).Append('.').Append(property.Type);

        if (property.IsGeneric)
        {
            fullTypeDefBuilder.Append('<');
            foreach (var generic in property.GenericTypes)
            {
                fullTypeDefBuilder.Append(generic.Namespace).Append('.').Append(generic.Name);
                if (property.GenericTypes[^1] != generic)
                {
                    fullTypeDefBuilder.Append(',');
                }
            }
            fullTypeDefBuilder.Append('>');
        }

        return fullTypeDefBuilder.ToString();
    }

    private static (StringBuilder propertyCode, StringBuilder propertySnapshotCode) GeneratePropertyCode(
        ProjectionDefinition projection)
    {
        var propertyCode = new StringBuilder();
        var propertySnapshotCode = new StringBuilder();

        foreach (var property in projection.Properties)
        {
            var type = BuildPropertyTypeString(property);

            if (property.Name != "WhenParameterValueFactories")
            {
                propertySnapshotCode.AppendLine($"public {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get; init; }}");
            }

            if (property.Name != "Checkpoint" && projection.Name != "CheckpointFingerprint" && property.Name != "WhenParameterValueFactories" && property.Name != "CurrentContext")
            {
                propertyCode.AppendLine($"public {type}{(property.IsNullable ? "?" : string.Empty)} {property.Name} {{get;}}");
            }
        }

        return (propertyCode, propertySnapshotCode);
    }

    private static string BuildPropertyTypeString(PropertyDefinition property)
    {
        var typeBuilder = new StringBuilder(property.Type);
        if (property.IsGeneric)
        {
            typeBuilder.Append('<');
            foreach (var generic in property.GenericTypes)
            {
                typeBuilder.Append(BuildGenericTypeStringRecursive(generic));
                if (property.GenericTypes[^1] != generic)
                {
                    typeBuilder.Append(',');
                }
            }
            typeBuilder.Append('>');
        }

        return typeBuilder.ToString();
    }

    private static string BuildGenericTypeStringRecursive(PropertyGenericTypeDefinition prop)
    {
        var typeBuilder = new StringBuilder();
        typeBuilder.Append(prop.Namespace).Append('.').Append(prop.Name);

        if (prop.GenericTypes.Count != 0)
        {
            typeBuilder.Append('<');
            foreach (var generic in prop.GenericTypes)
            {
                if (prop.GenericTypes.Count != 0)
                {
                    typeBuilder.Append(BuildGenericTypeStringRecursive(prop.GenericTypes[0]));
                }
                else
                {
                    typeBuilder.Append(generic.Namespace).Append('.').Append(generic.Name);
                }

                if (prop.GenericTypes[^1] != generic)
                {
                    typeBuilder.Append(',');
                }
            }
            typeBuilder.Append('>');
        }

        return typeBuilder.ToString();
    }

    private static (string get, string ctorInput) GenerateConstructorParametersForFactory(ProjectionDefinition projection)
    {
        var getBuilder = new StringBuilder();
        var ctorInputBuilder = new StringBuilder();

        // Find the best constructor (prefer one with most parameters that includes IObjectDocumentFactory and IEventStreamFactory)
        var specialDependencies = new[] { "IObjectDocumentFactory", "IEventStreamFactory" };
        var bestMatch = projection.Constructors
            .Where(ctor => specialDependencies.All(dep => ctor.Parameters.Any(p => p.Type == dep)))
            .OrderByDescending(ctor => ctor.Parameters.Count)
            .FirstOrDefault();

        if (bestMatch == null)
        {
            return (string.Empty, string.Empty);
        }

        // Generate DI resolution for parameters that are not IObjectDocumentFactory or IEventStreamFactory
        foreach (var param in bestMatch.Parameters.Where(p => p.Type != "IObjectDocumentFactory" && p.Type != "IEventStreamFactory"))
        {
            getBuilder.AppendLine($"            var {param.Name} = serviceProvider.GetService(typeof({param.Type})) as {param.Type};");
            ctorInputBuilder.Append($", {param.Name}!");
        }

        return (getBuilder.ToString(), ctorInputBuilder.ToString());
    }

    private static string GenerateBlobFactoryCode(ProjectionDefinition projection, List<string> usings, string get, string ctorInput, string version)
    {
        if (projection.BlobProjection == null)
        {
            return string.Empty;
        }

        usings.Add("ErikLieben.FA.ES.AzureStorage.Blob");
        usings.Add("Microsoft.Extensions.Azure");
        usings.Add("Azure.Storage.Blobs");

        // Check if this is a routed projection
        if (projection is RoutedProjectionDefinition routedProjection && routedProjection.IsRoutedProjection)
        {
            return GenerateRoutedBlobFactoryCode(routedProjection, usings, get, ctorInput);
        }

        var needsServiceProvider = !string.IsNullOrEmpty(get);
        var serviceProviderParam = needsServiceProvider ? ",\n    IServiceProvider serviceProvider" : "";

        var newMethodBody = needsServiceProvider
            ? $"{get}            return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});"
            : $"return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});";

        return $$"""
                 /// <summary>
                 /// Factory for creating and managing {{projection.Name}} blob-based projections.
                 /// </summary>
                 public partial class {{projection.Name}}Factory(
                     IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
                     IObjectDocumentFactory objectDocumentFactory,
                     IEventStreamFactory eventStreamFactory{{serviceProviderParam}})
                     : BlobProjectionFactory<{{projection.Name}}>(
                         blobServiceClientFactory,
                         "{{projection.BlobProjection.Connection}}",
                         "{{projection.BlobProjection.Container}}")
                 {
                     [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                     [ExcludeFromCodeCoverage]
                     protected override bool HasExternalCheckpoint => {{projection.ExternalCheckpoint.ToString().ToLowerInvariant()}};

                     [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                     [ExcludeFromCodeCoverage]
                     protected override {{projection.Name}} New()
                     {
                         {{newMethodBody}}
                     }

                     /// <summary>
                     /// Loads a {{projection.Name}} instance from JSON with complete state restoration.
                     /// </summary>
                     /// <param name="json">The JSON string containing the serialized projection state.</param>
                     /// <param name="documentFactory">Factory for managing object documents.</param>
                     /// <param name="eventStreamFactory">Factory for creating event streams.</param>
                     /// <returns>A {{projection.Name}} instance with restored state, or null if deserialization fails.</returns>
                     protected override {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                     {
                         return {{projection.Name}}.LoadFromJson(json, documentFactory, eventStreamFactory);
                     }
                 }
                """;
    }

    private static string GenerateCosmosDbFactoryCode(ProjectionDefinition projection, List<string> usings, string get, string ctorInput)
    {
        if (projection.CosmosDbProjection == null)
        {
            return string.Empty;
        }

        usings.Add("ErikLieben.FA.ES.CosmosDb");
        usings.Add("ErikLieben.FA.ES.CosmosDb.Configuration");
        usings.Add("Microsoft.Azure.Cosmos");

        var needsServiceProvider = !string.IsNullOrEmpty(get);
        var serviceProviderParam = needsServiceProvider ? ",\n    IServiceProvider serviceProvider" : "";

        var newMethodBody = needsServiceProvider
            ? $"{get}            return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});"
            : $"return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});";

        return $$"""
                 /// <summary>
                 /// Factory for creating and managing {{projection.Name}} CosmosDB-based projections.
                 /// </summary>
                 public partial class {{projection.Name}}Factory(
                     CosmosClient cosmosClient,
                     EventStreamCosmosDbSettings settings,
                     IObjectDocumentFactory objectDocumentFactory,
                     IEventStreamFactory eventStreamFactory{{serviceProviderParam}})
                     : CosmosDbProjectionFactory<{{projection.Name}}>(
                         cosmosClient,
                         settings,
                         "{{projection.CosmosDbProjection.Container}}",
                         "{{projection.CosmosDbProjection.PartitionKeyPath}}")
                 {
                     /// <summary>
                     /// Gets a value indicating whether this projection uses an external checkpoint mechanism.
                     /// </summary>
                     protected override bool HasExternalCheckpoint => {{projection.ExternalCheckpoint.ToString().ToLowerInvariant()}};

                     /// <summary>
                     /// Creates a new instance of {{projection.Name}} projection.
                     /// </summary>
                     /// <returns>A new {{projection.Name}} instance.</returns>
                     protected override {{projection.Name}} New()
                     {
                         {{newMethodBody}}
                     }

                     /// <summary>
                     /// Loads a {{projection.Name}} instance from JSON with complete state restoration.
                     /// </summary>
                     /// <param name="json">The JSON string containing the serialized projection state.</param>
                     /// <param name="documentFactory">Factory for managing object documents.</param>
                     /// <param name="eventStreamFactory">Factory for creating event streams.</param>
                     /// <returns>A {{projection.Name}} instance with restored state, or null if deserialization fails.</returns>
                     protected override {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                     {
                         return {{projection.Name}}.LoadFromJson(json, documentFactory, eventStreamFactory);
                     }
                 }
                """;
    }

    private static string GenerateRoutedBlobFactoryCode(RoutedProjectionDefinition projection, List<string> usings, string get, string ctorInput)
    {
        usings.Add("ErikLieben.FA.ES.Projections");
        usings.Add("System.Text");
        usings.Add("System.Text.Json");
        usings.Add("System.IO");

        var needsServiceProvider = !string.IsNullOrEmpty(get);
        var serviceProviderParam = needsServiceProvider ? ",\n    IServiceProvider serviceProvider" : "";

        var destinationType = projection.DestinationType ?? "Projection";

        return $$"""
                 /// <summary>
                 /// Factory for creating and managing {{projection.Name}} routed blob-based projections.
                 /// Main file at [BlobJsonProjection] path contains $checkpoint and $metadata.
                 /// Each destination is stored in a separate file.
                 /// </summary>
                 public partial class {{projection.Name}}Factory(
                     IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
                     IObjectDocumentFactory objectDocumentFactory,
                     IEventStreamFactory eventStreamFactory{{serviceProviderParam}})
                     : RoutedBlobProjectionFactory<{{projection.Name}}>(
                         blobServiceClientFactory,
                         "{{projection.BlobProjection!.Connection}}",
                         "{{projection.BlobProjection.Container}}")
                 {
                     /// <summary>
                     /// Gets the JSON serializer context for the projection.
                     /// </summary>
                     protected override JsonSerializerContext GetProjectionJsonContext()
                     {
                         return {{projection.Name}}JsonSerializerContext.Default;
                     }

                     /// <summary>
                     /// Loads the main projection (checkpoint and metadata) from JSON.
                     /// </summary>
                     protected override {{projection.Name}}? LoadMainProjectionFromJson(
                         string json,
                         IObjectDocumentFactory documentFactory,
                         IEventStreamFactory eventStreamFactory)
                     {
                         return {{projection.Name}}.LoadFromJson(json, documentFactory, eventStreamFactory);
                     }

                     /// <summary>
                     /// Loads a destination projection from JSON.
                     /// </summary>
                     protected override Projection LoadDestinationFromJson(
                         string json,
                         IObjectDocumentFactory documentFactory,
                         IEventStreamFactory eventStreamFactory,
                         string destinationKey)
                     {
                         {{GenerateLoadDestinationFromJsonBody(projection)}}
                     }

                     /// <summary>
                     /// Checks if a destination type has external checkpoint enabled.
                     /// </summary>
                     protected override bool DestinationHasExternalCheckpoint(string destinationTypeName)
                     {
                         {{GenerateDestinationHasExternalCheckpointBody(projection)}}
                     }

                     /// <summary>
                     /// Serializes the main projection (checkpoint and metadata) to JSON.
                     /// </summary>
                     protected override string SerializeMainProjection({{projection.Name}} projection)
                     {
                         return projection.ToJson();
                     }

                     /// <summary>
                     /// Gets save tasks for all destinations.
                     /// </summary>
                     protected override IEnumerable<Task> GetDestinationSaveTasks(
                         {{projection.Name}} projection,
                         BlobContainerClient containerClient,
                         CancellationToken cancellationToken)
                     {
                         if (projection.Destinations == null)
                         {
                             yield break;
                         }

                         foreach (var kvp in projection.Destinations)
                         {
                             var destinationKey = kvp.Key;
                             var destination = kvp.Value;

                             // Get the blob path from registry
                             if (projection.Registry.Destinations.TryGetValue(destinationKey, out var metadata) &&
                                 metadata.Metadata.TryGetValue("blobPath", out var blobPath))
                             {
                                 var json = destination.ToJson();
                                 yield return UploadBlobAsync(containerClient, blobPath, json, cancellationToken);

                                 // Save external checkpoint if the destination type has it enabled
                                 if (DestinationHasExternalCheckpoint(metadata.DestinationTypeName))
                                 {
                                     yield return SaveDestinationCheckpointAsync(blobPath, destination, cancellationToken);
                                 }
                             }
                         }
                     }

                     /// <summary>
                     /// Adds a loaded destination to the projection.
                     /// </summary>
                     protected override void AddDestinationToProjection(
                         {{projection.Name}} projection,
                         string destinationKey,
                         Projection destination)
                     {
                         // Use reflection to access the private _destinations field in RoutedProjection
                         var destinationsField = typeof(RoutedProjection).GetField("_destinations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                         var destinations = destinationsField?.GetValue(projection) as System.Collections.Concurrent.ConcurrentDictionary<string, Projection>;

                         if (destinations != null)
                         {
                             destinations[destinationKey] = destination;
                         }
                     }

                     /// <summary>
                     /// Sets the factories on the projection.
                     /// </summary>
                     protected override void SetFactories(
                         {{projection.Name}} projection,
                         IObjectDocumentFactory documentFactory,
                         IEventStreamFactory eventStreamFactory)
                     {
                         // Use reflection to set the private readonly fields
                         var docField = typeof(Projection).GetField("DocumentFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                         var streamField = typeof(Projection).GetField("EventStreamFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                         docField?.SetValue(projection, documentFactory);
                         streamField?.SetValue(projection, eventStreamFactory);
                     }

                     /// <summary>
                     /// Gets the path template for a destination type from its [BlobJsonProjection] attribute.
                     /// </summary>
                     protected override string? GetDestinationPathTemplate(string destinationTypeName)
                     {
                         {{GenerateDestinationPathTemplateLookup(projection)}}
                     }
                 }

                 /// <summary>
                 /// JSON serializer context for DestinationRegistry and related types.
                 /// </summary>
                 [JsonSerializable(typeof(DestinationRegistry))]
                 [JsonSerializable(typeof(DestinationMetadata))]
                 [JsonSerializable(typeof(RoutedProjectionMetadata))]
                 [JsonSerializable(typeof({{destinationType}}))]
                 [JsonSourceGenerationOptions(WriteIndented = true)]
                 internal partial class {{projection.Name}}DestinationRegistryJsonContext : JsonSerializerContext
                 {
                 }
                """;
    }

    private static string GenerateDestinationPathTemplateLookup(RoutedProjectionDefinition projection)
    {
        if (projection.DestinationPathTemplates.Count == 0)
        {
            return "return null;";
        }

        var code = new StringBuilder();
        code.AppendLine("return destinationTypeName switch");
        code.AppendLine("                        {");

        foreach (var kvp in projection.DestinationPathTemplates)
        {
            code.AppendLine($"                            \"{kvp.Key}\" => \"{kvp.Value}\",");
        }

        code.AppendLine("                            _ => null");
        code.Append("                        };");

        return code.ToString();
    }

    private static string GenerateLoadDestinationFromJsonBody(RoutedProjectionDefinition projection)
    {
        var destinationTypes = projection.DestinationPathTemplates.Keys.ToList();

        if (destinationTypes.Count == 0)
        {
            return "throw new ArgumentException($\"No destination types configured\", nameof(destinationKey));";
        }

        if (destinationTypes.Count == 1)
        {
            return $"return {destinationTypes[0]}.LoadFromJson(json, documentFactory, eventStreamFactory)!;";
        }

        // Multiple destination types - we need to look up the type from registry
        // For now, we'll try each type until one works (the registry has the type name)
        var code = new StringBuilder();
        code.AppendLine("// Multiple destination types - try to deserialize based on registry");
        code.AppendLine("Projection? result;");

        foreach (var destType in destinationTypes)
        {
            code.AppendLine($"                        result = {destType}.LoadFromJson(json, documentFactory, eventStreamFactory);");
            code.AppendLine("                        if (result != null) return result;");
        }

        code.Append($"                        throw new ArgumentException($\"Failed to deserialize destination {{destinationKey}}\", nameof(destinationKey));");

        return code.ToString();
    }

    private static string GenerateDestinationHasExternalCheckpointBody(RoutedProjectionDefinition projection)
    {
        if (projection.DestinationsWithExternalCheckpoint.Count == 0)
        {
            return "return false;";
        }

        var code = new StringBuilder();
        code.AppendLine("return destinationTypeName switch");
        code.AppendLine("                        {");

        foreach (var destType in projection.DestinationsWithExternalCheckpoint)
        {
            code.AppendLine($"                            \"{destType}\" => true,");
        }

        code.AppendLine("                            _ => false");
        code.Append("                        };");

        return code.ToString();
    }

    private static string GenerateWhenParameterBindingCode(List<string> whenParameterDeclarations)
    {
        if (whenParameterDeclarations.Count == 0)
        {
            return string.Empty;
        }

        return whenParameterDeclarations.Aggregate((x, y) => x + "," + y + Environment.NewLine);
    }

    private static string GeneratePostWhenCode(ProjectionDefinition projection)
    {
        if (projection.PostWhen == null)
        {
            return string.Empty;
        }

        var postWhenStringBuilder = new StringBuilder();
        postWhenStringBuilder.Append($"PostWhen(");

        foreach (var parameter in projection.PostWhen.Parameters)
        {
            switch (parameter.Type)
            {
                case "IObjectDocument":
                    postWhenStringBuilder.Append("document");
                    break;
                case IEventTypeName:
                    postWhenStringBuilder.Append("JsonEvent.ToEvent(@event, @event.EventType)");
                    break;
            }

            if (parameter != projection.PostWhen.Parameters[^1])
            {
                postWhenStringBuilder.Append(", ");
            }
        }
        postWhenStringBuilder.Append(");");

        return postWhenStringBuilder.ToString();
    }

    private static string GeneratePostWhenCodeWithVersionToken(ProjectionDefinition projection)
    {
        if (projection.PostWhen == null)
        {
            return string.Empty;
        }

        var postWhenStringBuilder = new StringBuilder();
        postWhenStringBuilder.Append($"PostWhen(");

        foreach (var parameter in projection.PostWhen.Parameters)
        {
            switch (parameter.Type)
            {
                case "IObjectDocument":
                    // In version token fold, we don't have document - use null or skip
                    postWhenStringBuilder.Append("null! /* Document not available in version token fold */");
                    break;
                case "VersionToken":
                    postWhenStringBuilder.Append("versionToken");
                    break;
                case IEventTypeName:
                    postWhenStringBuilder.Append("JsonEvent.ToEvent(@event, @event.EventType)");
                    break;
            }

            if (parameter != projection.PostWhen.Parameters[^1])
            {
                postWhenStringBuilder.Append(", ");
            }
        }
        postWhenStringBuilder.Append(");");

        return postWhenStringBuilder.ToString();
    }

    private static string GeneratePostWhenAllDummyCode(ProjectionDefinition projection, string version)
    {
        if (projection.HasPostWhenAllMethod)
        {
            return string.Empty;
        }

        var postWhenAllDummyCode = new StringBuilder();
        postWhenAllDummyCode.Append($"[GeneratedCode(\"ErikLieben.FA.ES\", \"{version}\")]\n");
        postWhenAllDummyCode.Append("[ExcludeFromCodeCoverage]\n");
        postWhenAllDummyCode.Append("protected override Task PostWhenAll(IObjectDocument document) { return Task.CompletedTask; }");

        return postWhenAllDummyCode.ToString();
    }

    private static string GenerateFoldMethod(ProjectionDefinition projection, string postWhenAllDummyCode)
    {
        var isAsync = projection.Events.Any(e => e.ActivationAwaitRequired);
        var asyncKeyword = isAsync ? "async " : string.Empty;
        var returnValue = isAsync ? "" : "Task.CompletedTask";

        // Generate version token-based fold code (using versionToken instead of document for parameter lookup)
        var foldWithVersionTokenCode = GenerateFoldCodeWithVersionToken(projection);
        var postWhenCodeWithVersionToken = GeneratePostWhenCodeWithVersionToken(projection);

        // Check if this is a routed projection
        var isRoutedProjection = projection is RoutedProjectionDefinition routedProj && routedProj.IsRoutedProjection;

        if (isRoutedProjection)
        {
            // For routed projections, we call the When methods directly (the base class Fold sets up routing context)
            return $$$"""
                      #nullable enable
                      /// <summary>
                      /// Dispatches events to When methods. Called by base RoutedProjection.Fold after routing context setup.
                      /// </summary>
                      protected override void DispatchToWhen(IEvent @event, VersionToken versionToken)
                      {
                         switch (@event.EventType)
                         {
                             {{{foldWithVersionTokenCode}}}
                         }
                      }

                      {{{postWhenAllDummyCode}}}
                      #nullable restore
                      """;
        }

        return $$$"""
                  #nullable enable
                  /// <summary>
                  /// Applies an event to the projection by dispatching to the appropriate When method using version token.
                  /// This is the primary implementation that avoids redundant document lookups.
                  /// </summary>
                  /// <typeparam name="T">The type of additional data passed through the execution context.</typeparam>
                  /// <param name="event">The event to apply to the projection.</param>
                  /// <param name="versionToken">The version token associated with the event.</param>
                  /// <param name="data">Optional data to pass through the execution context.</param>
                  /// <param name="parentContext">Optional parent execution context for nested projections.</param>
                  /// <returns>A task representing the asynchronous fold operation.</returns>
                  public override {{{asyncKeyword}}}Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = default(T?), IExecutionContext? parentContext = null) where T : class {

                     switch (@event.EventType)
                     {
                         {{{foldWithVersionTokenCode}}}
                     }

                     {{{postWhenCodeWithVersionToken}}}

                     return {{{returnValue}}};
                  }

                  {{{postWhenAllDummyCode}}}
                  #nullable restore
                  """;
    }

    private static string GenerateFoldCodeWithVersionToken(ProjectionDefinition projection)
    {
        var foldCode = new StringBuilder();
        var usings = new List<string>();

        foreach (var @event in projection.Events)
        {
            // Note: ActivationType is the method name (e.g., "When" or "MarkAsDeleted")
            // We process all methods that were detected as When handlers

            if (!usings.Contains(@event.Namespace))
            {
                usings.Add(@event.Namespace);
            }

            var firstParameter = @event.Parameters.FirstOrDefault();

            // Generate case statement
            string awaitCode = @event.ActivationAwaitRequired ? "await " : string.Empty;
            foldCode.Append($$"""
                              case "{{@event.EventName}}":
                                {{awaitCode}} {{@event.ActivationType}}(
                              """);

            // If there's a first parameter, generate the event argument
            if (firstParameter != null)
            {
                if (firstParameter.Type == @event.TypeName && firstParameter.Type != "IEvent")
                {
                    foldCode.Append(
                        $"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}).Data()");
                }
                else if (firstParameter.Type == "IEvent")
                {
                    foldCode.Append(
                        $"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName})");
                }

                if (@event.Parameters.Count > 1)
                {
                    foldCode.Append(", ");
                }

                // Generate parameter arguments using versionToken instead of document
                // Use WhenParameterDeclarations which has correct parameters (already skipped event param if present)
                var parametersToGenerate = @event.WhenParameterDeclarations;
                for (int i = 0; i < parametersToGenerate.Count; i++)
                {
                    var paramDecl = parametersToGenerate[i];

                    // For custom parameters that use WhenParameterValueFactory, use version token variant
                    if (paramDecl.Type != "IEvent" && paramDecl.Type != "IObjectDocument" && !paramDecl.IsExecutionContext)
                    {
                        foldCode.Append($"GetWhenParameterValue<{paramDecl.Type}, {@event.TypeName}>({Environment.NewLine}\t\t\t\"{paramDecl.Type}\",{Environment.NewLine}\t\t\tversionToken, @event)!");
                    }
                    else if (paramDecl.Type == "IObjectDocument")
                    {
                        // If they really need the document, we don't have it in this context - this shouldn't happen
                        // but we'll add a comment
                        foldCode.Append($"null! /* Document not available in version token fold */");
                    }
                    else if (paramDecl.Type == "IEvent")
                    {
                        foldCode.Append($"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName})");
                    }
                    else if (paramDecl.IsExecutionContext)
                    {
                        // Pass parentContext, cast to the specific type if not IExecutionContext
                        if (paramDecl.Type == "IExecutionContext" || paramDecl.Type.StartsWith("IExecutionContext"))
                        {
                            foldCode.Append($"parentContext");
                        }
                        else
                        {
                            // Cast to the specific implementation type (e.g., LanguageContext)
                            foldCode.Append($"parentContext as {paramDecl.Type}");
                        }
                    }

                    if (i < parametersToGenerate.Count - 1)
                    {
                        foldCode.Append(", ");
                    }
                }
            }

            foldCode.Append("""
                            );
                            break;
                            """);
        }

        return foldCode.ToString();
    }

    private static StringBuilder SelectBestConstructorAndGenerateCode(ProjectionDefinition projection)
    {
        var ctorCode = new StringBuilder();
        var specialDependencies = new[] { "IObjectDocumentFactory", "IEventStreamFactory" };

        var bestMatch = RankConstructorsByPropertyMatch(projection, specialDependencies).FirstOrDefault();

        if (bestMatch != null)
        {
            GenerateConstructorParameters(bestMatch.Constructor, projection, ctorCode);
        }

        return ctorCode;
    }

    private static List<dynamic> RankConstructorsByPropertyMatch(ProjectionDefinition projection, string[] specialDependencies)
    {
        return projection.Constructors
            .Select(ctor =>
            {
                bool hasAllSpecialDeps = specialDependencies.All(dep =>
                    ctor.Parameters.Any(p => p.Type == dep));

                var regularParams = ctor.Parameters
                    .Where(p => !specialDependencies.Contains(p.Type))
                    .ToList();

                int matchedParamCount = regularParams.Count(param =>
                    projection.Properties.Any(prop =>
                        prop.Type == param.Type &&
                        (string.Equals(prop.Name, param.Name, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(prop.Name, "_" + param.Name, StringComparison.OrdinalIgnoreCase))));

                double matchPercentage = regularParams.Count > 0
                    ? (double)matchedParamCount / regularParams.Count
                    : 0;

                return new {
                    Constructor = ctor,
                    HasRequiredDeps = hasAllSpecialDeps,
                    MatchedCount = matchedParamCount,
                    TotalRegularParams = regularParams.Count,
                    MatchPercentage = matchPercentage
                };
            })
            .Where(result => result.HasRequiredDeps)
            .OrderByDescending(result => result.MatchPercentage)
            .ThenByDescending(result => result.TotalRegularParams)
            .ToList<dynamic>();
    }

    private static void GenerateConstructorParameters(ConstructorDefinition constructor, ProjectionDefinition projection,
        StringBuilder ctorCode)
    {
        foreach (var parameter in constructor.Parameters)
        {
            if (parameter.Type is "IObjectDocumentFactory" or "IEventStreamFactory")
            {
                switch (parameter.Type)
                {
                    case "IObjectDocumentFactory":
                        ctorCode.Append("documentFactory");
                        break;
                    case "IEventStreamFactory":
                        ctorCode.Append("eventStreamFactory");
                        break;
                }
            }
            else
            {
                var name = projection.Properties.FirstOrDefault(p =>
                    p.Name.Equals(parameter.Name, StringComparison.InvariantCultureIgnoreCase))?.Name;
                if (name is null)
                {
                    continue;
                }
                ctorCode.Append("obj." + name);
            }

            if (parameter != constructor.Parameters[^1])
            {
                ctorCode.Append(", ");
            }
        }
    }

    private static string GenerateDeserializationCode(ProjectionDefinition projection, string ctorParams)
    {
        var code = new StringBuilder();

        // Generate variable declarations for each property
        code.AppendLine("// Deserialize each property manually to preserve data");

        foreach (var property in projection.Properties.Where(p => p.Name != "WhenParameterValueFactories" && p.Name != "Checkpoint"))
        {
            var fullTypeDef = BuildFullTypeDefinition(property);
            code.AppendLine($"                                {fullTypeDef}? {ToCamelCase(property.Name)} = null;");
        }

        code.AppendLine("                                Checkpoint checkpoint = [];");
        code.AppendLine();
        code.AppendLine("                                var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));");
        code.AppendLine("                                ");
        code.AppendLine("                                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)");
        code.AppendLine("                                    return null;");
        code.AppendLine();
        code.AppendLine("                                while (reader.Read())");
        code.AppendLine("                                {");
        code.AppendLine("                                    if (reader.TokenType == JsonTokenType.EndObject)");
        code.AppendLine("                                        break;");
        code.AppendLine();
        code.AppendLine("                                    if (reader.TokenType != JsonTokenType.PropertyName)");
        code.AppendLine("                                        continue;");
        code.AppendLine();
        code.AppendLine("                                    string? propertyName = reader.GetString();");
        code.AppendLine("                                    reader.Read();");
        code.AppendLine();
        code.AppendLine("                                    switch (propertyName)");
        code.AppendLine("                                    {");

        // Generate case for each property
        foreach (var property in projection.Properties.Where(p => p.Name != "WhenParameterValueFactories"))
        {
            var jsonPropertyName = property.Name switch
            {
                "Checkpoint" => "$checkpoint",
                "CheckpointFingerprint" => "$checkpointFingerprint",
                _ => property.Name
            };
            var varName = property.Name == "Checkpoint" ? "checkpoint" : ToCamelCase(property.Name);
            var fullTypeDef = BuildFullTypeDefinition(property);

            code.AppendLine($"                                        case \"{jsonPropertyName}\":");

            if (property.Name == "Checkpoint")
            {
                code.AppendLine($"                                            {varName} = JsonSerializer.Deserialize<{fullTypeDef}>(ref reader, {projection.Name}JsonSerializerContext.Default.Options) ?? [];");
            }
            else
            {
                code.AppendLine($"                                            {varName} = JsonSerializer.Deserialize<{fullTypeDef}>(ref reader, {projection.Name}JsonSerializerContext.Default.Options);");
            }

            code.AppendLine("                                            break;");
        }

        code.AppendLine("                                    }");
        code.AppendLine("                                }");
        code.AppendLine();
        code.AppendLine($"                                // Create instance with factories and deserialized properties");

        // Build constructor call with parameters
        var ctorParamsWithValues = ctorParams;
        if (ctorParams.Contains("documentFactory, eventStreamFactory"))
        {
            // Extract additional parameters beyond the standard factories
            var extraParams = new List<string>();
            foreach (var property in projection.Properties.Where(p => p.Name != "WhenParameterValueFactories" && p.Name != "Checkpoint" && p.Name != "CheckpointFingerprint"))
            {
                var varName = ToCamelCase(property.Name);
                var paramMatch = $", {varName}";
                if (ctorParams.Contains(paramMatch))
                {
                    extraParams.Add($", {varName}");
                }
            }

            if (extraParams.Any())
            {
                ctorParamsWithValues = "documentFactory, eventStreamFactory" + string.Join("", extraParams.Select(p => $"{p}!"));
            }
        }

        code.AppendLine($"                                var instance = new {projection.Name}({ctorParamsWithValues});");
        code.AppendLine();

        // Generate code to populate each property (skip those that were constructor parameters)
        foreach (var property in projection.Properties.Where(p => p.Name != "WhenParameterValueFactories" && p.Name != "Checkpoint"))
        {
            var varName = ToCamelCase(property.Name);

            // Skip if this property was passed to constructor
            if (ctorParams.Contains($", {varName}"))
                continue;

            if (property.Type == "Dictionary")
            {
                code.AppendLine($"                                if ({varName} != null)");
                code.AppendLine("                                {");
                code.AppendLine($"                                    foreach (var kvp in {varName})");
                code.AppendLine("                                    {");
                code.AppendLine($"                                        instance.{property.Name}[kvp.Key] = kvp.Value;");
                code.AppendLine("                                    }");
                code.AppendLine("                                }");
            }
            else if (property.Type == "List")
            {
                code.AppendLine($"                                if ({varName} != null)");
                code.AppendLine("                                {");
                code.AppendLine($"                                    instance.{property.Name}.Clear();");
                code.AppendLine($"                                    instance.{property.Name}.AddRange({varName});");
                code.AppendLine("                                }");
            }
        }

        code.AppendLine("                                instance.Checkpoint = checkpoint;");
        code.AppendLine("                                instance.CheckpointFingerprint = checkpointFingerprint;");
        code.AppendLine();
        code.AppendLine("                                return instance;");

        return code.ToString();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string GenerateCreateDestinationInstanceMethod(ProjectionDefinition projection)
    {
        // Check if this projection inherits from RoutedProjection
        // We'll look at the properties to see if it has Destinations and Registry properties
        var hasDestinations = projection.Properties.Any(p => p.Name == "Destinations");
        var hasRegistry = projection.Properties.Any(p => p.Name == "Registry");

        if (!hasDestinations || !hasRegistry)
        {
            // Not a routed projection, don't generate the method
            return string.Empty;
        }

        // Extract destination types from the When method event definitions
        // Look for patterns like AddDestination<ProjectKanbanDestination> in the source
        var destinationTypes = new HashSet<string>();

        // Get all destination types from the DestinationPathTemplates dictionary
        if (projection is RoutedProjectionDefinition routedProjection)
        {
            foreach (var destType in routedProjection.DestinationPathTemplates.Keys)
            {
                destinationTypes.Add(destType);
            }
        }

        if (destinationTypes.Count == 0)
        {
            // No destination types found, generate a simple implementation
            return string.Empty;
        }

        var code = new StringBuilder();
        code.AppendLine();
        code.AppendLine("/// <summary>");
        code.AppendLine("/// Creates a destination instance with proper initialization.");
        code.AppendLine("/// AOT-compatible implementation without reflection.");
        code.AppendLine("/// </summary>");
        code.AppendLine("protected override TDestination CreateDestinationInstance<TDestination>(string destinationKey)");
        code.AppendLine("{");

        // Generate type checks for each destination type
        bool first = true;
        foreach (var destinationType in destinationTypes)
        {
            var keyword = first ? "if" : "else if";
            code.AppendLine($"    {keyword} (typeof(TDestination) == typeof({destinationType}))");
            code.AppendLine("    {");
            code.AppendLine($"        var destination = new {destinationType}(DocumentFactory!, EventStreamFactory!);");
            code.AppendLine("        return (TDestination)(Projection)destination;");
            code.AppendLine("    }");
            first = false;
        }

        code.AppendLine();
        code.AppendLine("    throw new ArgumentException($\"Unknown destination type: {typeof(TDestination).Name}\", nameof(TDestination));");
        code.AppendLine("}");


        return code.ToString();
    }

    private static string GenerateRoutedProjectionSerializationMethods()
    {
        // Routed projections use standard ToJson() and LoadFromJson() methods
        // since the RoutedProjection base class already has proper [JsonPropertyName] and [JsonIgnore] attributes
        // No additional serialization methods needed
        return string.Empty;
    }

    private static string GetFullTypeName(PropertyDefinition prop)
    {
        if (!prop.IsGeneric)
        {
            return string.IsNullOrEmpty(prop.Namespace) ? prop.Type : $"{prop.Namespace}.{prop.Type}";
        }

        var genericArgs = string.Join(", ", prop.GenericTypes.Select(GetFullGenericTypeName));
        return $"{prop.Type}<{genericArgs}>";
    }

    private static string GetFullGenericTypeName(PropertyGenericTypeDefinition genType)
    {
        if (genType.GenericTypes.Count == 0)
        {
            return string.IsNullOrEmpty(genType.Namespace) ? genType.Name : $"{genType.Namespace}.{genType.Name}";
        }

        var genericArgs = string.Join(", ", genType.GenericTypes.Select(GetFullGenericTypeName));
        return $"{genType.Name}<{genericArgs}>";
    }

    private static async Task AssembleAndWriteCode(
        ProjectionDefinition projection,
        List<string> usings,
        ProjectionCodeComponents components,
        string? path,
        string version)
    {
        var code = new StringBuilder();

        foreach (var namespaceName in usings.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Order())
        {
            code.AppendLine($"using {namespaceName};");
        }

        code.AppendLine("");
        code.AppendLine($$"""

                          namespace {{projection.Namespace}};

                          // <auto-generated />
                          [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                          [ExcludeFromCodeCoverage]
                          public partial class {{projection.Name}} : I{{projection.Name}} {

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              public {{projection.Name}}() : base() {
                              }

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              public {{projection.Name}}(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                                : base(documentFactory,eventStreamFactory) {
                              }

                              {{components.FoldMethod}}

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } =
                                    new Dictionary<string, IProjectionWhenParameterValueFactory>(){
                                        {{ components.WhenParameterValueBindingCode }}
                                    };

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              public static {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                              {
                                {{components.DeserializationCode}}
                              }

                              [GeneratedCode("ErikLieben.FA.ES", "{{version}}")]
                              [ExcludeFromCodeCoverage]
                              public override string ToJson()
                              {
                                return JsonSerializer.Serialize(this, {{projection.Name}}JsonSerializerContext.Default.{{projection.Name}});
                              }

                              /// <summary>
                              /// Gets or sets the checkpoint tracking the last processed event position.
                              /// </summary>
                              {{components.CheckpointJsonAnnotation}}
                              public override Checkpoint Checkpoint { get; set; } = [];
                              {{components.CreateDestinationInstanceCode}}
                              {{components.RoutedProjectionSerializationCode}}
                          }

                          {{components.JsonBlobFactoryCode}}

                          #nullable enable
                          // <auto-generated />
                          /// <summary>
                          /// Interface defining the public state properties of {{projection.Name}}.
                          /// </summary>
                          public interface I{{projection.Name}} {
                                {{components.PropertyCode}}
                          }
                          #nullable restore

                          {{components.SerializableCode}}
                          // <auto-generated />
                          /// <summary>
                          /// JSON serializer context for {{projection.Name}} types.
                          /// </summary>
                          internal partial class {{projection.Name}}JsonSerializerContext : JsonSerializerContext
                          {
                          }

                          """);

        var directory = Path.GetDirectoryName(path!);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var projectDir = CodeFormattingHelper.FindProjectDirectory(path!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString(), projectDir));
    }

    private static void GenerateWhenMethods(
        ProjectionEventDefinition @event,
        List<string> usings,
        List<string> whenParameterDeclarations,
        StringBuilder foldCode)
    {
        // Note: ActivationType is the method name (e.g., "When" or "MarkAsDeleted")
        // We process all methods that were detected as When handlers

        if (!usings.Contains(@event.Namespace))
        {
            usings.Add(@event.Namespace);
        }

        var firstParameter = @event.Parameters.FirstOrDefault();

        // Support methods with [When<TEvent>] attribute that have no parameters
        AppendWhenMethodHeader(@event, firstParameter, foldCode);
        RegisterParameterFactories(@event, usings, whenParameterDeclarations);
        var whenLookups = BuildExecutionContextLookups(@event, usings);
        AppendParameterArguments(@event, whenLookups, foldCode);

        foldCode.Append("""
                        );
                        break;
                        """);
    }

    private static void AppendWhenMethodHeader(
        ProjectionEventDefinition @event,
        ParameterDefinition? firstParameter,
        StringBuilder foldCode)
    {
        string awaitCode = @event.ActivationAwaitRequired ? "await " : string.Empty;
        foldCode.Append($$"""
                          case "{{@event.EventName}}":
                            {{awaitCode}} {{@event.ActivationType}}(
                          """);

        // If there's a first parameter, generate the event argument
        if (firstParameter != null)
        {
            if (firstParameter.Type == @event.TypeName && firstParameter.Type != IEventTypeName)
            {
                foldCode.Append(
                    $"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}).Data()");
            }
            else if (firstParameter.Type == IEventTypeName)
            {
                foldCode.Append(
                    $"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName})");
            }

            if (@event.Parameters.Count > 1)
            {
                foldCode.Append(", ");
            }
        }
    }

    private static void RegisterParameterFactories(
        ProjectionEventDefinition @event,
        List<string> usings,
        List<string> whenParameterDeclarations)
    {
        foreach (var parameterFactory in @event.WhenParameterValueFactories)
        {
            // Normalize type name to match what's used in WhenParameterDeclarations
            // "string" -> "String", "int" -> "Int32", etc.
            var normalizedTypeName = NormalizeTypeName(parameterFactory.ForType.Type, parameterFactory.ForType.Namespace);

            var toAdd = $"{{\"{normalizedTypeName}\", new {parameterFactory.Type.Type}()}}";
            if (!whenParameterDeclarations.Contains(toAdd))
            {
                whenParameterDeclarations.Add(toAdd);
            }

            if (!usings.Contains(parameterFactory.ForType.Namespace))
            {
                usings.Add(parameterFactory.ForType.Namespace);
            }
        }
    }

    private static string NormalizeTypeName(string typeName, string typeNamespace)
    {
        // Map C# keyword aliases to their CLR type names
        // This ensures consistent dictionary keys for lookups
        if (typeNamespace == "System")
        {
            return typeName switch
            {
                "string" => "String",
                "int" => "Int32",
                "long" => "Int64",
                "short" => "Int16",
                "byte" => "Byte",
                "sbyte" => "SByte",
                "uint" => "UInt32",
                "ulong" => "UInt64",
                "ushort" => "UInt16",
                "bool" => "Boolean",
                "decimal" => "Decimal",
                "double" => "Double",
                "float" => "Single",
                "char" => "Char",
                "object" => "Object",
                _ => typeName
            };
        }

        return typeName;
    }

    private static Dictionary<string, string> BuildExecutionContextLookups(
        ProjectionEventDefinition @event,
        List<string> usings)
    {
        var whenLookups = new Dictionary<string, string>();

        foreach (var parameterDeclaration in @event.WhenParameterDeclarations)
        {
            var lookupCode = GetExecutionContextCode(parameterDeclaration, @event, usings);
            whenLookups.Add(parameterDeclaration.Type, lookupCode);
        }

        return whenLookups;
    }

    private static string GetExecutionContextCode(
        WhenParameterDeclaration parameterDeclaration,
        ProjectionEventDefinition @event,
        List<string> usings)
    {
        // Check if it's an IExecutionContext implementation type (like LanguageContext)
        if (parameterDeclaration.IsExecutionContext)
        {
            // For IExecutionContext interface types, build the full context wrapper
            if (parameterDeclaration.Type == "IExecutionContextWithData")
            {
                return BuildExecutionContextWithDataCode(parameterDeclaration, @event);
            }
            if (parameterDeclaration.Type == "IExecutionContext" || parameterDeclaration.Type == "IExecutionContextWithEvent")
            {
                return BuildExecutionContextCode(@event);
            }
            // For concrete implementation types (like LanguageContext), just cast parentContext
            usings.Add(parameterDeclaration.Namespace);
            return $"parentContext as {parameterDeclaration.Type}";
        }

        return BuildCustomParameterLookupCode(parameterDeclaration, @event, usings);
    }

    private static string BuildExecutionContextWithDataCode(
        WhenParameterDeclaration parameterDeclaration,
        ProjectionEventDefinition @event)
    {
        var lastGenericType = parameterDeclaration.GenericArguments.LastOrDefault()?.Type ?? "object";
        return Environment.NewLine +
               $"\t\tnew ExecutionContext<{@event.TypeName},{lastGenericType}>({
                   Environment.NewLine
               }\t\t\tJsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}),{
                   Environment.NewLine
               }\t\t\tdocument,{
                   Environment.NewLine
               }\t\t\tdata as {lastGenericType},{
                   Environment.NewLine
               }\t\t\tparentContext as IExecutionContextWithData<{lastGenericType}>)";
    }

    private static string BuildExecutionContextCode(ProjectionEventDefinition @event)
    {
        return Environment.NewLine +
               $"\t\tnew ExecutionContext<{@event.TypeName},object>({
                   Environment.NewLine
               }\t\t\tJsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}),{
                   Environment.NewLine
               }\t\t\tdocument,{
                   Environment.NewLine
               }\t\t\tnull!,{
                   Environment.NewLine
               }\t\t\tparentContext as IExecutionContext<{@event.TypeName},object>)";
    }

    private static string BuildCustomParameterLookupCode(
        WhenParameterDeclaration parameterDeclaration,
        ProjectionEventDefinition @event,
        List<string> usings)
    {
        usings.Add(parameterDeclaration.Namespace);
        // The parameterDeclaration.Type already contains the full type name (e.g., "Demo.App.Events.SomeType" or "String")
        return $"GetWhenParameterValue<{parameterDeclaration.Type}, {@event.TypeName}>({Environment.NewLine}\t\t\t\"{parameterDeclaration.Type}\",{Environment.NewLine}\t\t\tdocument, @event)!";
    }

    private static void AppendParameterArguments(
        ProjectionEventDefinition @event,
        Dictionary<string, string> whenLookups,
        StringBuilder foldCode)
    {
        // Use WhenParameterDeclarations which already has the correct parameters
        // (skipped appropriately based on whether there's an event parameter)
        var parametersToGenerate = @event.WhenParameterDeclarations;

        for (int i = 0; i < parametersToGenerate.Count; i++)
        {
            var paramDecl = parametersToGenerate[i];

            if (whenLookups.TryGetValue(paramDecl.Type, out var lookupCode))
            {
                foldCode.Append(lookupCode);
            }
            else
            {
                // Find the matching parameter from @event.Parameters for AppendParameterByType
                var matchingParam = @event.Parameters.FirstOrDefault(p => p.Type == paramDecl.Type);
                if (matchingParam != null)
                {
                    AppendParameterByType(matchingParam, @event, foldCode);
                }
            }

            if (i < parametersToGenerate.Count - 1)
            {
                foldCode.Append(", ");
            }
        }
    }

    private static void AppendParameterByType(
        ParameterDefinition parameter,
        ProjectionEventDefinition @event,
        StringBuilder foldCode)
    {
        switch (parameter.Type)
        {
            case IEventTypeName:
                foldCode.Append(
                    $"JsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName})");
                break;
            case "IObjectDocument":
                foldCode.Append($"document");
                break;
            default:
                if (parameter.Type.StartsWith("IExecutionContext"))
                {
                    foldCode.Append($"parentContext");
                }
                break;
        }
    }


}
