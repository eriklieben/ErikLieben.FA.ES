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

public class ParameterHelperTests
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

    private static (IMethodSymbol?, SemanticModel) GetMethodSymbol(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "ParamAsm",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        return (model.GetDeclaredSymbol(method), model);
    }

    [Fact]
    public void Should_capture_parameters_with_generics_and_subtypes_and_nullability()
    {
        // Arrange
        var (methodSymbol, _) = GetMethodSymbol(
            """
            using System;
            using System.Collections.Generic;
            
            public record Inner(string Value);
            public record Wrapper(Inner Data);
            
            public class Demo
            {
                public void Act(Wrapper wrapper, List<string> names, Guid? id) {}
            }
            """
        );
        Assert.NotNull(methodSymbol);

        // Act
        var parameters = ParameterHelper.GetParameters(methodSymbol!);

        // Assert
        Assert.Equal(3, parameters.Count);

        var p0 = parameters[0];
        Assert.Equal("wrapper", p0.Name);
        Assert.Equal("Wrapper", p0.Type);
        Assert.Equal("", p0.GenericTypes?.Count == 0 ? string.Empty : "has-generics");
        Assert.True(p0.IsNullable); // reference type
        Assert.True(p0.SubTypes.Count >= 1);

        var p1 = parameters[1];
        Assert.Equal("names", p1.Name);
        Assert.Equal("List", p1.Type);
        var g = Assert.Single(p1.GenericTypes);
        Assert.Equal("String", g.Name);
        Assert.Equal("System", g.Namespace);
        Assert.True(p1.IsNullable); // reference type

        var p2 = parameters[2];
        Assert.Equal("id", p2.Name);
        Assert.Equal("Nullable", p2.Type);
        Assert.False(p2.IsNullable); // Helper rule for System.Nullable<T>
    }
}
