using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class ConstructorHelperTests
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

    public class GetConstructorsMethod : ConstructorHelperTests
    {
        [Fact]
        public void Should_return_empty_for_class_with_no_explicit_constructor()
        {
            // Arrange
            var code = """
                public class Simple { }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert - There's always an implicit default constructor
            Assert.Single(constructors);
            Assert.Empty(constructors[0].Parameters);
        }

        [Fact]
        public void Should_return_constructor_with_simple_parameters()
        {
            // Arrange
            var code = """
                public class Person
                {
                    public Person(string name, int age) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Equal(2, constructors[0].Parameters.Count);

            var nameParam = constructors[0].Parameters[0];
            Assert.Equal("name", nameParam.Name);
            Assert.Equal("String", nameParam.Type);
            Assert.Equal("System", nameParam.Namespace);

            var ageParam = constructors[0].Parameters[1];
            Assert.Equal("age", ageParam.Name);
            Assert.Equal("Int32", ageParam.Type);
            Assert.Equal("System", ageParam.Namespace);
        }

        [Fact]
        public void Should_return_multiple_constructors()
        {
            // Arrange
            var code = """
                public class MultiCtor
                {
                    public MultiCtor() { }
                    public MultiCtor(string name) { }
                    public MultiCtor(string name, int id) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Equal(3, constructors.Count);
        }

        [Fact]
        public void Should_detect_nullable_reference_types()
        {
            // Arrange
            var code = """
                #nullable enable
                public class NullableDemo
                {
                    public NullableDemo(string? nullableName, string nonNullableName) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Equal(2, constructors[0].Parameters.Count);

            var nullableParam = constructors[0].Parameters[0];
            Assert.Equal("nullableName", nullableParam.Name);
            Assert.True(nullableParam.IsNullable);

            var nonNullableParam = constructors[0].Parameters[1];
            Assert.Equal("nonNullableName", nonNullableParam.Name);
            // Reference types are nullable by default unless constrained
        }

        [Fact]
        public void Should_handle_nullable_value_types()
        {
            // Arrange
            var code = """
                public class NullableValueDemo
                {
                    public NullableValueDemo(int? nullableInt, int nonNullableInt) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Equal(2, constructors[0].Parameters.Count);

            var nullableParam = constructors[0].Parameters[0];
            Assert.Equal("nullableInt", nullableParam.Name);
            // The type is Nullable<Int32>
            Assert.Contains("Nullable", nullableParam.Type);

            var nonNullableParam = constructors[0].Parameters[1];
            Assert.Equal("nonNullableInt", nonNullableParam.Name);
            Assert.Equal("Int32", nonNullableParam.Type);
        }

        [Fact]
        public void Should_handle_generic_parameters()
        {
            // Arrange
            var code = """
                using System.Collections.Generic;
                public class GenericDemo
                {
                    public GenericDemo(List<string> items) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Single(constructors[0].Parameters);

            var param = constructors[0].Parameters[0];
            Assert.Equal("items", param.Name);
            Assert.Equal("List", param.Type);
        }

        [Fact]
        public void Should_handle_guid_parameters()
        {
            // Arrange
            var code = """
                using System;
                public class GuidDemo
                {
                    public GuidDemo(Guid id) { }
                }
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Single(constructors[0].Parameters);

            var param = constructors[0].Parameters[0];
            Assert.Equal("id", param.Name);
            Assert.Equal("Guid", param.Type);
            Assert.Equal("System", param.Namespace);
        }

        [Fact]
        public void Should_handle_primary_constructor()
        {
            // Arrange
            var code = """
                public class PrimaryCtor(string name, int age);
                """;
            var classSymbol = GetClassSymbol(code);
            Assert.NotNull(classSymbol);

            // Act
            var constructors = ConstructorHelper.GetConstructors(classSymbol!).ToList();

            // Assert
            Assert.Single(constructors);
            Assert.Equal(2, constructors[0].Parameters.Count);
        }
    }
}
