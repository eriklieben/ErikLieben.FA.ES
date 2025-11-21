using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

public static class CodeFormattingHelper
{
    private static List<MetadataReference>? _cachedReferences;
    private static readonly object _lock = new object();

    public static string FormatCode(string code, string? projectDirectory = null, CancellationToken cancelToken = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancelToken);
        var syntaxNode = syntaxTree.GetRoot(cancelToken);

        using var workspace = new AdhocWorkspace();
        var options = workspace.Options
            .WithChangedOption(FormattingOptions.SmartIndent, LanguageNames.CSharp,
                FormattingOptions.IndentStyle.Smart);

        var formattedNode = Formatter.Format(syntaxNode, workspace, options, cancellationToken: cancelToken);

        // Remove unused usings with proper assembly references and project context
        var references = GetMetadataReferences(projectDirectory);
        var syntaxTrees = new List<SyntaxTree> { formattedNode.SyntaxTree };

        // Include all project source files for complete type information
        if (!string.IsNullOrEmpty(projectDirectory) && Directory.Exists(projectDirectory))
        {
            var sourceFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase));

            foreach (var sourceFile in sourceFiles)
            {
                try
                {
                    var sourceCode = File.ReadAllText(sourceFile);
                    var sourceSyntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancelToken);
                    syntaxTrees.Add(sourceSyntaxTree);
                }
                catch
                {
                    // Skip files that can't be read
                }
            }
        }

        var compilation = CSharpCompilation.Create("temp", syntaxTrees, references);
        var semanticModel = compilation.GetSemanticModel(formattedNode.SyntaxTree);
        var root = formattedNode.SyntaxTree.GetRoot(cancelToken);

        var unusedUsings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Name != null && !IsUsingDirectiveUsed(semanticModel, root, u, cancelToken))
            .ToList();

        var newRoot = unusedUsings.Any() ? root.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepNoTrivia) : root;
        var formattedCode = newRoot?.ToFullString() ?? formattedNode.ToFullString();

        // Remove consecutive empty lines
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n){3,}", "$1$1");

        // Remove empty lines before closing braces
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n)\s*(\r?\n)(\s*})", "$1$3");

        return formattedCode;
    }

    private static List<MetadataReference> GetMetadataReferences(string? projectDirectory = null)
    {
        if (_cachedReferences != null)
            return _cachedReferences;

        lock (_lock)
        {
            if (_cachedReferences != null)
                return _cachedReferences;

            var references = new List<MetadataReference>();
            var addedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));

            foreach (var assembly in loadedAssemblies)
            {
                TryAddReference(assembly.Location, references, addedLocations);
            }

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

            var currentDirectory = Path.GetDirectoryName(currentAssembly.Location);
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                foreach (var dllPath in Directory.GetFiles(currentDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    TryAddReference(dllPath, references, addedLocations);
                }
            }

            // Load NuGet packages from the project's assets file
            LoadNuGetPackagesFromProject(projectDirectory, references, addedLocations);

            _cachedReferences = references;
            return references;
        }
    }

    private static void LoadNuGetPackagesFromProject(string? projectDirectory, List<MetadataReference> references, HashSet<string> addedLocations)
    {
        if (string.IsNullOrEmpty(projectDirectory) || !Directory.Exists(projectDirectory))
            return;

        // Read project.assets.json from obj folder
        var assetsFile = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
            return;

        try
        {
            var assetsJson = File.ReadAllText(assetsFile);
            var nugetCache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages");

            if (!Directory.Exists(nugetCache))
                return;

            // Parse packages from assets file (simple text search approach)
            // Looking for patterns like "Azure.Storage.Blobs/12.19.1"
            var lines = assetsJson.Split('\n');
            var packagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                if (line.Contains("\"compile\"") || line.Contains("\"runtime\""))
                {
                    // Extract package path from entries like "lib/net6.0/Azure.Storage.Blobs.dll"
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"""([^""]+\.dll)""");
                    if (match.Success)
                    {
                        packagePaths.Add(match.Groups[1].Value);
                    }
                }
            }

            // Also look for target section which has package names and versions
            foreach (var line in lines)
            {
                var packageMatch = System.Text.RegularExpressions.Regex.Match(line, @"""([a-zA-Z0-9\.]+)/([0-9\.]+)""");
                if (packageMatch.Success)
                {
                    var packageName = packageMatch.Groups[1].Value.ToLowerInvariant();
                    var version = packageMatch.Groups[2].Value;

                    var packageDir = Path.Combine(nugetCache, packageName, version);
                    if (Directory.Exists(packageDir))
                    {
                        LoadPackageAssemblies(packageDir, references, addedLocations);
                    }
                }
            }
        }
        catch
        {
            // If we can't read assets file, silently continue
        }
    }

    private static void LoadPackageAssemblies(string packageVersionDir, List<MetadataReference> references, HashSet<string> addedLocations)
    {
        var libDir = Path.Combine(packageVersionDir, "lib");
        if (!Directory.Exists(libDir))
            return;

        try
        {
            // Try to find the most appropriate target framework folder
            var tfmDirs = Directory.GetDirectories(libDir)
                .Where(d =>
                    d.Contains("net8.0") ||
                    d.Contains("net9.0") ||
                    d.Contains("net10.0") ||
                    d.Contains("netstandard2.1") ||
                    d.Contains("netstandard2.0") ||
                    d.Contains("net6.0") ||
                    d.Contains("net7.0"))
                .OrderByDescending(d => d);

            foreach (var tfmDir in tfmDirs)
            {
                foreach (var dllPath in Directory.GetFiles(tfmDir, "*.dll"))
                {
                    TryAddReference(dllPath, references, addedLocations);
                }
                break; // Only use first matching TFM
            }
        }
        catch
        {
            // Skip packages that can't be loaded
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

    private static bool IsUsingDirectiveUsed(SemanticModel semanticModel, SyntaxNode root, UsingDirectiveSyntax usingDirective, CancellationToken cancelToken)
    {
        var namespaceName = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(namespaceName))
            return true;

        // First check if there are ANY unresolved symbols - if so, keep all usings
        if (HasUnresolvedSymbols(semanticModel, root, cancelToken))
        {
            return true; // Keep all usings when there are unresolved types
        }

        // All symbols can be resolved - check if this specific using is needed
        // Check all identifiers
        var allIdentifiers = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Span.Start > usingDirective.Span.End);

        foreach (var identifier in allIdentifiers)
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
        var allAttributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Span.Start > usingDirective.Span.End);

        foreach (var attribute in allAttributes)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancelToken);
            if (symbolInfo.Symbol != null)
            {
                var containingNamespace = symbolInfo.Symbol.ContainingNamespace?.ToDisplayString();
                if (containingNamespace != null && containingNamespace.StartsWith(namespaceName))
                    return true;
            }
        }

        // Check generic names (like ConcurrentDictionary in ConcurrentDictionary<K,V>)
        var allGenericNames = root.DescendantNodes()
            .OfType<GenericNameSyntax>()
            .Where(gn => gn.Span.Start > usingDirective.Span.End);

        foreach (var genericName in allGenericNames)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(genericName, cancelToken);
            if (symbolInfo.Symbol != null)
            {
                var containingNamespace = symbolInfo.Symbol.ContainingNamespace?.ToDisplayString();
                if (containingNamespace != null && containingNamespace.StartsWith(namespaceName))
                    return true;
            }
        }

        return false;
    }

    private static bool HasUnresolvedSymbols(SemanticModel semanticModel, SyntaxNode root, CancellationToken cancelToken)
    {
        // Check identifiers
        var allIdentifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var identifier in allIdentifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancelToken);
            if (symbolInfo.Symbol == null && symbolInfo.CandidateSymbols.IsEmpty)
            {
                return true;
            }
        }

        // Check attributes
        var allAttributes = root.DescendantNodes().OfType<AttributeSyntax>();
        foreach (var attribute in allAttributes)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancelToken);
            if (symbolInfo.Symbol == null && symbolInfo.CandidateSymbols.IsEmpty)
            {
                return true;
            }
        }

        // Check generic names
        var allGenericNames = root.DescendantNodes().OfType<GenericNameSyntax>();
        foreach (var genericName in allGenericNames)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(genericName, cancelToken);
            if (symbolInfo.Symbol == null && symbolInfo.CandidateSymbols.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    public static string? FindProjectDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.GetFiles(directory, "*.csproj").Any())
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }
}
