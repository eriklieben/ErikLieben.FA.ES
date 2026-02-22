using System.Composition;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Code refactoring provider that allows converting between the implicit ObjectName convention
/// (camelCase of class name) and explicit [ObjectName("...")] attribute declarations on aggregates.
/// </summary>
/// <remarks>
/// Convention: The object name is derived by lowercasing the first character of the class name.
/// For example, "OrderAggregate" becomes "orderAggregate".
/// </remarks>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ObjectNameRefactoringProvider)), Shared]
public class ObjectNameRefactoringProvider : CodeRefactoringProvider
{
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var node = root.FindNode(context.Span);

        // Check if we're on a class declaration that is an aggregate
        var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        if (!SymbolHelpers.IsAggregateClass(classDecl, semanticModel))
            return;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
            return;

        // Check if we're on an ObjectName attribute
        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute != null && IsObjectNameAttribute(attribute))
        {
            // Offer to remove the attribute if it matches convention
            var attributeValue = GetObjectNameFromAttribute(attribute);
            var conventionValue = GetConventionObjectName(classSymbol.Name);

            if (attributeValue == conventionValue)
            {
                context.RegisterRefactoring(
                    CodeAction.Create(
                        title: "Remove redundant [ObjectName] attribute (matches convention)",
                        createChangedDocument: c => RemoveObjectNameAttributeAsync(context.Document, attribute, classDecl, c),
                        equivalenceKey: "RemoveRedundantObjectName"));
            }
            return;
        }

        // Check if class doesn't have ObjectName attribute - offer to add it
        var hasObjectNameAttribute = classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(IsObjectNameAttribute);

        if (!hasObjectNameAttribute)
        {
            var objectName = GetConventionObjectName(classSymbol.Name);
            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Add explicit [ObjectName(\"{objectName}\")] attribute",
                    createChangedDocument: c => AddObjectNameAttributeAsync(context.Document, classDecl, objectName, c),
                    equivalenceKey: "AddExplicitObjectName"));
        }
    }

    private static bool IsObjectNameAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name == "ObjectName" ||
               name == "ObjectNameAttribute" ||
               name.EndsWith(".ObjectName") ||
               name.EndsWith(".ObjectNameAttribute");
    }

    private static string? GetObjectNameFromAttribute(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = attribute.ArgumentList.Arguments[0];
            if (firstArg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the conventional object name by lowercasing the first character.
    /// </summary>
    private static string GetConventionObjectName(string className)
    {
        if (string.IsNullOrEmpty(className))
            return className;

        return char.ToLower(className[0]) + className.Substring(1);
    }

    private static async Task<Document> AddObjectNameAttributeAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        string objectName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Create the [ObjectName("objectName")] attribute
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("ObjectName"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(objectName))))));

        // Get indentation from the class declaration
        var classLeadingTrivia = classDecl.GetLeadingTrivia();
        var indentation = classLeadingTrivia
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));

        var attributeLeadingTrivia = indentation != default
            ? SyntaxFactory.TriviaList(indentation)
            : SyntaxFactory.TriviaList();

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(attributeLeadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Add the attribute to the class
        var newAttributeLists = classDecl.AttributeLists.Add(attributeList);
        var newClassDecl = classDecl.WithAttributeLists(newAttributeLists);

        // Replace the class in the root
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        // Add using directive if needed
        newRoot = AddUsingDirectiveIfNeeded(newRoot, TypeConstants.FrameworkAttributesNamespace);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> RemoveObjectNameAttributeAsync(
        Document document,
        AttributeSyntax attribute,
        ClassDeclarationSyntax classDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        var attributeList = attribute.FirstAncestorOrSelf<AttributeListSyntax>();
        ClassDeclarationSyntax newClassDecl;

        if (attributeList != null)
        {
            if (attributeList.Attributes.Count == 1)
            {
                // Remove the entire attribute list
                newClassDecl = classDecl.WithAttributeLists(
                    classDecl.AttributeLists.Remove(attributeList));
            }
            else
            {
                // Remove just this attribute from the list
                var newAttributeList = attributeList.WithAttributes(
                    attributeList.Attributes.Remove(attribute));
                newClassDecl = classDecl.ReplaceNode(attributeList, newAttributeList);
            }
        }
        else
        {
            return document;
        }

        // Replace the class in the root
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode AddUsingDirectiveIfNeeded(SyntaxNode root, string namespaceToAdd)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        // Check if using already exists at file level
        var hasFileUsing = compilationUnit.Usings
            .Any(u => u.Name?.ToString() == namespaceToAdd);

        if (hasFileUsing)
            return root;

        // Check if there's a namespace declaration with usings
        var namespaceDecl = compilationUnit.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDecl != null)
        {
            var hasNamespaceUsing = namespaceDecl.Usings
                .Any(u => u.Name?.ToString() == namespaceToAdd);

            if (hasNamespaceUsing)
                return root;
        }

        // Add to file level usings
        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceToAdd))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Insert in alphabetical order
        var usings = compilationUnit.Usings.ToList();
        var insertIndex = usings.FindIndex(u =>
            string.Compare(u.Name?.ToString(), namespaceToAdd, StringComparison.Ordinal) > 0);

        if (insertIndex < 0)
            insertIndex = usings.Count;

        usings.Insert(insertIndex, usingDirective);

        return compilationUnit.WithUsings(SyntaxFactory.List(usings));
    }
}
