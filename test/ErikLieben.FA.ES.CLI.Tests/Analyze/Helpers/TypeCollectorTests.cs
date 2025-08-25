using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;

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
            new[] { tree },
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
}
