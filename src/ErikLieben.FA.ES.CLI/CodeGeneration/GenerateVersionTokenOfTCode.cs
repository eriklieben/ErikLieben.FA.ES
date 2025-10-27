using System.Text;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Spectre.Console;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public class GenerateVersionTokenOfTCode
{

    private readonly SolutionDefinition solution;
    private readonly Config config;
    private readonly string solutionPath;

    public GenerateVersionTokenOfTCode(SolutionDefinition solution, Config config, string solutionPath)
    {
        this.solution = solution;
        this.config = config;
        this.solutionPath = solutionPath;
    }

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
                await GenerateVersionToken(versionToken, path);
            }

            // Generate json
        }
    }

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
            
            public partial record {{versionToken.Name}}
            {
                public {{versionToken.Name}}() {}
            
                public {{versionToken.Name}}(string versionTokenString) : base(versionTokenString) { }
            
                public {{versionToken.Name}}({{versionToken.GenericType}} objectId, string versionIdentifierPart)
                {
                    ArgumentNullException.ThrowIfNull(versionIdentifierPart);
            
                    Version = -1;
                    Value = $"{ObjectName}__{objectId}__{versionIdentifierPart}";
                    ParseFullString(Value);
                }
            
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
            
                public {{versionToken.Name}}({{versionToken.GenericType}} objectId, VersionIdentifier versionIdentifier)
                {
                    ArgumentNullException.ThrowIfNull(versionIdentifier);
            
                    Version = -1;
                    ParseFullString($"{ObjectName}__{objectId}__{versionIdentifier.Value}");
                }
            
                public {{versionToken.Name}}(IEvent @event, IObjectDocument document) : base(@event, document)
                {
                }
            }
            
            
            public static class ObjectMetaData{{versionToken.Name}}Extensions
            {
                public static {{versionToken.Name}} ToVersionToken(this ObjectMetadata<{{versionToken.GenericType}}> token)
                {
                    return new {{versionToken.Name}}(token.Id, token.StreamId, token.VersionInStream);
                }
            }

            """);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path!)!);
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
