using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CodeAnalysis.Tests;

public class SymbolHelpersTests
{
    private static readonly List<PortableExecutableReference> References =
    [
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Processors.Aggregate).Assembly.Location)
    ];

    private static (INamedTypeSymbol? ClassSymbol, SemanticModel SemanticModel) GetClassSymbol(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classNode = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (semanticModel.GetDeclaredSymbol(classNode), semanticModel);
    }

    private static ClassDeclarationSyntax GetClassDeclaration(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        return syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();
    }

    public class IsAggregateType
    {
        [Fact]
        public void Should_return_true_for_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace TestDomain;

                public class TestAggregate(IEventStream stream) : Aggregate(stream)
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateType(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_non_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateType(classSymbol);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsAggregateType(null);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_different_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class Aggregate {}

                public class TestAggregate : Aggregate
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateType(classSymbol);

            Assert.False(result);
        }
    }

    public class IsOrInheritsFromAggregate
    {
        [Fact]
        public void Should_return_true_for_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace TestDomain;

                public class TestAggregate(IEventStream stream) : Aggregate(stream)
                {
                }
                """);

            var result = SymbolHelpers.IsOrInheritsFromAggregate(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsOrInheritsFromAggregate(null);

            Assert.False(result);
        }
    }

    public class IsProjectionType
    {
        [Fact]
        public void Should_return_true_for_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : Projection
                {
                }
                """);

            var result = SymbolHelpers.IsProjectionType(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_true_for_routed_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : RoutedProjection
                {
                }
                """);

            var result = SymbolHelpers.IsProjectionType(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_non_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """);

            var result = SymbolHelpers.IsProjectionType(classSymbol);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsProjectionType(null);

            Assert.False(result);
        }
    }

    public class IsAggregateOrProjectionType
    {
        [Fact]
        public void Should_return_true_for_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace TestDomain;

                public class TestAggregate(IEventStream stream) : Aggregate(stream)
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateOrProjectionType(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_true_for_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : Projection
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateOrProjectionType(classSymbol);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_regular_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateOrProjectionType(classSymbol);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsAggregateOrProjectionType(null);

            Assert.False(result);
        }
    }

    public class IsAggregateClass
    {
        [Fact]
        public void Should_return_true_for_aggregate_class_declaration()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace TestDomain;

                public class TestAggregate(IEventStream stream) : Aggregate(stream)
                {
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var classDecl = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First();

            var result = SymbolHelpers.IsAggregateClass(classDecl, semanticModel);

            Assert.True(result);
        }
    }

    public class IsEventStreamType
    {
        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsEventStreamType(null);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_true_for_IEventStream_interface()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace TestDomain;

                public class TestClass
                {
                    public IEventStream Stream { get; set; }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var propertyNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .First();
            var typeInfo = semanticModel.GetTypeInfo(propertyNode.Type);

            var result = SymbolHelpers.IsEventStreamType(typeInfo.Type);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_non_event_stream_type()
        {
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public string Name { get; set; }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var propertyNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .First();
            var typeInfo = semanticModel.GetTypeInfo(propertyNode.Type);

            var result = SymbolHelpers.IsEventStreamType(typeInfo.Type);

            Assert.False(result);
        }
    }

    public class IsEventStreamSessionMethod
    {
        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsEventStreamSessionMethod(null);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_non_event_stream_method()
        {
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public void DoSomething() { }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var methodNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .First();
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);

            var result = SymbolHelpers.IsEventStreamSessionMethod(methodSymbol);

            Assert.False(result);
        }
    }

    public class IsInsideStreamSession
    {
        [Fact]
        public void Should_return_false_when_not_in_session()
        {
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public void Test()
                    {
                        DoSomething();
                    }

                    private void DoSomething() {}
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var invocation = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .First();

            var result = SymbolHelpers.IsInsideStreamSession(semanticModel, invocation);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_when_session_is_not_on_event_stream()
        {
            var code = """
                namespace TestDomain;

                public class OtherClass
                {
                    public void Session(System.Action action) => action();
                }

                public class TestClass
                {
                    private readonly OtherClass _other = new();

                    public void Test()
                    {
                        _other.Session(() =>
                        {
                            DoSomething();
                        });
                    }

                    private void DoSomething() {}
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var invocations = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .ToList();
            // Get the DoSomething() invocation inside the lambda
            var doSomethingInvocation = invocations.FirstOrDefault(i =>
                SymbolHelpers.GetInvocationMethodName(i) == "DoSomething");

            var result = SymbolHelpers.IsInsideStreamSession(semanticModel, doSomethingInvocation!);

            Assert.False(result);
        }
    }

    public class GetRelevantBaseTypeName
    {
        [Fact]
        public void Should_return_aggregate_full_name_for_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace TestDomain;

                public class TestAggregate(IEventStream stream) : Aggregate(stream)
                {
                }
                """);

            var result = SymbolHelpers.GetRelevantBaseTypeName(classSymbol);

            Assert.Equal(TypeConstants.AggregateFullName, result);
        }

        [Fact]
        public void Should_return_projection_full_name_for_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : Projection
                {
                }
                """);

            var result = SymbolHelpers.GetRelevantBaseTypeName(classSymbol);

            Assert.Equal(TypeConstants.ProjectionFullName, result);
        }

        [Fact]
        public void Should_return_routed_projection_full_name_for_routed_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : RoutedProjection
                {
                }
                """);

            var result = SymbolHelpers.GetRelevantBaseTypeName(classSymbol);

            Assert.Equal(TypeConstants.RoutedProjectionFullName, result);
        }

        [Fact]
        public void Should_return_null_for_regular_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """);

            var result = SymbolHelpers.GetRelevantBaseTypeName(classSymbol);

            Assert.Null(result);
        }

        [Fact]
        public void Should_return_null_for_null()
        {
            var result = SymbolHelpers.GetRelevantBaseTypeName(null);

            Assert.Null(result);
        }
    }

    public class IsPartialClass
    {
        [Fact]
        public void Should_return_true_for_partial_class()
        {
            var classDecl = GetClassDeclaration(
                """
                namespace TestDomain;

                public partial class TestClass
                {
                }
                """);

            var result = SymbolHelpers.IsPartialClass(classDecl);

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_non_partial_class()
        {
            var classDecl = GetClassDeclaration(
                """
                namespace TestDomain;

                public class TestClass
                {
                }
                """);

            var result = SymbolHelpers.IsPartialClass(classDecl);

            Assert.False(result);
        }
    }

    public class IsGeneratedFile
    {
        [Fact]
        public void Should_return_true_for_generated_file()
        {
            var result = SymbolHelpers.IsGeneratedFile("TestAggregate.Generated.cs");

            Assert.True(result);
        }

        [Fact]
        public void Should_return_true_for_generated_file_case_insensitive()
        {
            var result = SymbolHelpers.IsGeneratedFile("TestAggregate.GENERATED.CS");

            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_for_regular_file()
        {
            var result = SymbolHelpers.IsGeneratedFile("TestAggregate.cs");

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_null()
        {
            var result = SymbolHelpers.IsGeneratedFile(null);

            Assert.False(result);
        }

        [Fact]
        public void Should_return_false_for_empty_string()
        {
            var result = SymbolHelpers.IsGeneratedFile(string.Empty);

            Assert.False(result);
        }
    }

    public class GetFullNamespace
    {
        [Fact]
        public void Should_return_namespace_for_class_in_namespace()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain.SubNamespace;

                public class TestClass
                {
                }
                """);

            var result = SymbolHelpers.GetFullNamespace(classSymbol!);

            Assert.Equal("TestDomain.SubNamespace", result);
        }

        [Fact]
        public void Should_return_empty_for_global_namespace()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                public class TestClass
                {
                }
                """);

            var result = SymbolHelpers.GetFullNamespace(classSymbol!);

            Assert.Equal(string.Empty, result);
        }
    }

    public class GetFullTypeName
    {
        [Fact]
        public void Should_return_type_name_for_non_nested_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class TestClass
                {
                }
                """);

            var result = SymbolHelpers.GetFullTypeName(classSymbol!);

            Assert.Equal("TestClass", result);
        }

        [Fact]
        public void Should_return_containing_type_and_name_for_nested_class()
        {
            var code = """
                namespace TestDomain;

                public class OuterClass
                {
                    public class InnerClass
                    {
                    }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var innerClassNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Last();
            var classSymbol = semanticModel.GetDeclaredSymbol(innerClassNode);

            var result = SymbolHelpers.GetFullTypeName(classSymbol!);

            Assert.Equal("OuterClass.InnerClass", result);
        }
    }

    public class GetFullTypeNameWithGenerics
    {
        [Fact]
        public void Should_return_type_name_for_non_generic_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class TestClass
                {
                }
                """);

            var result = SymbolHelpers.GetFullTypeNameWithGenerics(classSymbol!);

            Assert.Equal("TestClass", result);
        }

        [Fact]
        public void Should_return_type_name_with_generic_arguments()
        {
            var code = """
                namespace TestDomain;

                public class GenericClass<T>
                {
                }

                public class TestClass
                {
                    public GenericClass<string> Field;
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var fieldNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First();
            var typeInfo = semanticModel.GetTypeInfo(fieldNode.Declaration.Type);

            var result = SymbolHelpers.GetFullTypeNameWithGenerics(typeInfo.Type!);

            Assert.Equal("GenericClass<String>", result);
        }
    }

    public class GetInvocationMethodName
    {
        [Fact]
        public void Should_return_method_name_for_simple_invocation()
        {
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public void Test()
                    {
                        DoSomething();
                    }

                    private void DoSomething() {}
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var invocation = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .First();

            var result = SymbolHelpers.GetInvocationMethodName(invocation);

            Assert.Equal("DoSomething", result);
        }

        [Fact]
        public void Should_return_method_name_for_member_access_invocation()
        {
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public void Test()
                    {
                        this.DoSomething();
                    }

                    private void DoSomething() {}
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var invocation = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .First();

            var result = SymbolHelpers.GetInvocationMethodName(invocation);

            Assert.Equal("DoSomething", result);
        }

        [Fact]
        public void Should_return_null_for_complex_expression()
        {
            // Test case where invocation.Expression is not IdentifierNameSyntax or MemberAccessExpressionSyntax
            var code = """
                namespace TestDomain;

                public class TestClass
                {
                    public System.Func<int> GetFunc() => () => 42;

                    public void Test()
                    {
                        GetFunc()();
                    }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var invocations = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .ToList();
            // Get the outer invocation (GetFunc()())
            var outerInvocation = invocations.FirstOrDefault(i =>
                i.Expression is InvocationExpressionSyntax);

            var result = SymbolHelpers.GetInvocationMethodName(outerInvocation!);

            Assert.Null(result);
        }
    }

    public class IsAggregateOrProjectionTypeAdditional
    {
        [Fact]
        public void Should_return_true_for_routed_projection_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                using ErikLieben.FA.ES.Projections;

                namespace TestDomain;

                public class TestProjection : RoutedProjection
                {
                }
                """);

            var result = SymbolHelpers.IsAggregateOrProjectionType(classSymbol);

            Assert.True(result);
        }
    }

    public class IsOrInheritsFromAggregateAdditional
    {
        [Fact]
        public void Should_return_false_for_non_aggregate_class()
        {
            var (classSymbol, _) = GetClassSymbol(
                """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """);

            var result = SymbolHelpers.IsOrInheritsFromAggregate(classSymbol);

            Assert.False(result);
        }
    }

    public class IsAggregateClassAdditional
    {
        [Fact]
        public void Should_return_false_for_non_aggregate_class_declaration()
        {
            var code = """
                namespace TestDomain;

                public class RegularClass
                {
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var classDecl = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First();

            var result = SymbolHelpers.IsAggregateClass(classDecl, semanticModel);

            Assert.False(result);
        }
    }

    public class GetFullTypeNameWithGenericsAdditional
    {
        [Fact]
        public void Should_return_type_name_with_nested_generic_arguments()
        {
            var code = """
                namespace TestDomain;

                public class GenericClass<T>
                {
                }

                public class TestClass
                {
                    public GenericClass<GenericClass<int>> Field;
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var fieldNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .First();
            var typeInfo = semanticModel.GetTypeInfo(fieldNode.Declaration.Type);

            var result = SymbolHelpers.GetFullTypeNameWithGenerics(typeInfo.Type!);

            Assert.Equal("GenericClass<GenericClass<Int32>>", result);
        }
    }

    public class GetFullTypeNameAdditional
    {
        [Fact]
        public void Should_return_deeply_nested_type_name()
        {
            var code = """
                namespace TestDomain;

                public class Level1
                {
                    public class Level2
                    {
                        public class Level3
                        {
                        }
                    }
                }
                """;
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [syntaxTree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var level3ClassNode = syntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Last();
            var classSymbol = semanticModel.GetDeclaredSymbol(level3ClassNode);

            var result = SymbolHelpers.GetFullTypeName(classSymbol!);

            Assert.Equal("Level1.Level2.Level3", result);
        }
    }
}
