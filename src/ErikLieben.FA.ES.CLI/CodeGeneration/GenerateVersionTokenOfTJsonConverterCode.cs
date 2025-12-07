using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using ErikLieben.FA.ES.JsonConverters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Generates partial JSON converter classes for version token types, providing Read implementation for deserializing version tokens.
/// </summary>
public class GenerateVersionTokenOfTJsonConverterCode
{

    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateVersionTokenOfTJsonConverterCode"/> class.
    /// </summary>
    /// <param name="solution">The parsed solution model containing version token JSON converter definitions.</param>
    /// <param name="config">The CLI configuration values.</param>
    /// <param name="solutionPath">The absolute path to the solution root directory.</param>
    public GenerateVersionTokenOfTJsonConverterCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    /// <summary>
    /// Generates partial JSON converter class files for all version token converter definitions found in the solution.
    /// Creates .Generated.cs files alongside the original JSON converter definitions.
    /// </summary>
    /// <returns>A task representing the asynchronous code generation operation.</returns>
    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var versionTokenJsonConverter in project.VersionTokenJsonConverterDefinitions)
            {
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [darkcyan]{versionTokenJsonConverter.Name}[/]");
                var currentFile = versionTokenJsonConverter.FileLocations.FirstOrDefault();
                if (currentFile is null || currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rel = (versionTokenJsonConverter.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized);
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");
                await GenerateVersionToken(versionTokenJsonConverter, path, project.VersionTokens, solution.Generator?.Version ?? "1.0.0");
            }

            // Generate json
        }
    }

    /// <summary>
    /// Generates the partial JSON converter class code for a version token converter, implementing the Read method with switch expressions for all version token types.
    /// </summary>
    /// <param name="versionTokenJsonConverter">The version token JSON converter definition to generate code for.</param>
    /// <param name="path">The output file path for the generated code.</param>
    /// <param name="versionTokens">List of all version token definitions in the project for generating type mappings.</param>
    /// <returns>A task representing the asynchronous file write operation.</returns>
    /// <remarks>
    /// The generated code includes a generic Read method that uses a switch expression to instantiate the appropriate version token type based on the generic type parameter.
    /// </remarks>
    private static async Task GenerateVersionToken(VersionTokenJsonConverterDefinition versionTokenJsonConverter, string? path, List<VersionTokenDefinition> versionTokens)
    {
        if (!versionTokenJsonConverter.IsPartialClass)
        {
            AnsiConsole.MarkupLine(
                $"[red][bold]ERROR:[/] Skipping [underline]{versionTokenJsonConverter.Name}[/] class; it needs to be partial to support generated code, make it partial please.[/]");
            return;
        }

        var usings = new List<string>
        {
            "System.Text.Json",
            "ErikLieben.FA.ES",
            "System.CodeDom.Compiler",
            "System.Diagnostics.CodeAnalysis"
        };
        usings.AddRange(versionTokens.Select(versionToken => versionToken.NamespaceOfType));

        var code = new StringBuilder();

        foreach (var namespaceName in usings.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Order())
        {
            code.AppendLine($"using {namespaceName};");
        }
        code.Append($$"""

                      namespace {{versionTokenJsonConverter.Namespace}};

                      #nullable enable
                      /// <summary>
                      /// JSON converter for deserializing version token types with polymorphic type resolution.
                      /// </summary>
                      /// <typeparam name="T">The specific version token type to deserialize to.</typeparam>
                      public partial class {{versionTokenJsonConverter.Name}}<T>
                      {
                      /// <summary>
                      /// Reads and converts JSON to a version token of the specified type.
                      /// </summary>
                      /// <param name="reader">The UTF-8 JSON reader to read from.</param>
                      /// <param name="typeToConvert">The target type to convert to.</param>
                      /// <param name="options">JSON serializer options for the conversion.</param>
                      /// <returns>A version token instance of the specified type, or null if conversion fails.</returns>
                      public override partial T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                      {
                      var versionToken = Converter.Read(ref reader, typeof(VersionToken), options);
                      return typeof(T) switch
                      {
                      """);

        foreach (var versionToken in versionTokens)
        {
            code.AppendLine($$"""{ } when typeof(T) == typeof({{versionToken.Name}}) => new {{versionToken.Name}}(versionToken.Value) as T,""");
        }

        code.Append($$"""
                        _ => null
                    };
                }
            }
            #nullable restore
            """);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        var projectDir = CodeFormattingHelper.FindProjectDirectory(path!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString(), projectDir));
    }


}
