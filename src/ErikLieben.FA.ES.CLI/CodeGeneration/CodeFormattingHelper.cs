#pragma warning disable S3267 // Loops should be simplified - explicit loops improve debuggability

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Provides code formatting utilities with Roslyn-based formatting and intelligent unused using directive removal.
/// </summary>
public static class CodeFormattingHelper
{
    private static List<MetadataReference>? _cachedReferences;
    private static readonly object _lock = new object();

    /// <summary>
    /// Formats C# code using Roslyn formatting and removes unused using directives when the semantic model is complete.
    /// </summary>
    /// <param name="code">The C# code to format.</param>
    /// <param name="projectDirectory">Optional project directory path for loading source files and NuGet packages to improve type resolution.</param>
    /// <param name="cancelToken">Cancellation token for the operation.</param>
    /// <returns>The formatted C# code with unused usings removed when possible.</returns>
    /// <remarks>
    /// This method performs the following operations:
    /// 1. Formats the code using Roslyn's Formatter
    /// 2. Loads metadata references from loaded assemblies, bin folder, and NuGet packages
    /// 3. Includes all project source files in the compilation for complete type information
    /// 4. Removes unused using directives only when all symbols can be resolved
    /// 5. Removes consecutive empty lines
    /// 6. Removes empty lines before closing braces
    /// </remarks>
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

        var newRoot = unusedUsings.Count > 0 ? root.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepNoTrivia) : root;
        var formattedCode = newRoot?.ToFullString() ?? formattedNode.ToFullString();

        // Remove consecutive empty lines
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n){3,}", "$1$1", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));

        // Remove empty lines before closing braces
        formattedCode = System.Text.RegularExpressions.Regex.Replace(formattedCode, @"(\r?\n)\s*(\r?\n)(\s*})", "$1$3", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));

        return formattedCode;
    }

    /// <summary>
    /// Gets metadata references for Roslyn compilation, including loaded assemblies, bin folder DLLs, and NuGet packages.
    /// Results are cached for performance across multiple calls.
    /// </summary>
    /// <param name="projectDirectory">Optional project directory for loading project-specific NuGet packages.</param>
    /// <returns>List of metadata references for Roslyn compilation.</returns>
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

    /// <summary>
    /// Loads NuGet package assemblies referenced by the project from the global NuGet cache.
    /// Reads package information from the project's project.assets.json file.
    /// </summary>
    /// <param name="projectDirectory">The project directory containing the obj/project.assets.json file.</param>
    /// <param name="references">List to add metadata references to.</param>
    /// <param name="addedLocations">Set tracking already-added assembly locations to avoid duplicates.</param>
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
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"""([^""]+\.dll)""", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (match.Success)
                    {
                        packagePaths.Add(match.Groups[1].Value);
                    }
                }
            }

            // Also look for target section which has package names and versions
            foreach (var line in lines)
            {
                var packageMatch = System.Text.RegularExpressions.Regex.Match(line, @"""([a-zA-Z0-9\.]+)/([0-9\.]+)""", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
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

    /// <summary>
    /// Loads assemblies from a specific NuGet package version directory, selecting the most appropriate target framework.
    /// </summary>
    /// <param name="packageVersionDir">The NuGet package version directory path.</param>
    /// <param name="references">List to add metadata references to.</param>
    /// <param name="addedLocations">Set tracking already-added assembly locations to avoid duplicates.</param>
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

            // Only use first matching TFM
            var firstTfmDir = tfmDirs.FirstOrDefault();
            if (firstTfmDir != null)
            {
                foreach (var dllPath in Directory.GetFiles(firstTfmDir, "*.dll"))
                {
                    TryAddReference(dllPath, references, addedLocations);
                }
            }
        }
        catch
        {
            // Skip packages that can't be loaded
        }
    }

    /// <summary>
    /// Attempts to add a metadata reference from the specified assembly location.
    /// Silently skips assemblies that fail to load or are duplicates.
    /// </summary>
    /// <param name="location">The file path of the assembly.</param>
    /// <param name="references">List to add the metadata reference to.</param>
    /// <param name="addedLocations">Set tracking already-added assembly locations to prevent duplicates.</param>
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

    /// <summary>
    /// Determines if a using directive is actually used in the code by checking if any symbols from its namespace are referenced.
    /// If any symbols in the file cannot be resolved, all using directives are kept to avoid removing needed ones.
    /// </summary>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="root">The syntax tree root node.</param>
    /// <param name="usingDirective">The using directive to check.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>True if the using directive should be kept, false if it can be safely removed.</returns>
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

    /// <summary>
    /// Checks if there are any unresolved symbols in the syntax tree.
    /// Unresolved symbols indicate the semantic model is incomplete (missing assemblies, types from same project not yet compiled, etc.).
    /// </summary>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="root">The syntax tree root node.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>True if any symbols cannot be resolved, false if all symbols are resolved.</returns>
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

    /// <summary>
    /// Finds the project directory by walking up the directory tree from a file path until a .csproj file is found.
    /// </summary>
    /// <param name="filePath">The starting file path.</param>
    /// <returns>The project directory path containing a .csproj file, or null if not found.</returns>
    public static string? FindProjectDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.GetFiles(directory, "*.csproj").Length > 0)
            {
                return directory;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }
}
