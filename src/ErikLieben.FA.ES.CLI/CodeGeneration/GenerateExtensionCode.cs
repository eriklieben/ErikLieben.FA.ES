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
                await GenerateExtension(project, path, config);
            }
            catch (Exception)
            {
                // Swallowing exceptions here is intentional to continue generation for other projects; consider logging if needed.
            }

        }
    }

    private static async Task GenerateExtension(ProjectDefinition project, string path, Config config1)
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


        var events = project.Aggregates
            .SelectMany(agg => agg.Events)
            .Concat(project.Projections.SelectMany(proj => proj.Events))
            .DistinctBy(e => e.EventName)
            .ToList();



        var nameSpaceUsigns = new List<string>();
        var jsonSerializerCode = new StringBuilder();
        foreach (var eventDefinition in events)
        {
            var jsonSerializerCodeList = new List<string>();

            nameSpaceUsigns.Add(eventDefinition.Namespace);
            jsonSerializerCodeList.Add($"[JsonSerializable(typeof({eventDefinition.TypeName}))]");
            foreach (var prop in eventDefinition.Properties)
            {
                nameSpaceUsigns.Add(prop.Namespace);
                if (prop.IsGeneric)
                {

                    var genericDefBuilder = new StringBuilder();
                    foreach (var generic in prop.GenericTypes)
                    {
                        nameSpaceUsigns.Add(generic.Namespace);
                        genericDefBuilder.Append(generic.Name);
                        if (prop.GenericTypes[^1] != generic)
                        {
                            genericDefBuilder.Append(", ");
                        }

                        foreach (var subType2 in generic.SubTypes)
                        {
                            nameSpaceUsigns.Add(subType2.Namespace);

                            if (subType2.GenericTypes.Count != 0)
                            {
                                var genericDef2Builder = new StringBuilder();
                                foreach (var generic2 in subType2.GenericTypes)
                                {
                                    nameSpaceUsigns.Add(generic2.Namespace);
                                    genericDef2Builder.Append(generic2.Name);
                                    if (subType2.GenericTypes[^1] != generic2)
                                    {
                                        genericDef2Builder.Append(", ");
                                    }
                                }

                                var genericDef2 = genericDef2Builder.ToString();
                                jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType2.Name}<{genericDef2}>))]");
                            }
                            else
                            {
                                jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType2.Name}))]");
                            }

                        }
                    }

                    var genericDef = genericDefBuilder.ToString();
                    jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}<{genericDef}>))]");
                }
                else
                {
                    jsonSerializerCodeList.Add($"[JsonSerializable(typeof({prop.Type}))]");
                }

                foreach (var subType in prop.SubTypes)
                {
                    nameSpaceUsigns.Add(subType.Namespace);

                    if (subType.GenericTypes.Count != 0)
                    {

                        var genericDefBuilder = new StringBuilder();
                        foreach (var generic in subType.GenericTypes)
                        {
                            nameSpaceUsigns.Add(generic.Namespace);
                            genericDefBuilder.Append(generic.Name);
                            if (subType.GenericTypes[^1] != generic)
                            {
                                genericDefBuilder.Append(", ");
                            }
                        }

                        var genericDef = genericDefBuilder.ToString();
                        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}<{genericDef}>))]");
                    }
                    else
                    {
                        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}))]");
                    }

                   //  nameSpaceUsigns.Add(subType.Namespace);
                   // jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}))]");
                }
            }

            // Add json serializers for parameter types (Temp fix)
            foreach (var parameter in eventDefinition.Parameters)
            {
                foreach (var subType in parameter.SubTypes)
                {
                    if (!nameSpaceUsigns.Contains(subType.Namespace))
                    {
                        nameSpaceUsigns.Add(subType.Namespace);
                    }

                    if (parameter.Type != subType.Name)
                    {
                        jsonSerializerCodeList.Add($"[JsonSerializable(typeof({subType.Name}))]");
                    }
                }
            }

            jsonSerializerCode.Append(string.Join(Environment.NewLine, jsonSerializerCodeList.Distinct().Where(i =>
                !i.StartsWith("[JsonSerializable(typeof(IList<") &&
                !i.StartsWith("[JsonSerializable(typeof(List<") &&
                !i.StartsWith("[JsonSerializable(typeof(Collection<")
                ).Order()));
            jsonSerializerCode.AppendLine("");
            jsonSerializerCode.AppendLine("// <auto-generated />");
            jsonSerializerCode.AppendLine(
                "internal partial class " + eventDefinition.TypeName +
                "JsonSerializerContext : JsonSerializerContext { }");
            jsonSerializerCode.AppendLine("");
        }

        return (jsonSerializerCode, nameSpaceUsigns);
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
