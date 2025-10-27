using System.Text;
using System.Text.RegularExpressions;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Generates aggregate factory and System.Text.Json source generation context code for a target solution.
/// </summary>
public partial class GenerateExtensionCode
{
    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    /// <summary>
/// Initializes a new instance of the <see cref="GenerateExtensionCode"/> class.
/// </summary>
/// <param name="solution">The parsed solution definition describing projects, aggregates, and events.</param>
/// <param name="config">The CLI configuration influencing generation behavior.</param>
/// <param name="solutionPath">The absolute path to the solution root where files are written.</param>
public GenerateExtensionCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    /// <summary>
/// Generates extension registration classes and JSON serializer contexts for all eligible projects in the solution.
/// </summary>
/// <returns>A task that represents the asynchronous generation operation.</returns>
public async Task Generate()
    {

        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            AnsiConsole.MarkupLine($"Generating extension registration for: [violet]{project.Name}[/]");

            var currentFile = project.FileLocation;
            if (currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rel = currentFile.Replace('\\', '/');
            var relGen = rel.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                ? string.Concat(rel.AsSpan(0, rel.Length - ".csproj".Length), "Extensions.Generated.cs")
                : rel + "Extensions.Generated.cs";
            var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            var path = System.IO.Path.Combine(solutionPath, normalized);
            AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");

            try
            {
                await GenerateExtension(project, path);
            }
            catch (Exception)
            {
                // Swallowing exceptions here is intentional to continue generation for other projects; consider logging if needed.
            }

        }
    }

    private static async Task GenerateExtension(ProjectDefinition project, string path)
    {
        var projectName = ProjectNameRegex().Replace(project.Name, string.Empty);

        var registerCode = new StringBuilder();
        var mappingCode = new StringBuilder();

        var (jsonSerializerCode, jsonNamespaces) = GenerateJsonSerializers(project);

        foreach (var declaration in project.Aggregates)
        {
            if (!declaration.IsPartialClass)
            {
                continue;
            }

            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<I{declaration.IdentifierName}Factory, {declaration.IdentifierName}Factory>();");
            mappingCode.AppendLine(
                $"Type agg when agg == typeof({declaration.IdentifierName}) => typeof(IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>),");
        }


        foreach (var declaration in project.InheritedAggregates)
        {
            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>, {declaration.IdentifierName}Factory>();");
            registerCode.AppendLine(
                $"serviceCollection.AddSingleton<I{declaration.IdentifierName}Factory, {declaration.IdentifierName}Factory>();");
            mappingCode.AppendLine(
                $"Type agg when agg == typeof({declaration.IdentifierName}) => typeof(IAggregateFactory<{declaration.IdentifierName}, {declaration.IdentifierType}>),");
        }

        var ss = new StringBuilder();
        foreach (var @namespace in jsonNamespaces.Distinct().Order())
        {
            ss.AppendLine($"using {@namespace};");
        }

        var code = $$"""
                     using ErikLieben.FA.ES.Aggregates;
                     using Microsoft.Extensions.DependencyInjection;
                     using System.Text.Json.Serialization;
                     {{ss}}

                     namespace {{project.Namespace}};

                     // <auto-generated />
                     public partial class {{projectName}}Factory : AggregateFactory, IAggregateFactory
                     {
                         public {{projectName}}Factory(IServiceProvider serviceProvider) : base(serviceProvider)
                         {
                     
                         }
                     
                         public static void Register(IServiceCollection serviceCollection)
                         {
                             {{registerCode}}
                             serviceCollection.AddSingleton<IAggregateFactory, {{projectName}}Factory>();
                         }
                     
                         public static Type Get(Type type)
                         {
                             return type switch
                             {
                                 {{mappingCode}}
                                 _ => null!
                             };
                         }
                     
                         protected override Type InternalGet(Type type)
                         {
                             return Get(type);
                         }
                     }

                     // <auto-generated />
                     public static class {{projectName}}Extensions
                     {
                         public static IServiceCollection Configure{{projectName}}Factory(this IServiceCollection services)
                         {
                             {{projectName}}Factory.Register(services);
                             return services;
                         }
                     }
                     
                     {{jsonSerializerCode}}
                     """;

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        await File.WriteAllTextAsync(path!, FormatCode(code.ToString()));
    }

    private static (StringBuilder, List<string>) GenerateJsonSerializers(ProjectDefinition project)
    {
        var events = CollectDistinctEvents(project);
        var nameSpaceUsings = new List<string>();
        var jsonSerializerCode = new StringBuilder();

        foreach (var eventDefinition in events)
        {
            ProcessEventDefinition(eventDefinition, nameSpaceUsings, jsonSerializerCode);
        }

        return (jsonSerializerCode, nameSpaceUsings);
    }

