using System.Text;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using ErikLieben.FA.ES.Projections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    string DeserializationCode);

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
        var (foldCode, whenParameterDeclarations) = GenerateWhenMethodsForProjection(projection, usings);
        var (serializableCode, _) = GenerateJsonSerializationCode(projection);
        var (propertyCode, _) = GeneratePropertyCode(projection);
        var (get, ctorInput) = GenerateConstructorParametersForFactory(projection);
        var jsonBlobFactoryCode = GenerateBlobFactoryCode(projection, usings, get, ctorInput);
        var whenParameterValueBindingCode = GenerateWhenParameterBindingCode(whenParameterDeclarations);
        var postWhenCode = GeneratePostWhenCode(projection);
        var postWhenAllDummyCode = GeneratePostWhenAllDummyCode(projection, usings);
        var foldMethod = GenerateFoldMethod(projection, foldCode, postWhenCode, postWhenAllDummyCode);
        var ctorCode = SelectBestConstructorAndGenerateCode(projection);
        var checkpointJsonAnnotation = projection.ExternalCheckpoint ? "[JsonIgnore]" : "[JsonPropertyName(\"$checkpoint\")]";

        var deserializationCode = GenerateDeserializationCode(projection, ctorCode.ToString());

        var codeComponents = new ProjectionCodeComponents(
            foldMethod, whenParameterValueBindingCode, ctorCode, checkpointJsonAnnotation,
            jsonBlobFactoryCode, propertyCode, serializableCode, deserializationCode);

        await AssembleAndWriteCode(projection, usings, codeComponents, path);
    }

    private static List<string> InitializeUsings(ProjectionDefinition projection)
    {
        var usings = new List<string>
        {
            "System.Text.Json.Serialization",
            "ErikLieben.FA.ES",
            "ErikLieben.FA.ES.Projections",
            "ErikLieben.FA.ES.Documents",
            "System.Text.Json",
            "ErikLieben.FA.ES.VersionTokenParts"
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

            if (property.Name != "Checkpoint" && projection.Name != "CheckpointFingerprint" && property.Name != "WhenParameterValueFactories")
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

    private static string GenerateBlobFactoryCode(ProjectionDefinition projection, List<string> usings, string get, string ctorInput)
    {
        if (projection.BlobProjection == null)
        {
            return string.Empty;
        }

        usings.Add("ErikLieben.FA.ES.AzureStorage.Blob");
        usings.Add("Microsoft.Extensions.Azure");
        usings.Add("Azure.Storage.Blobs");

        var newMethodBody = string.IsNullOrEmpty(get)
            ? $"return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});"
            : $"{get}            return new {projection.Name}(objectDocumentFactory, eventStreamFactory{ctorInput});";

        return $$"""
                 public class {{projection.Name}}Factory(
                     IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
                     IObjectDocumentFactory objectDocumentFactory,
                     IEventStreamFactory eventStreamFactory,
                     IServiceProvider serviceProvider)
                     : BlobProjectionFactory<{{projection.Name}}>(
                         blobServiceClientFactory,
                         "{{projection.BlobProjection.Connection}}",
                         "{{projection.BlobProjection.Container}}")
                 {
                     protected override bool HasExternalCheckpoint => {{projection.ExternalCheckpoint.ToString().ToLowerInvariant()}};

                     protected override {{projection.Name}} New()
                     {
                         {{newMethodBody}}
                     }

                     protected override {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                     {
                         return {{projection.Name}}.LoadFromJson(json, documentFactory, eventStreamFactory);
                     }
                 }
                """;
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

    private string GeneratePostWhenAllDummyCode(ProjectionDefinition projection, List<string> usings)
    {
        if (projection.HasPostWhenAllMethod)
        {
            return string.Empty;
        }

        usings.Add("System.CodeDom.Compiler");
        var postWhenAllDummyCode = new StringBuilder();
        postWhenAllDummyCode.Append($"[GeneratedCode(\"ErikLieben.FA.ES\", \"{solution.Generator?.Version}\")]\n");
        postWhenAllDummyCode.Append("protected override Task PostWhenAll(IObjectDocument document) { return Task.CompletedTask; }");

        return postWhenAllDummyCode.ToString();
    }

    private static string GenerateFoldMethod(ProjectionDefinition projection, StringBuilder foldCode,
        string postWhenCode, string postWhenAllDummyCode)
    {
        var isAsync = projection.Events.Any(e => e.ActivationAwaitRequired);
        var asyncKeyword = isAsync ? "async " : string.Empty;
        var returnValue = isAsync ? "" : "Task.CompletedTask";

        return $$$"""
                  #nullable enable
                  public override {{{asyncKeyword}}}Task Fold<T>(IEvent @event, IObjectDocument document, T? data = default(T?), IExecutionContext? parentContext = null) where T : class {



                     switch (@event.EventType)
                     {
                         {{{foldCode}}}
                     }

                     {{{postWhenCode}}}

                     return {{{returnValue}}};
                  }

                  {{{postWhenAllDummyCode}}}
                  #nullable restore
                  """;
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
            var jsonPropertyName = property.Name == "Checkpoint" ? "$checkpoint" : property.Name;
            var varName = property.Name == "Checkpoint" ? "checkpoint" : ToCamelCase(property.Name);
            var fullTypeDef = BuildFullTypeDefinition(property);

            code.AppendLine($"                                        case \"{jsonPropertyName}\":");
            code.AppendLine($"                                            {varName} = JsonSerializer.Deserialize<{fullTypeDef}>(ref reader, {projection.Name}JsonSerializerContext.Default.Options);");
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

    private static async Task AssembleAndWriteCode(
        ProjectionDefinition projection,
        List<string> usings,
        ProjectionCodeComponents components,
        string? path)
    {
        var code = new StringBuilder();

        foreach (var namespaceName in usings.Distinct().Order())
        {
            code.AppendLine($"using {namespaceName};");
        }

        code.AppendLine("");
        code.AppendLine($$"""

                          namespace {{projection.Namespace}};

                          // <auto-generated />
                          public partial class {{projection.Name}} : I{{projection.Name}} {

                              public {{projection.Name}}() : base() {
                              }

                              public {{projection.Name}}(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                                : base(documentFactory,eventStreamFactory) {
                              }

                              {{components.FoldMethod}}

                              protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } =
                                    new Dictionary<string, IProjectionWhenParameterValueFactory>(){
                                        {{ components.WhenParameterValueBindingCode }}
                                    };

                              public static {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                              {
                                {{components.DeserializationCode}}
                              }


                              public override string ToJson()
                              {
                                return JsonSerializer.Serialize(this, {{projection.Name}}JsonSerializerContext.Default.{{projection.Name}});
                              }

                              {{components.CheckpointJsonAnnotation}}
                              public override Checkpoint Checkpoint { get; set; } = [];
                          }

                          {{components.JsonBlobFactoryCode}}

                          #nullable enable
                          // <auto-generated />
                          public interface I{{projection.Name}} {
                                {{components.PropertyCode}}
                          }
                          #nullable restore

                          {{components.SerializableCode}}
                          // <auto-generated />
                          internal partial class {{projection.Name}}JsonSerializerContext : JsonSerializerContext
                          {
                          }

                          """);

        var directory = Path.GetDirectoryName(path!);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(path!, FormatCode(code.ToString()));
    }

    private static void GenerateWhenMethods(
        ProjectionEventDefinition @event,
        List<string> usings,
        List<string> whenParameterDeclarations,
        StringBuilder foldCode)
    {
        if (@event.ActivationType != "When")
        {
            return;
        }

        if (!usings.Contains(@event.Namespace))
        {
            usings.Add(@event.Namespace);
        }

        var firstParameter = @event.Parameters.FirstOrDefault();
        if (firstParameter is null)
        {
            return;
        }

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
        ParameterDefinition firstParameter,
        StringBuilder foldCode)
    {
        string awaitCode = @event.ActivationAwaitRequired ? "await " : string.Empty;
        foldCode.Append($$"""
                          case "{{@event.EventName}}":
                            {{awaitCode}} When(
                          """);

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
        return parameterDeclaration.Type switch
        {
            "IExecutionContextWithData" => BuildExecutionContextWithDataCode(parameterDeclaration, @event),
            "IExecutionContext" or "IExecutionContextWithEvent" => BuildExecutionContextCode(@event),
            _ => BuildCustomParameterLookupCode(parameterDeclaration, @event, usings)
        };
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
        foreach (var parameter in @event.Parameters.Skip(1))
        {
            if (whenLookups.TryGetValue(parameter.Type, out var lookupCode))
            {
                foldCode.Append(lookupCode);
            }
            else
            {
                AppendParameterByType(parameter, @event, foldCode);
            }

            if (@event.Parameters[^1] != parameter)
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
