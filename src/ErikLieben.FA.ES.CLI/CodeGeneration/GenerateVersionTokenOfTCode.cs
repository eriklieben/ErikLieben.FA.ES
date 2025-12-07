using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Generates supporting partial classes for version token definitions, providing constructors and extension methods for version token manipulation.
/// </summary>
public class GenerateVersionTokenOfTCode
{

    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateVersionTokenOfTCode"/> class.
    /// </summary>
    /// <param name="solution">The parsed solution model containing version token definitions.</param>
    /// <param name="config">The CLI configuration values.</param>
    /// <param name="solutionPath">The absolute path to the solution root directory.</param>
    public GenerateVersionTokenOfTCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

    /// <summary>
    /// Generates partial class files for all version token definitions found in the solution.
    /// Creates .Generated.cs files alongside the original version token definitions.
    /// </summary>
    /// <returns>A task representing the asynchronous code generation operation.</returns>
    public async Task Generate()
    {
        foreach (var project in solution.Projects.Where(p => !p.Name.StartsWith("ErikLieben.FA.ES")))
        {
            foreach (var versionToken in project.VersionTokens)
            {
                AnsiConsole.MarkupLine($"Generating supporting partial class for: [darkcyan]{versionToken.Name}[/]");
                var currentFile = versionToken.FileLocations.FirstOrDefault();
                if (currentFile is null || currentFile.Contains(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rel = (versionToken.FileLocations.FirstOrDefault() ?? string.Empty).Replace('\\', '/');
                var relGen = rel.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? string.Concat(rel.AsSpan(0, rel.Length - 3), ".Generated.cs")
                    : rel + ".Generated.cs";
                var normalized = relGen.Replace('/', System.IO.Path.DirectorySeparatorChar)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var path = System.IO.Path.Combine(solutionPath, normalized);
                AnsiConsole.MarkupLine($"Path: [blue]{path}[/]");
                await GenerateVersionToken(versionToken, path, solution.Generator?.Version ?? "1.0.0");
            }

            // Generate json
        }
    }

    /// <summary>
    /// Generates the partial class code for a version token, including multiple constructors and extension methods.
    /// </summary>
    /// <param name="versionToken">The version token definition to generate code for.</param>
    /// <param name="path">The output file path for the generated code.</param>
    /// <returns>A task representing the asynchronous file write operation.</returns>
    /// <remarks>
    /// The generated code includes:
    /// - Multiple constructors for creating version tokens from different inputs
    /// - Extension methods for converting ObjectMetadata to version tokens
    /// - Proper using directives and namespace declarations
    /// </remarks>
    private static async Task GenerateVersionToken(VersionTokenDefinition versionToken, string? path)
    {
        if (!versionToken.IsPartialClass)
        {
            AnsiConsole.MarkupLine(
                $"[red][bold]ERROR:[/] Skipping [underline]{versionToken.Name}[/] class; it needs to be partial to support generated code, make it partial please.[/]");
            return;
        }

        var code = new StringBuilder();

        code.Append($$"""
            using System.Text.Json.Serialization;
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Documents;
            using ErikLieben.FA.ES.VersionTokenParts;
            using {{versionToken.NamespaceOfType}};

            namespace {{versionToken.Namespace}};

            /// <summary>
            /// Version token for tracking {{versionToken.Name}} aggregate versions across event streams.
            /// </summary>
            public partial record {{versionToken.Name}}
            {
                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class.
                /// </summary>
                public {{versionToken.Name}}() {}

                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class from a version token string.
                /// </summary>
                /// <param name="versionTokenString">The version token string to parse.</param>
                public {{versionToken.Name}}(string versionTokenString) : base(versionTokenString) { }

                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class with object ID and version identifier.
                /// </summary>
                /// <param name="objectId">The object identifier.</param>
                /// <param name="versionIdentifierPart">The version identifier part.</param>
                public {{versionToken.Name}}({{versionToken.GenericType}} objectId, string versionIdentifierPart)
                {
                    ArgumentNullException.ThrowIfNull(versionIdentifierPart);

                    Version = -1;
                    Value = $"{ObjectName}__{objectId}__{versionIdentifierPart}";
                    ParseFullString(Value);
                }

                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class with object ID, stream identifier, and version.
                /// </summary>
                /// <param name="objectId">The object identifier.</param>
                /// <param name="streamIdentifier">The stream identifier.</param>
                /// <param name="version">The event version number.</param>
                public {{versionToken.Name}}({{versionToken.GenericType}} objectId, string streamIdentifier, int version)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(streamIdentifier);

                    ObjectId = objectId;
                    StreamIdentifier = streamIdentifier;
                    Version = version;
                    VersionString = ToVersionTokenString(version);

                    var objectIdentifierPart = $"{ObjectName}__{ObjectId}";
                    var versionIdentifierPart = $"{StreamIdentifier}__{VersionString}";
                    Value = $"{objectIdentifierPart}__{versionIdentifierPart}";
                    ParseFullString(Value);
                }

                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class with object ID and version identifier.
                /// </summary>
                /// <param name="objectId">The object identifier.</param>
                /// <param name="versionIdentifier">The version identifier.</param>
                public {{versionToken.Name}}({{versionToken.GenericType}} objectId, VersionIdentifier versionIdentifier)
                {
                    ArgumentNullException.ThrowIfNull(versionIdentifier);

                    Version = -1;
                    ParseFullString($"{ObjectName}__{objectId}__{versionIdentifier.Value}");
                }

                /// <summary>
                /// Initializes a new instance of the {{versionToken.Name}} class from an event and document.
                /// </summary>
                /// <param name="event">The event to extract version information from.</param>
                /// <param name="document">The object document associated with the event.</param>
                public {{versionToken.Name}}(IEvent @event, IObjectDocument document) : base(@event, document)
                {
                }
            }


            /// <summary>
            /// Extension methods for converting ObjectMetadata to {{versionToken.Name}} version tokens.
            /// </summary>
            public static class ObjectMetaData{{versionToken.Name}}Extensions
            {
                /// <summary>
                /// Converts ObjectMetadata to a {{versionToken.Name}} version token.
                /// </summary>
                /// <param name="token">The object metadata to convert.</param>
                /// <returns>A {{versionToken.Name}} version token.</returns>
                public static {{versionToken.Name}} ToVersionToken(this ObjectMetadata<{{versionToken.GenericType}}> token)
                {
                    return new {{versionToken.Name}}(token.Id, token.StreamId, token.VersionInStream);
                }
            }

            """);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
        var projectDir = CodeFormattingHelper.FindProjectDirectory(path!);
        await File.WriteAllTextAsync(path!, CodeFormattingHelper.FormatCode(code.ToString(), projectDir));
    }


}
