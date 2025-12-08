using System.Composition;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Code refactoring provider that allows converting between the implicit EventName convention
/// (period-separated at uppercase boundaries) and explicit [EventName("...")] attribute declarations on events.
/// </summary>
/// <remarks>
/// Convention: The event name is derived by inserting periods before uppercase letters that follow lowercase letters.
/// For example, "UserCreated" becomes "User.Created" and "OrderItemAdded" becomes "Order.Item.Added".
/// </remarks>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EventNameRefactoringProvider)), Shared]
public class EventNameRefactoringProvider : CodeRefactoringProvider
{
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var node = root.FindNode(context.Span);

        // Check if we're on a type declaration (class or record) that could be an event
        TypeDeclarationSyntax? typeDecl = node.FirstAncestorOrSelf<RecordDeclarationSyntax>();
        typeDecl ??= node.FirstAncestorOrSelf<ClassDeclarationSyntax>();

        if (typeDecl == null)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        // Check if this type implements IEvent<T> (is an event type)
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (typeSymbol == null || !IsEventType(typeSymbol))
            return;

        // Check if we're on an EventName attribute
        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute != null && IsEventNameAttribute(attribute))
        {
            // Offer to remove the attribute if it matches convention
            var attributeValue = GetEventNameFromAttribute(attribute);
            var conventionValue = GetConventionEventName(typeSymbol.Name);

            if (attributeValue == conventionValue)
            {
                context.RegisterRefactoring(
                    CodeAction.Create(
                        title: "Remove redundant [EventName] attribute (matches convention)",
                        createChangedDocument: c => RemoveEventNameAttributeAsync(context.Document, attribute, typeDecl, c),
                        equivalenceKey: "RemoveRedundantEventName"));
            }
            return;
        }

        // Check if type doesn't have EventName attribute - offer to add it
        var hasEventNameAttribute = typeDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(IsEventNameAttribute);

        if (!hasEventNameAttribute)
        {
            var eventName = GetConventionEventName(typeSymbol.Name);
            context.RegisterRefactoring(
                CodeAction.Create(
                    title: $"Add explicit [EventName(\"{eventName}\")] attribute",
                    createChangedDocument: c => AddEventNameAttributeAsync(context.Document, typeDecl, eventName, c),
                    equivalenceKey: "AddExplicitEventName"));
        }
    }

    private static bool IsEventType(INamedTypeSymbol typeSymbol)
    {
        // Check if the type implements IEvent<T>
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name == "IEvent" &&
                iface.ContainingNamespace.ToDisplayString() == TypeConstants.FrameworkNamespace &&
                iface.TypeArguments.Length == 1)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsEventNameAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name == "EventName" ||
               name == "EventNameAttribute" ||
               name.EndsWith(".EventName") ||
               name.EndsWith(".EventNameAttribute");
    }

    private static string? GetEventNameFromAttribute(AttributeSyntax attribute)
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
    /// Gets the conventional event name by inserting periods before uppercase letters that follow lowercase letters.
    /// </summary>
    private static string GetConventionEventName(string className)
    {
        if (string.IsNullOrEmpty(className))
            return className;

        var result = new System.Text.StringBuilder(className.Length * 2);
        result.Append(className[0]);

        for (var i = 1; i < className.Length; i++)
        {
            if (char.IsUpper(className[i]) && char.IsLower(className[i - 1]))
            {
                result.Append('.');
            }
            result.Append(className[i]);
        }

        return result.ToString();
    }

    private static async Task<Document> AddEventNameAttributeAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        string eventName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Create the [EventName("eventName")] attribute
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("EventName"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(eventName))))));

        // Get indentation from the type declaration
        var typeLeadingTrivia = typeDecl.GetLeadingTrivia();
        var indentation = typeLeadingTrivia
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));

        var attributeLeadingTrivia = indentation != default
            ? SyntaxFactory.TriviaList(indentation)
            : SyntaxFactory.TriviaList();

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(attributeLeadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Add the attribute to the type
        var newAttributeLists = typeDecl.AttributeLists.Add(attributeList);
        var newTypeDecl = typeDecl.WithAttributeLists(newAttributeLists);

        // Replace the type in the root
        var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

        // Add using directive if needed
        newRoot = AddUsingDirectiveIfNeeded(newRoot, TypeConstants.FrameworkAttributesNamespace);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> RemoveEventNameAttributeAsync(
        Document document,
        AttributeSyntax attribute,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        var attributeList = attribute.FirstAncestorOrSelf<AttributeListSyntax>();
        TypeDeclarationSyntax newTypeDecl;

        if (attributeList != null)
        {
            if (attributeList.Attributes.Count == 1)
            {
                // Remove the entire attribute list
                newTypeDecl = typeDecl.WithAttributeLists(
                    typeDecl.AttributeLists.Remove(attributeList));
            }
            else
            {
                // Remove just this attribute from the list
                var newAttributeList = attributeList.WithAttributes(
                    attributeList.Attributes.Remove(attribute));
                newTypeDecl = typeDecl.ReplaceNode(attributeList, newAttributeList);
            }
        }
        else
        {
            return document;
        }

        // Replace the type in the root
        var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);

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
