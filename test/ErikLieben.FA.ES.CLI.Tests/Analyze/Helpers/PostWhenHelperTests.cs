using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class PostWhenHelperTests
{
    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    private static INamedTypeSymbol? GetClassSymbol(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();
        return model.GetDeclaredSymbol(classDecl);
    }

    public class GetPostWhenMethodMethod : PostWhenHelperTests
    {
        [Fact]
        public void Should_return_null_when_no_PostWhen_method()
        {
            // Arrange
            var code = """
                public class NoPostWhen
                {
                    public void OtherMethod() { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.GetPostWhenMethod(classSymbol!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_return_PostWhen_declaration_when_method_exists()
        {
            // Arrange
            var code = """
                public class WithPostWhen
                {
                    public void PostWhen() { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.GetPostWhenMethod(classSymbol!);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result!.Parameters);
        }

        [Fact]
        public void Should_capture_PostWhen_parameters()
        {
            // Arrange
            var code = """
                public class WithPostWhenParams
                {
                    public void PostWhen(string name, int count) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.GetPostWhenMethod(classSymbol!);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result!.Parameters.Count);

            var nameParam = result.Parameters[0];
            Assert.Equal("name", nameParam.Name);
            Assert.Equal("String", nameParam.Type);
            Assert.Equal("System", nameParam.Namespace);

            var countParam = result.Parameters[1];
            Assert.Equal("count", countParam.Name);
            Assert.Equal("Int32", countParam.Type);
            Assert.Equal("System", countParam.Namespace);
        }

        [Fact]
        public void Should_use_first_PostWhen_when_multiple_overloads_exist()
        {
            // Arrange
            var code = """
                public class MultiplePostWhen
                {
                    public void PostWhen() { }
                    public void PostWhen(string name) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.GetPostWhenMethod(classSymbol!);

            // Assert
            Assert.NotNull(result);
            // The first one (no parameters) should be used
            Assert.Empty(result!.Parameters);
        }

        [Fact]
        public void Should_handle_complex_parameter_types()
        {
            // Arrange
            var code = """
                using System;
                using System.Collections.Generic;
                public class ComplexPostWhen
                {
                    public void PostWhen(Guid id, List<string> items) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.GetPostWhenMethod(classSymbol!);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result!.Parameters.Count);

            var idParam = result.Parameters[0];
            Assert.Equal("id", idParam.Name);
            Assert.Equal("Guid", idParam.Type);

            var itemsParam = result.Parameters[1];
            Assert.Equal("items", itemsParam.Name);
            Assert.Equal("List", itemsParam.Type);
        }
    }

    public class HasPostWhenAllMethodMethod : PostWhenHelperTests
    {
        [Fact]
        public void Should_return_false_when_no_PostWhenAll_method()
        {
            // Arrange
            var code = """
                public class NoPostWhenAll
                {
                    public void OtherMethod() { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.HasPostWhenAllMethod(classSymbol!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Should_return_true_when_PostWhenAll_method_exists_without_GeneratedCode_attribute()
        {
            // Arrange
            var code = """
                public class WithPostWhenAll
                {
                    public void PostWhenAll() { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.HasPostWhenAllMethod(classSymbol!);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Should_return_false_when_PostWhenAll_has_GeneratedCode_attribute()
        {
            // Arrange
            var code = """
                using System.CodeDom.Compiler;
                public class WithGeneratedPostWhenAll
                {
                    [GeneratedCode("Generator", "1.0")]
                    public void PostWhenAll() { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var result = PostWhenHelper.HasPostWhenAllMethod(classSymbol!);

            // Assert
            Assert.False(result);
        }
    }
}
