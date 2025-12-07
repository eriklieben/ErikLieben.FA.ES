using System.Collections.Generic;
using System.IO;
using System.Linq;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class PropertyHelperTests
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

    private static (INamedTypeSymbol?, SemanticModel) GetClassSymbol(string code)
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
            .Last();

        return (semanticModel.GetDeclaredSymbol(classNode), semanticModel);
    }

    [Fact]
    public void Should_detect_public_getter_properties_and_filter_private_internal_and_private_getter()
    {
        // Arrange
        var (classSymbol, _) = GetClassSymbol(
            """
            public class MyType
            {
                public string Allowed { get; set; } = string.Empty;
                private string PrivateProp { get; set; } = string.Empty;
                internal string InternalProp { get; set; } = string.Empty;
                public string PrivateGetter { private get; set; } = string.Empty;
                public string InternalGetter { internal get; set; } = string.Empty;
            }
            """
        );
        Assert.NotNull(classSymbol);

        // Act
        var properties = PropertyHelper.GetPublicGetterProperties(classSymbol!);

        // Assert
        var prop = Assert.Single(properties);
        Assert.Equal("Allowed", prop.Name);
        Assert.Equal("String", prop.Type);
        Assert.Equal("System", prop.Namespace);
        // Reference types are considered explicitly nullable in helper
        Assert.True(prop.IsNullable);
    }

    [Fact]
    public void Should_include_parent_properties_via_include_parent_definitions()
    {
        // Arrange
        var (classSymbol, _) = GetClassSymbol(
            """
            public class Parent
            {
                public string ParentOnly { get; set; } = string.Empty;
            }
            public class Child : Parent
            {
                public string ChildOnly { get; set; } = string.Empty;
            }
            """
        );
        Assert.NotNull(classSymbol);

        // Act
        var properties = PropertyHelper.GetPublicGetterPropertiesIncludeParentDefinitions(classSymbol!);

        // Assert
        Assert.Equal(2, properties.Count);
        Assert.Contains(properties, p => p.Name == "ParentOnly");
        Assert.Contains(properties, p => p.Name == "ChildOnly");
    }

    [Fact]
    public void Should_capture_generics_and_nullable_flags_and_subtypes()
    {
        // Arrange
        var (classSymbol, _) = GetClassSymbol(
            """
            using System;
            using System.Collections.Generic;
            
            public record Inner(string Value);
            public record Wrapper(Inner Data);
            
            public class Demo
            {
                public List<string> Names { get; } = new();
                public Guid? OptionalId { get; } = Guid.NewGuid();
                public Wrapper Record { get; } = new(new("x"));
            }
            """
        );
        Assert.NotNull(classSymbol);

        // Act
        var props = PropertyHelper.GetPublicGetterProperties(classSymbol!);

        // Assert
        Assert.Equal(3, props.Count);

        var names = props.First(p => p.Name == "Names");
        Assert.True(names.IsNullable); // reference type
        Assert.True(names.GenericTypes?.Count > 0);
        var gt = Assert.Single(names.GenericTypes);
        Assert.Equal("String", gt.Name);
        Assert.Equal("System", gt.Namespace);

        var optional = props.First(p => p.Name == "OptionalId");
        Assert.False(optional.IsNullable); // Helper treats System.Nullable<T> as not explicitly nullable
        Assert.Equal("Nullable", optional.Type);
        Assert.Equal("System", optional.Namespace);

        var record = props.First(p => p.Name == "Record");
        Assert.True(record.IsNullable); // reference type
        Assert.True(record.SubTypes.Count >= 1);
        Assert.Contains(record.SubTypes, s => s.Name == "Inner" || s.Name.EndsWith("Inner"));
    }
}
