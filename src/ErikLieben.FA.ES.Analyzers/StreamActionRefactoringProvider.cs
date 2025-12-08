using System.Composition;
using ErikLieben.FA.ES.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.Analyzers;

/// <summary>
/// Code refactoring provider that allows converting between manual stream.RegisterAction()
/// calls and [StreamAction] attribute declarations on aggregates.
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(StreamActionRefactoringProvider)), Shared]
public class StreamActionRefactoringProvider : CodeRefactoringProvider
{
    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var node = root.FindNode(context.Span);

        // Check if we're on a RegisterAction invocation
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation != null && IsRegisterActionCall(invocation))
        {
            await TryRegisterManualToAttributeRefactoringAsync(context, invocation);
            return;
        }

        // Check if we're on a StreamAction attribute
        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute != null && IsStreamActionAttribute(attribute))
        {
            await TryRegisterAttributeToManualRefactoringAsync(context, attribute);
        }
    }

    private static bool IsRegisterActionCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        return methodName == "RegisterAction";
    }

    private static bool IsStreamActionAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name.StartsWith("StreamAction<") ||
               name.StartsWith("StreamActionAttribute<") ||
               name.Contains(".StreamAction<") ||
               name.Contains(".StreamActionAttribute<");
    }

    private async Task TryRegisterManualToAttributeRefactoringAsync(
        CodeRefactoringContext context,
        InvocationExpressionSyntax invocation)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        // Check if inside an aggregate constructor
        var constructor = invocation.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructor == null)
            return;

        var classDecl = constructor.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return;

        if (!SymbolHelpers.IsAggregateClass(classDecl, semanticModel))
            return;

        // Get the action type from the invocation
        var actionTypeName = GetActionTypeFromInvocation(invocation, semanticModel);
        if (actionTypeName == null)
            return;

        context.RegisterRefactoring(
            CodeAction.Create(
                title: $"Convert to [StreamAction<{actionTypeName}>] attribute",
                createChangedDocument: c => ConvertToAttributeAsync(context.Document, invocation, classDecl, actionTypeName, c),
                equivalenceKey: $"ConvertToAttribute_{actionTypeName}"));
    }

    private async Task TryRegisterAttributeToManualRefactoringAsync(
        CodeRefactoringContext context,
        AttributeSyntax attribute)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null)
            return;

        var classDecl = attribute.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return;

        if (!SymbolHelpers.IsAggregateClass(classDecl, semanticModel))
            return;

        // Get the action type from the attribute
        var actionTypeName = GetActionTypeFromAttribute(attribute);
        if (actionTypeName == null)
            return;

        context.RegisterRefactoring(
            CodeAction.Create(
                title: $"Convert to stream.RegisterAction(new {actionTypeName}())",
                createChangedDocument: c => ConvertToManualAsync(context.Document, attribute, classDecl, actionTypeName, c),
                equivalenceKey: $"ConvertToManual_{actionTypeName}"));
    }

    private static string? GetActionTypeFromInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Check for: stream.RegisterAction(new ActionType())
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var argument = invocation.ArgumentList.Arguments[0].Expression;

        // Handle: new ActionType()
        if (argument is ObjectCreationExpressionSyntax objectCreation)
        {
            var typeInfo = semanticModel.GetTypeInfo(objectCreation);
            return typeInfo.Type?.Name;
        }

        // Handle: new ActionType { ... }
        if (argument is ImplicitObjectCreationExpressionSyntax)
        {
            var typeInfo = semanticModel.GetTypeInfo(argument);
            return typeInfo.Type?.Name;
        }

        return null;
    }

    private static string? GetActionTypeFromAttribute(AttributeSyntax attribute)
    {
        // Extract type from StreamAction<ActionType> or StreamActionAttribute<ActionType>
        var name = attribute.Name;

        if (name is GenericNameSyntax genericName && genericName.TypeArgumentList.Arguments.Count > 0)
        {
            return genericName.TypeArgumentList.Arguments[0].ToString();
        }

        if (name is QualifiedNameSyntax qualifiedName &&
            qualifiedName.Right is GenericNameSyntax qualifiedGeneric &&
            qualifiedGeneric.TypeArgumentList.Arguments.Count > 0)
        {
            return qualifiedGeneric.TypeArgumentList.Arguments[0].ToString();
        }

        return null;
    }

    private static async Task<Document> ConvertToAttributeAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ClassDeclarationSyntax classDecl,
        string actionTypeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Create the [StreamAction<ActionType>] attribute
        var attributeName = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("StreamAction"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                    SyntaxFactory.IdentifierName(actionTypeName))));

        var attribute = SyntaxFactory.Attribute(attributeName);

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

        // Find the statement containing the invocation and remove it
        var statement = invocation.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null)
            return document;

        // Build new class with added attribute
        var newAttributeLists = classDecl.AttributeLists.Add(attributeList);
        var newClassDecl = classDecl.WithAttributeLists(newAttributeLists);

        // Remove the RegisterAction statement from the constructor
        var constructor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.DescendantNodes().Contains(invocation));

        if (constructor?.Body != null)
        {
            var newStatements = constructor.Body.Statements.Remove(statement);
            var newBody = constructor.Body.WithStatements(newStatements);
            var newConstructor = constructor.WithBody(newBody);

            newClassDecl = newClassDecl.ReplaceNode(
                newClassDecl.Members.OfType<ConstructorDeclarationSyntax>().First(c => c.Identifier.Text == constructor.Identifier.Text),
                newConstructor);
        }

        // Replace the class in the root
        var newRoot = root.ReplaceNode(classDecl, newClassDecl);

        // Add using directive if needed
        newRoot = AddUsingDirectiveIfNeeded(newRoot, TypeConstants.StreamActionAttributeNamespace);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ConvertToManualAsync(
        Document document,
        AttributeSyntax attribute,
        ClassDeclarationSyntax classDecl,
        string actionTypeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Find or create the constructor
        var constructor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => c.ParameterList.Parameters.Any(p =>
                p.Type?.ToString().Contains("IEventStream") == true));

        if (constructor?.Body == null)
            return document;

        // Get the stream parameter name
        var streamParam = constructor.ParameterList.Parameters
            .FirstOrDefault(p => p.Type?.ToString().Contains("IEventStream") == true);

        var streamName = streamParam?.Identifier.Text ?? "stream";

        // Create: stream.RegisterAction(new ActionType());
        var registerActionStatement = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(streamName),
                    SyntaxFactory.IdentifierName("RegisterAction")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName(actionTypeName))
                                .WithArgumentList(SyntaxFactory.ArgumentList()))))));

        // Get indentation from existing statements or constructor body
        var statementIndentation = constructor.Body.Statements.FirstOrDefault()?.GetLeadingTrivia()
            ?? SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("        "));

        registerActionStatement = registerActionStatement
            .WithLeadingTrivia(statementIndentation)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Add the statement to the constructor body
        var newStatements = constructor.Body.Statements.Add(registerActionStatement);
        var newBody = constructor.Body.WithStatements(newStatements);
        var newConstructor = constructor.WithBody(newBody);

        // Remove the attribute from the class
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
            newClassDecl = classDecl;
        }

        // Replace the constructor in the class
        var oldConstructor = newClassDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .First(c => c.Identifier.Text == constructor.Identifier.Text);

        newClassDecl = newClassDecl.ReplaceNode(oldConstructor, newConstructor);

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
