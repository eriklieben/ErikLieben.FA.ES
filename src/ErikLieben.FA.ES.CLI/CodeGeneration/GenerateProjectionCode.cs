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
                await GenerateProjection(projection, path, config);
            }
        }
    }

    private async Task GenerateProjection(ProjectionDefinition projection, string? path, Config config)
    {
        var usings = InitializeUsings(projection);
        var (foldCode, whenParameterDeclarations) = GenerateWhenMethodsForProjection(projection, usings);
        var (serializableCode, newJsonSerializableCode) = GenerateJsonSerializationCode(projection);
        var (propertyCode, propertySnapshotCode) = GeneratePropertyCode(projection);
        var jsonBlobFactoryCode = GenerateBlobFactoryCode(projection, usings);
        var whenParameterValueBindingCode = GenerateWhenParameterBindingCode(whenParameterDeclarations);
        var postWhenCode = GeneratePostWhenCode(projection);
        var postWhenAllDummyCode = GeneratePostWhenAllDummyCode(projection, usings);
        var foldMethod = GenerateFoldMethod(projection, foldCode, postWhenCode, postWhenAllDummyCode);
        var ctorCode = SelectBestConstructorAndGenerateCode(projection);
        var checkpointJsonAnnotation = projection.ExternalCheckpoint ? "[JsonIgnore]" : "[JsonPropertyName(\"$checkpoint\")]";

        await AssembleAndWriteCode(projection, usings, foldMethod, whenParameterValueBindingCode, ctorCode,
            checkpointJsonAnnotation, jsonBlobFactoryCode, propertyCode, serializableCode, path);
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

    private (StringBuilder foldCode, List<string> whenParameterDeclarations) GenerateWhenMethodsForProjection(
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

        serializableCode.AppendLine($"[JsonSerializable(typeof({projection.Name}))]");
        return (serializableCode, newJsonSerializableCode);
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

    private static string GenerateBlobFactoryCode(ProjectionDefinition projection, List<string> usings)
    {
        if (projection.BlobProjection == null)
        {
            return string.Empty;
        }

        usings.Add("ErikLieben.FA.ES.AzureStorage.Blob");
        usings.Add("Microsoft.Extensions.Azure");
        usings.Add("Azure.Storage.Blobs");

        return $$"""
                 public class {{projection.Name}}Factory(
                     IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
                     IObjectDocumentFactory objectDocumentFactory,
                     IEventStreamFactory eventStreamFactory)
                     : BlobProjectionFactory<{{projection.Name}}>(
                         blobServiceClientFactory,
                         "{{projection.BlobProjection.Connection}}",
                         "{{projection.BlobProjection.Container}}")
                 {
                     protected override bool HasExternalCheckpoint => {{projection.ExternalCheckpoint.ToString().ToLowerInvariant()}};

                     protected override {{projection.Name}} New()
                     {
                         return new {{projection.Name}}(objectDocumentFactory, eventStreamFactory);
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

    private static async Task AssembleAndWriteCode(ProjectionDefinition projection, List<string> usings,
        string foldMethod, string whenParameterValueBindingCode, StringBuilder ctorCode,
        string checkpointJsonAnnotation, string jsonBlobFactoryCode, StringBuilder propertyCode,
        StringBuilder serializableCode, string? path)
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

                              {{foldMethod}}

                              protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } =
                                    new Dictionary<string, IProjectionWhenParameterValueFactory>(){
                                        {{ whenParameterValueBindingCode }}
                                    };

                              public static {{projection.Name}}? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
                              {
                                var obj = JsonSerializer.Deserialize(json, {{projection.Name}}JsonSerializerContext.Default.{{projection.Name}});
                                if (obj is null) {
                                    return null;
                                }
                                return new {{projection.Name}}({{ctorCode}});
                              }


                              public override string ToJson()
                              {
                                return JsonSerializer.Serialize(this, {{projection.Name}}JsonSerializerContext.Default.{{projection.Name}});
                              }

                              {{checkpointJsonAnnotation}}
                              public override Checkpoint Checkpoint { get; set; } = [];
                          }

                          {{jsonBlobFactoryCode}}

                          #nullable enable
                          // <auto-generated />
                          public interface I{{projection.Name}} {
                                {{propertyCode}}
                          }
                          #nullable restore

                          {{serializableCode}}
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
            var toAdd = $"{{\"{parameterFactory.ForType.Type}\", new {parameterFactory.Type.Type}()}}";
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
        return $"GetWhenParameterValue<{parameterDeclaration.Type}, {@event.TypeName}>({Environment.NewLine}\t\t\t\"{parameterDeclaration.Namespace}.{parameterDeclaration.Type}\",{Environment.NewLine}\t\t\tdocument, @event)!";
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

//     private static void GenerateWhenMethods22(
//         ProjectionEventDefinition @event,
//         List<string> usings,
//         List<string> whenParameterDeclarations,
//         StringBuilder foldCode)
//     {
//         if (@event.ActivationType == "When")
//         {
//             // When
//             if (!usings.Contains(@event.Namespace))
//             {
//                 usings.Add(@event.Namespace);
//             }
//
//             // TODO: we would first require the params as well
//             foreach (var parameterFactory in @event.WhenParameterValueFactories)
//             {
//                 var toAdd = $"{{\"{parameterFactory.ForType.Type}\", new {parameterFactory.Type.Type}()}}";
//                 if (!whenParameterDeclarations.Contains(toAdd))
//                 {
//                     whenParameterDeclarations.Add(toAdd);
//                 }
//
//                 if (!usings.Contains(parameterFactory.ForType.Namespace))
//                 {
//                     usings.Add(parameterFactory.ForType.Namespace);
//                 }
//             }
//
//             if (@event.WhenParameterDeclarations.Count == 0)
//             {
//
//                 // do what we did before...
//                 foldCode.AppendLine($$"""
//                                       case "{{@event.EventName}}":
//                                           {{(@event.ActivationAwaitRequired ? "await " : string.Empty)}}When({{Environment.NewLine + "\t\t"}}JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
//                                       break;
//                                       """);
//             }
//             else
//             {
//
//                 if (@event.WhenParameterDeclarations.Count == 1 &&
//                     @event.WhenParameterDeclarations.First().Type == @event.TypeName)
//                 {
//                     // do what we did before...
//                     foldCode.AppendLine($$"""
//                                           case "{{@event.EventName}}":
//                                               {{(@event.ActivationAwaitRequired ? "await " : string.Empty)}}When({{Environment.NewLine + "\t\t"}}JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}));
//                                           break;
//                                           """);
//                 }
//                 else
//                 {
//                     // case "FeatureFlag.Enabled":
//                     //     var identifier = GetWhenParameterValue<FooIdentifier, FeatureFlagEnabled>("DemoApp.Projections.FooIdentifier", document, @event);
//                     //     When(JsonEvent.To(@event, FeatureFlagEnabledJsonSerializerContext.Default.FeatureFlagEnabled), identifier);
//                     //     break;
//
//
//                     string codeee = string.Empty;
//                     foreach (var parameterDeclaration in @event.WhenParameterDeclarations)
//                     {
//                         if (@event.TypeName == parameterDeclaration.Type)
//                         {
//                             continue;
//                         }
//
//                         if (parameterDeclaration.Type.StartsWith("IExecutionContextWithData"))
//                         {
//                             var x = parameterDeclaration.GenericArguments.LastOrDefault();
//                             if (x is not null)
//                             {
//                                 codeee += Environment.NewLine +
//                                           $"\t\tnew ExecutionContext<{@event.TypeName},{x.Type}>({
//                                               Environment.NewLine
//                                           }\t\t\tJsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}),{
//                                               Environment.NewLine
//                                           }\t\t\tdocument,{
//                                               Environment.NewLine
//                                           }\t\t\tdata as {x.Type},{
//                                               Environment.NewLine
//                                           }\t\t\tparentContext as IExecutionContextWithData<{x.Type}>)";
//                             }
//                         }
//                         else if (parameterDeclaration.Type.StartsWith("IExecutionContextWithEvent"))
//                         {
//                             codeee += Environment.NewLine +
//                                       $"\t\tnew ExecutionContext<{@event.TypeName},object>({
//                                           Environment.NewLine
//                                       }\t\t\tJsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}),{
//                                           Environment.NewLine
//                                       }\t\t\tdocument,{
//                                           Environment.NewLine
//                                       }\t\t\tnull!,{
//                                           Environment.NewLine
//                                       }\t\t\tparentContext as IExecutionContext<{@event.TypeName},object>)";
//                         }
//                         else if (parameterDeclaration.Type.StartsWith("IExecutionContext"))
//                         {
//                             codeee += Environment.NewLine +
//                                       $"\t\tnew ExecutionContext<{@event.TypeName},object>({
//                                           Environment.NewLine
//                                       }\t\t\tJsonEvent.ToEvent(@event, {@event.TypeName}JsonSerializerContext.Default.{@event.TypeName}),{
//                                           Environment.NewLine
//                                       }\t\t\tdocument,{
//                                           Environment.NewLine
//                                       }\t\t\tnull!,{
//                                           Environment.NewLine
//                                       }\t\t\tparentContext as IExecutionContext<{@event.TypeName},object>)";
//                         }
//                         else
//                         {
//                             codeee += Environment.NewLine +
//                                       $"\t\tGetWhenParameterValue<{parameterDeclaration.Type}, {@event.TypeName}>({Environment.NewLine}\t\t\t\"{parameterDeclaration.Namespace}.{parameterDeclaration.Type}\",{Environment.NewLine}\t\t\tdocument, @event)!";
//                         }
//
//                         if (parameterDeclaration != @event.WhenParameterDeclarations.Last())
//                         {
//                             codeee += ", ";
//                         }
//                     }
//
//                     foldCode.AppendLine($$"""
//
//                                           case "{{@event.EventName}}":
//                                               {{(@event.ActivationAwaitRequired ? "await " : string.Empty)}}When(JsonEvent.To(@event, {{@event.TypeName}}JsonSerializerContext.Default.{{@event.TypeName}}), {{codeee}});
//                                           break;
//                                           """);
//                 }
//             }
//
//         }
//     }

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
