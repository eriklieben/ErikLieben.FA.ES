using System.Collections.Generic;
using System.IO;
using System.Linq;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class TypeCollectorTests
{
    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    private static INamedTypeSymbol GetNamedTypeSymbol(string code, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TypesAsm",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(n => n.Identifier.Text == typeName);
        return (INamedTypeSymbol)model.GetDeclaredSymbol(node)!;
    }

    [Fact]
    public void Should_collect_nested_and_generic_types_while_skipping_noise()
    {
        // Arrange
        var code = """
            using System;
            using System.Collections.Generic;

            public class InnerA { public int X { get; set; } }
            public class InnerB { public InnerA A { get; set; } = new(); }

            public class Holder
            {
                public List<InnerB> Items { get; set; } = new();
            }
            """;
        var symbol = GetNamedTypeSymbol(code, "Holder");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        // Expect Holder itself, List, InnerB, InnerA, Int32 (primitive is allowed by helper), maybe more system types skipped
        Assert.Contains(set, t => t.Name == "Holder");
        Assert.Contains(set, t => t.Name == "InnerB");
        Assert.Contains(set, t => t.Name == "InnerA");
    }

    [Fact]
    public void Should_handle_null_type_symbol()
    {
        // Arrange
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(null, set);

        // Assert
        Assert.Empty(set);
    }

    [Fact]
    public void Should_skip_interface_types()
    {
        // Arrange
        var code = """
            public interface IMyInterface { }
            public class MyClass : IMyInterface { }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TypesAsm",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var interfaceNode = tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().First();
        var interfaceSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(interfaceNode)!;
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(interfaceSymbol, set);

        // Assert - interface should be skipped
        Assert.Empty(set);
    }

    [Fact]
    public void Should_not_add_same_type_twice()
    {
        // Arrange
        var code = """
            public class SimpleClass { public int Value { get; set; } }
            """;
        var symbol = GetNamedTypeSymbol(code, "SimpleClass");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Add type first
        TypeCollector.GetAllTypesInClass(symbol, set);
        var initialCount = set.Count;

        // Act - try to add again
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert - count should not change
        Assert.Equal(initialCount, set.Count);
    }

    [Fact]
    public void Should_collect_array_element_types()
    {
        // Arrange
        var code = """
            public class Element { public string Name { get; set; } = ""; }
            public class Container { public Element[] Elements { get; set; } = []; }
            """;
        var symbol = GetNamedTypeSymbol(code, "Container");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "Container");
        Assert.Contains(set, t => t.Name == "Element");
    }

    [Fact]
    public void Should_process_base_type()
    {
        // Arrange
        var code = """
            public class BaseClass { public int BaseValue { get; set; } }
            public class DerivedClass : BaseClass { public string Name { get; set; } = ""; }
            """;
        var symbol = GetNamedTypeSymbol(code, "DerivedClass");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "DerivedClass");
        Assert.Contains(set, t => t.Name == "BaseClass");
    }

    [Fact]
    public void Should_skip_nullable_types_as_noise()
    {
        // Arrange
        var code = """
            public class MyClass { public int? NullableValue { get; set; } }
            """;
        var symbol = GetNamedTypeSymbol(code, "MyClass");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "MyClass");
        // Nullable<int> should not be collected - it's filtered as noise
        Assert.DoesNotContain(set, t => t.Name.Contains("Nullable"));
    }

    [Fact]
    public void Should_handle_generic_type_with_custom_type_argument()
    {
        // Arrange
        var code = """
            using System.Collections.Generic;
            public class CustomType { public int Id { get; set; } }
            public class GenericHolder { public List<CustomType> Items { get; set; } = []; }
            """;
        var symbol = GetNamedTypeSymbol(code, "GenericHolder");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "GenericHolder");
        Assert.Contains(set, t => t.Name == "CustomType");
    }

    [Fact]
    public void Should_handle_string_property_as_system_type()
    {
        // Arrange
        var code = """
            public class WithString { public string Text { get; set; } = ""; }
            """;
        var symbol = GetNamedTypeSymbol(code, "WithString");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "WithString");
        // String is a system type but should be collected with special handling
        Assert.Contains(set, t => t.SpecialType == SpecialType.System_String);
    }

    [Fact]
    public void Should_handle_multiple_primitive_properties()
    {
        // Arrange
        var code = """
            public class Primitives
            {
                public int IntVal { get; set; }
                public bool BoolVal { get; set; }
                public double DoubleVal { get; set; }
                public decimal DecVal { get; set; }
                public byte ByteVal { get; set; }
                public char CharVal { get; set; }
                public float FloatVal { get; set; }
                public short ShortVal { get; set; }
                public long LongVal { get; set; }
                public uint UIntVal { get; set; }
                public ulong ULongVal { get; set; }
                public ushort UShortVal { get; set; }
            }
            """;
        var symbol = GetNamedTypeSymbol(code, "Primitives");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "Primitives");
    }

    [Fact]
    public void Should_handle_enum_property()
    {
        // Arrange
        var code = """
            public enum Status { Active, Inactive }
            public class WithEnum { public Status CurrentStatus { get; set; } }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TypesAsm",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var classNode = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classNode)!;
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "WithEnum");
        Assert.Contains(set, t => t.Name == "Status");
    }

    [Fact]
    public void Should_handle_struct_property()
    {
        // Arrange
        var code = """
            public struct Point { public int X { get; set; } public int Y { get; set; } }
            public class WithStruct { public Point Location { get; set; } }
            """;
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TypesAsm",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var classNode = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(classNode)!;
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "WithStruct");
        Assert.Contains(set, t => t.Name == "Point");
    }

    [Fact]
    public void Should_handle_nested_generic_types()
    {
        // Arrange - Using a custom generic wrapper instead of Dictionary/List which are from System.Collections.Generic
        // Types from System.Collections.Generic namespace are filtered out by IsFromUnwantedNamespace
        var code = """
            public class Inner { public int Value { get; set; } }
            public class Wrapper<T> { public T Item { get; set; } = default!; }
            public class Outer { public Wrapper<Inner> Data { get; set; } = new(); }
            """;
        var symbol = GetNamedTypeSymbol(code, "Outer");
        var set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Act
        TypeCollector.GetAllTypesInClass(symbol, set);

        // Assert
        Assert.Contains(set, t => t.Name == "Outer");
        Assert.Contains(set, t => t.Name == "Inner");
        Assert.Contains(set, t => t.Name == "Wrapper");
    }
}
