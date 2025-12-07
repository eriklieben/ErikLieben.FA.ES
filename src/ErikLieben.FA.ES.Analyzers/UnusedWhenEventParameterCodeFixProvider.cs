using System.Collections.Immutable;
using System.Composition;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Code fix provider that converts When methods with unused event parameters to use the [When&lt;TEvent&gt;] attribute.
/// Also updates the corresponding .Generated.cs file to call the renamed method.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedWhenEventParameterCodeFixProvider)), Shared]
public class UnusedWhenEventParameterCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        [UnusedWhenEventParameterAnalyzer.DiagnosticId];

    /// <summary>
    /// Gets the fix all provider for this code fix.
    /// </summary>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified diagnostic.
    /// </summary>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the parameter that triggered the diagnostic
        var parameter = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter == null)
            return;

        // Get the method declaration
        var methodDecl = parameter.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDecl == null)
            return;

        // Get event type info from diagnostic properties
        if (!diagnostic.Properties.TryGetValue("EventTypeName", out var eventTypeName) || eventTypeName == null)
            return;

        diagnostic.Properties.TryGetValue("EventTypeNamespace", out var eventTypeNamespace);

        // Generate a method name based on the event type
        var newMethodName = $"When{eventTypeName}";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Convert to [When<{eventTypeName}>] {newMethodName}()",
                createChangedSolution: c => ConvertToWhenAttributeAsync(context.Document, methodDecl, eventTypeName, newMethodName, eventTypeNamespace, c),
                equivalenceKey: nameof(UnusedWhenEventParameterCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Solution> ConvertToWhenAttributeAsync(
        Document document,
        MethodDeclarationSyntax methodDecl,
        string eventTypeName,
        string newMethodName,
        string? eventTypeNamespace,
        CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        // Find the generated document first (before any modifications)
        Document? generatedDocument = null;
        var containingClass = methodDecl.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass != null)
        {
            var className = containingClass.Identifier.ValueText;
            generatedDocument = FindGeneratedDocument(solution, document, className);
        }

        // 1. Update the source file (add attribute, rename method, remove parameter)
        var updatedSourceDocument = await UpdateSourceFileAsync(document, methodDecl, eventTypeName, newMethodName, cancellationToken);
        solution = updatedSourceDocument.Project.Solution;

        // 2. Update the .Generated.cs file if found
        if (generatedDocument != null)
        {
            // Get the generated document from the updated solution
            var updatedGeneratedDocument = solution.GetDocument(generatedDocument.Id);
            if (updatedGeneratedDocument != null)
            {
                var finalGeneratedDocument = await UpdateGeneratedFileAsync(updatedGeneratedDocument, eventTypeName, newMethodName, cancellationToken);
                solution = finalGeneratedDocument.Project.Solution;
            }
        }

        return solution;
    }

    private static async Task<Document> UpdateSourceFileAsync(
        Document document,
        MethodDeclarationSyntax methodDecl,
        string eventTypeName,
        string newMethodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Get the leading trivia from the method (to preserve indentation and blank lines)
        var methodLeadingTrivia = methodDecl.GetLeadingTrivia();

        // Extract just the indentation (whitespace before the method modifier)
        // The leading trivia typically ends with whitespace for indentation
        var indentationTrivia = methodLeadingTrivia
            .Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .LastOrDefault();

        var indentation = indentationTrivia != default
            ? SyntaxFactory.TriviaList(indentationTrivia)
            : SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("        "));

        // Create the [When<EventType>] attribute
        var attributeName = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("When"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                    SyntaxFactory.IdentifierName(eventTypeName))));

        var attribute = SyntaxFactory.Attribute(attributeName);

        // Attribute gets the full original leading trivia (blank line + indentation)
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(methodLeadingTrivia);

        // Remove the first parameter (the event parameter)
        var newParameters = methodDecl.ParameterList.Parameters.RemoveAt(0);
        var newParameterList = methodDecl.ParameterList.WithParameters(newParameters);

        // Use the provided method name
        var newIdentifier = SyntaxFactory.Identifier(newMethodName)
            .WithLeadingTrivia(methodDecl.Identifier.LeadingTrivia)
            .WithTrailingTrivia(methodDecl.Identifier.TrailingTrivia);

        // Build new attribute list - prepend the When attribute
        var newAttributeLists = methodDecl.AttributeLists.Insert(0, attributeList);

        // Create the new method declaration
        // The method needs a newline + indentation since attribute now has the original leading trivia
        var methodTrivia = SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)
            .AddRange(indentation);

        var newMethodDecl = methodDecl
            .WithAttributeLists(newAttributeLists)
            .WithIdentifier(newIdentifier)
            .WithParameterList(newParameterList)
            .WithLeadingTrivia(methodTrivia);

        // Replace the method in the syntax tree
        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

        // Add using directive if needed - check both file-level and namespace-level usings
        newRoot = AddUsingDirectiveIfNeeded(newRoot, methodDecl);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode AddUsingDirectiveIfNeeded(SyntaxNode root, MethodDeclarationSyntax methodDecl)
    {
        const string whenAttributeNamespace = "ErikLieben.FA.ES.Attributes";

        // Check if using already exists at file level
        if (root is CompilationUnitSyntax compilationUnit)
        {
            var hasFileUsing = compilationUnit.Usings
                .Any(u => u.Name?.ToString() == whenAttributeNamespace);

            if (hasFileUsing)
                return root;

            // Check if the method is inside a namespace with usings
            var namespaceDecl = methodDecl.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
            if (namespaceDecl != null)
            {
                var hasNamespaceUsing = namespaceDecl.Usings
                    .Any(u => u.Name?.ToString() == whenAttributeNamespace);

                if (hasNamespaceUsing)
                    return root;

                // Add the using inside the namespace
                var newNamespaceDecl = namespaceDecl;

                // Find the updated namespace in the root
                var updatedNamespace = root.DescendantNodes()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault(n => n.Name.ToString() == namespaceDecl.Name.ToString());

                if (updatedNamespace != null)
                {
                    // Get indentation from existing usings or default
                    var indentation = updatedNamespace.Usings.FirstOrDefault()?.GetLeadingTrivia()
                        ?? SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("    "));

                    var usingDirective = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName(whenAttributeNamespace))
                        .WithLeadingTrivia(indentation)
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    var newUsings = updatedNamespace.Usings.Insert(0, usingDirective);
                    var updatedNs = updatedNamespace.WithUsings(newUsings);

                    return root.ReplaceNode(updatedNamespace, updatedNs);
                }
            }

            // Fallback: add to file level usings
            var fileUsingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(whenAttributeNamespace))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            var newFileUsings = compilationUnit.Usings.Add(fileUsingDirective);
            return compilationUnit.WithUsings(newFileUsings);
        }

        return root;
    }

    private static Document? FindGeneratedDocument(Solution solution, Document sourceDocument, string className)
    {
        // Look for a .Generated.cs file with the same class name in the same project
        var sourceFilePath = sourceDocument.FilePath;
        if (string.IsNullOrEmpty(sourceFilePath))
            return null;

        var directory = Path.GetDirectoryName(sourceFilePath);
        var generatedFileName = $"{className}.Generated.cs";
        var expectedGeneratedPath = Path.Combine(directory ?? "", generatedFileName);

        // Search in the same project for the generated file
        foreach (var doc in sourceDocument.Project.Documents)
        {
            if (doc.FilePath != null &&
                doc.FilePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase) &&
                doc.FilePath.Contains(className, StringComparison.OrdinalIgnoreCase))
            {
                return doc;
            }
        }

        return null;
    }

    private static async Task<Document> UpdateGeneratedFileAsync(
        Document generatedDocument,
        string eventTypeName,
        string newMethodName,
        CancellationToken cancellationToken)
    {
        var text = await generatedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var content = text.ToString();

        // Find and replace the method call in the Fold switch case
        // Pattern: When(JsonEvent.To(@event, {EventTypeName}JsonSerializerContext.Default.{EventTypeName}));
        // Replace with: {newMethodName}();
        // The new method no longer takes the event parameter
        var pattern = $@"When\(JsonEvent\.To\(@event,\s*{Regex.Escape(eventTypeName)}JsonSerializerContext\.Default\.{Regex.Escape(eventTypeName)}\)\)";
        var replacement = $"{newMethodName}()";

        var updatedContent = Regex.Replace(content, pattern, replacement);

        // Return updated document
        return generatedDocument.WithText(SourceText.From(updatedContent, text.Encoding));
    }
}
