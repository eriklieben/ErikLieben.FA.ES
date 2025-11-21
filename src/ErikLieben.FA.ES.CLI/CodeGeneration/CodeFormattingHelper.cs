using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public static class CodeFormattingHelper
{
    private static List<MetadataReference>? _cachedReferences;
    private static readonly object _lock = new object();

    public static string FormatCode(string code, CancellationToken cancelToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancelToken);
        var syntaxNode = syntaxTree.GetRoot(cancelToken);

        using var workspace = new AdhocWorkspace();
        var options = workspace.Options
            .WithChangedOption(FormattingOptions.SmartIndent, LanguageNames.CSharp,
                FormattingOptions.IndentStyle.Smart);

        var formattedNode = Formatter.Format(syntaxNode, workspace, options, cancellationToken: cancelToken);

        // Remove unused usings with proper assembly references
        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create("temp", new[] { formattedNode.SyntaxTree }, references);
        var diagnostics = compilation.GetDiagnostics(cancelToken);

        // Log compilation errors for debugging
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Any())
        {
            Spectre.Console.AnsiConsole.MarkupLine("[yellow]Compilation diagnostics during formatting:[/]");
            foreach (var error in errors.Take(5))
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[dim]{error.GetMessage()}[/]");
            }
        }

        var semanticModel = compilation.GetSemanticModel(formattedNode.SyntaxTree);
        var root = formattedNode.SyntaxTree.GetRoot(cancelToken);

        var unusedUsings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name != null && !IsUsingDirectiveUsed(semanticModel, u, cancelToken))
            .ToList();

        if (unusedUsings.Any())
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[dim]Removing {unusedUsings.Count} unused usings: {string.Join(", ", unusedUsings.Select(u => u.Name?.ToString()))}[/]");
        }

        var newRoot = unusedUsings.Any() ? root.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepNoTrivia) : root;
        var formattedCode = newRoot?.ToFullString() ?? formattedNode.ToFullString();

        // Remove consecutive empty lines
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n){3,}", "$1$1");

        // Remove empty lines before closing braces
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n)\s*(\r?\n)(\s*})", "$1$3");

        return formattedCode;
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        // Return cached references if available
        if (_cachedReferences != null)
            return _cachedReferences;

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_cachedReferences != null)
                return _cachedReferences;

            var references = new List<MetadataReference>();
            var addedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add all currently loaded assemblies
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

            foreach (var assembly in loadedAssemblies)
            {
                TryAddReference(assembly.Location, references, addedLocations);
            }

            // Load referenced assemblies from the current executing assembly
            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var referencedAssembly in currentAssembly.GetReferencedAssemblies())
            {
                try
                {
                    var assembly = System.Reflection.Assembly.Load(referencedAssembly);
                    if (!string.IsNullOrEmpty(assembly.Location))
                    {
                        TryAddReference(assembly.Location, references, addedLocations);
                    }
                }
                catch
                {
                    // Skip assemblies that can't be loaded
                }
            }

            // Also scan for assemblies in the current directory (bin folder)
            var currentDirectory = Path.GetDirectoryName(currentAssembly.Location);
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                foreach (var dllPath in Directory.GetFiles(currentDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    TryAddReference(dllPath, references, addedLocations);
                }
            }

            _cachedReferences = references;
            return references;
        }
    }

    private static void TryAddReference(string location, List<MetadataReference> references, HashSet<string> addedLocations)
    {
        if (string.IsNullOrEmpty(location) || !addedLocations.Add(location))
            return;

        try
        {
            references.Add(MetadataReference.CreateFromFile(location));
        }
        catch
        {
            // Skip assemblies that can't be loaded
        }
    }

    private static bool IsUsingDirectiveUsed(SemanticModel semanticModel, UsingDirectiveSyntax usingDirective, CancellationToken cancelToken)
    {
        var namespaceName = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(namespaceName))
            return true;

        var root = semanticModel.SyntaxTree.GetRoot(cancelToken);

        // Check if there are ANY unresolved symbols in the entire file
        var allIdentifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
        var allAttributes = root.DescendantNodes().OfType<AttributeSyntax>();
        var hasUnresolvedSymbols = false;

        foreach (var identifier in allIdentifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancelToken);
            if (symbolInfo.Symbol == null && symbolInfo.CandidateSymbols.IsEmpty)
            {
                hasUnresolvedSymbols = true;
                break;
            }
        }

        if (!hasUnresolvedSymbols)
        {
            foreach (var attribute in allAttributes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancelToken);
                if (symbolInfo.Symbol == null && symbolInfo.CandidateSymbols.IsEmpty)
                {
                    hasUnresolvedSymbols = true;
                    break;
                }
            }
        }

        // If there are unresolved symbols, we can't reliably determine what's used
        // Keep ALL usings to be safe - the semantic model is incomplete
        if (hasUnresolvedSymbols)
        {
            // Debug output
            if (namespaceName == "System.Collections.Concurrent")
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[cyan]Keeping {namespaceName} due to unresolved symbols[/]");
            }
            return true;
        }

        // No unresolved symbols - we can safely determine if this using is actually used
        var identifiersAfterUsing = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Span.Start > usingDirective.Span.End);

        foreach (var identifier in identifiersAfterUsing)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancelToken);
            if (symbolInfo.Symbol != null)
            {
                var containingNamespace = symbolInfo.Symbol.ContainingNamespace?.ToDisplayString();
                if (containingNamespace != null && containingNamespace.StartsWith(namespaceName))
                    return true;
            }
        }

        // Check attributes
        var attributesAfterUsing = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Span.Start > usingDirective.Span.End);

        foreach (var attribute in attributesAfterUsing)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancelToken);
            if (symbolInfo.Symbol != null)
            {
                var containingNamespace = symbolInfo.Symbol.ContainingNamespace?.ToDisplayString();
                if (containingNamespace != null && containingNamespace.StartsWith(namespaceName))
                    return true;
            }
        }

        // Debug output for removed usings
        if (namespaceName == "System.Collections.Concurrent")
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[red]Removing {namespaceName} - no unresolved symbols detected![/]");
        }

        return false;
    }
}