    private static List<EventDefinition> CollectDistinctEvents(ProjectDefinition project)
    {
        return project.Aggregates
            .SelectMany(agg => agg.Events)
            .Concat(project.Projections.SelectMany(proj => proj.Events))
            .DistinctBy(e => e.EventName)
            .ToList();
    }

    private static void ProcessEventDefinition(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        StringBuilder jsonSerializerCode)
    {
        var jsonSerializerCodeList = new List<string>();

        nameSpaceUsings.Add(eventDefinition.Namespace);
        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({eventDefinition.TypeName}))]");

        ProcessEventProperties(eventDefinition, nameSpaceUsings, jsonSerializerCodeList);
        ProcessEventParameters(eventDefinition, nameSpaceUsings, jsonSerializerCodeList);

        AppendSerializerContext(eventDefinition, jsonSerializerCodeList, jsonSerializerCode);
    }

    private static void ProcessEventProperties(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var prop in eventDefinition.Properties)
        {
            nameSpaceUsings.Add(prop.Namespace);

            if (prop.IsGeneric)
            {
                ProcessGenericProperty(prop, nameSpaceUsings, jsonSerializerCodeList);
            }
            else
            {
                jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}))]");
            }

            ProcessPropertySubTypes(prop.SubTypes, nameSpaceUsings, jsonSerializerCodeList);
        }
    }

    private static void ProcessGenericProperty(
        PropertyDefinition prop,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        var genericSignature = BuildGenericTypeSignature(prop.GenericTypes, nameSpaceUsings);
        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}<{genericSignature}>))]");

        // Process nested subtypes within generic types
        foreach (var generic in prop.GenericTypes)
        {
            foreach (var subType2 in generic.SubTypes)
            {
                nameSpaceUsings.Add(subType2.Namespace);
                AddSerializerForType(subType2, jsonSerializerCodeList, nameSpaceUsings);
            }
        }
    }

    private static void ProcessPropertySubTypes(
        List<PropertyGenericTypeDefinition> subTypes,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var subType in subTypes)
        {
            nameSpaceUsings.Add(subType.Namespace);
            AddSerializerForType(subType, jsonSerializerCodeList, nameSpaceUsings);
        }
    }

    private static void AddSerializerForType(
        PropertyGenericTypeDefinition type,
        List<string> jsonSerializerCodeList,
        List<string> nameSpaceUsings)
    {
        if (type.GenericTypes.Count != 0)
        {
            var genericSignature = BuildGenericTypeSignature(type.GenericTypes, nameSpaceUsings);
            jsonSerializerCodeList.Add($"[JsonSerializable(typeof({type.Name}<{genericSignature}>))]");
        }
        else
        {
            jsonSerializerCodeList.Add($"[JsonSerializable(typeof({type.Name}))]");
        }
    }

    private static string BuildGenericTypeSignature(
        List<PropertyGenericTypeDefinition> genericTypes,
        List<string> nameSpaceUsings)
    {
        var builder = new StringBuilder();

        for (int i = 0; i < genericTypes.Count; i++)
        {
            var generic = genericTypes[i];
            nameSpaceUsings.Add(generic.Namespace);
            builder.Append(generic.Name);

            if (i < genericTypes.Count - 1)
            {
                builder.Append(", ");
            }
        }

        return builder.ToString();
    }

    private static void ProcessEventParameters(
        EventDefinition eventDefinition,
        List<string> nameSpaceUsings,
        List<string> jsonSerializerCodeList)
    {
        foreach (var parameter in eventDefinition.Parameters)
        {
            foreach (var subType in parameter.SubTypes)
            {
                if (!nameSpaceUsings.Contains(subType.Namespace))
                {
                    nameSpaceUsings.Add(subType.Namespace);
                }

                if (parameter.Type != subType.Name)
                {
                    jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}))]");
                }
            }
        }
    }

    private static void AppendSerializerContext(
        EventDefinition eventDefinition,
        List<string> jsonSerializerCodeList,
        StringBuilder jsonSerializerCode)
    {
        var filteredSerializers = jsonSerializerCodeList
            .Distinct()
            .Where(i =>
                !i.StartsWith("[JsonSerializable(typeof(IList<") &&
                !i.StartsWith("[JsonSerializable(typeof(List<") &&
                !i.StartsWith("[JsonSerializable(typeof(Collection<"))
            .Order();

        jsonSerializerCode.Append(string.Join(Environment.NewLine, filteredSerializers));
        jsonSerializerCode.AppendLine("");
        jsonSerializerCode.AppendLine("// <auto-generated />");
        jsonSerializerCode.AppendLine(
            $"internal partial class {eventDefinition.TypeName}JsonSerializerContext : JsonSerializerContext {{ }}");
        jsonSerializerCode.AppendLine("");
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

    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex ProjectNameRegex();
}
