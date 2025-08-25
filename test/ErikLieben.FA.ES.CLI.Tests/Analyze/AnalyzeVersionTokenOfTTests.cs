using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeVersionTokenOfTTests
{
    public class Run
    {
        [Fact]
        public void Should_not_add_when_record_does_not_inherit_VersionTokenOfT()
        {
            // Arrange
            var code = @"
namespace App.Tokens { public record NotAToken; }
";
            var (recordSymbol, _, semanticModel, compilation) = GetRecordFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C\\Repo\\App\\Tokens\\NotAToken.cs");

            var sut = new AnalyzeVersionTokenOfT(recordSymbol!, @"C\\Repo\\App\\");
            var list = new List<VersionTokenDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_add_version_token_with_correct_fields_and_partial_flag()
        {
            // Arrange: define a generic base VersionToken<T> and a partial record inheriting from it
            var code = @"
using System;
namespace App.Framework { public abstract record VersionToken<T>; }
namespace App.Tokens {
    public partial record OrderVersion : App.Framework.VersionToken<Guid>;
}
";
            var (recordSymbol, _, semanticModel, compilation) = GetRecordFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C\\Repo\\App\\Tokens\\OrderVersion.cs");

            // NOTE: AnalyzeVersionTokenOfT only checks BaseType name and TypeArguments.Length, not namespace.
            // So we adapt the symbol to have BaseType named "VersionToken" by naming the base accordingly.
            // Our base is App.Framework.VersionToken<Guid>, so the Name is VersionToken and IsGenericType true.

            var sut = new AnalyzeVersionTokenOfT(recordSymbol!, @"C\\Repo\\App\\");
            var list = new List<VersionTokenDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var vt = list.First();
            Assert.Equal("OrderVersion", vt.Name);
            Assert.Equal("App.Tokens", vt.Namespace);
            Assert.True(vt.IsPartialClass);
            Assert.Equal("Guid", vt.GenericType);
            Assert.Equal("System", vt.NamespaceOfType);
            Assert.Contains(vt.FileLocations, p => p.Replace("\\\\","\\").EndsWith("Tokens\\OrderVersion.cs") || p.EndsWith("OrderVersion.cs"));
        }
    }

    private static (INamedTypeSymbol?, RecordDeclarationSyntax?, SemanticModel, CSharpCompilation?) GetRecordFromCode(
        string code,
        string testAssembly = "TestAssembly",
        string? filePath = "Test.cs")
    {
        var syntaxTree = string.IsNullOrWhiteSpace(filePath)
            ? CSharpSyntaxTree.ParseText(code)
            : SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), filePath);

        var compilation = CSharpCompilation.Create(
            testAssembly,
            new[] { syntaxTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var recordNode = syntaxTree.GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>().Last();

        return (semanticModel.GetDeclaredSymbol(recordNode), recordNode, semanticModel, compilation);
    }

    private static List<PortableExecutableReference> References { get; } = new()
    {
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    };
}
