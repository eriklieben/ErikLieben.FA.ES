using System.IO;
using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeVersionTokenOfTJsonConverterTests
{
    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    private static (INamedTypeSymbol?, ClassDeclarationSyntax?, SemanticModel, CSharpCompilation?) GetClassFromCode(
        string code,
        string testAssembly = "TestAssembly",
        string? filePath = "Test.cs")
    {
        var syntaxTree = string.IsNullOrWhiteSpace(filePath)
            ? CSharpSyntaxTree.ParseText(code)
            : SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), filePath);

        var compilation = CSharpCompilation.Create(
            testAssembly,
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classNode = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();

        return (semanticModel.GetDeclaredSymbol(classNode), classNode, semanticModel, compilation);
    }

    public class Run : AnalyzeVersionTokenOfTJsonConverterTests
    {
        [Fact]
        public void Should_not_add_when_class_does_not_inherit_VersionTokenJsonConverterBase()
        {
            // Arrange
            var code = @"
namespace App.Converters { public class NotAConverter { } }
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\NotAConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_not_add_when_base_type_is_null()
        {
            // Arrange: object base type with no generics
            var code = @"
namespace App.Converters { public class SomeClass : object { } }
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\SomeClass.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_not_add_when_base_type_is_not_generic()
        {
            // Arrange
            var code = @"
namespace App.Framework { public class VersionTokenJsonConverterBase { } }
namespace App.Converters {
    public class MyConverter : App.Framework.VersionTokenJsonConverterBase { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\MyConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_not_add_when_base_type_has_wrong_name()
        {
            // Arrange
            var code = @"
namespace App.Framework { public class OtherBase<T> { } }
namespace App.Converters {
    public class MyConverter : App.Framework.OtherBase<int> { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\MyConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_add_converter_when_inheriting_VersionTokenJsonConverterBase_generic()
        {
            // Arrange
            var code = @"
using System;
namespace App.Framework { public abstract class VersionTokenJsonConverterBase<T> { } }
namespace App.Tokens { public record OrderVersion; }
namespace App.Converters {
    public class OrderVersionConverter : App.Framework.VersionTokenJsonConverterBase<App.Tokens.OrderVersion> { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\OrderVersionConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var converter = list.First();
            Assert.Equal("OrderVersionConverter", converter.Name);
            Assert.Equal("App.Converters", converter.Namespace);
            Assert.False(converter.IsPartialClass);
        }

        [Fact]
        public void Should_add_partial_converter_with_correct_flag()
        {
            // Arrange
            var code = @"
using System;
namespace App.Framework { public abstract class VersionTokenJsonConverterBase<T> { } }
namespace App.Tokens { public record OrderVersion; }
namespace App.Converters {
    public partial class OrderVersionConverter : App.Framework.VersionTokenJsonConverterBase<App.Tokens.OrderVersion> { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\OrderVersionConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var converter = list.First();
            Assert.True(converter.IsPartialClass);
        }

        [Fact]
        public void Should_include_file_locations()
        {
            // Arrange
            var code = @"
namespace App.Framework { public abstract class VersionTokenJsonConverterBase<T> { } }
namespace App.Tokens { public record OrderVersion; }
namespace App.Converters {
    public class OrderVersionConverter : App.Framework.VersionTokenJsonConverterBase<App.Tokens.OrderVersion> { }
}
";
            var filePath = @"C:\Repo\App\Converters\OrderVersionConverter.cs";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: filePath);

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.NotEmpty(list.First().FileLocations);
            Assert.Contains(list.First().FileLocations, f =>
                f.Contains("OrderVersionConverter.cs") || f.Contains("Converters"));
        }

        [Fact]
        public void Should_not_add_when_base_type_has_multiple_type_arguments()
        {
            // Arrange: Base type with 2 type arguments instead of 1
            var code = @"
namespace App.Framework { public abstract class VersionTokenJsonConverterBase<T1, T2> { } }
namespace App.Converters {
    public class MultiTypeConverter : App.Framework.VersionTokenJsonConverterBase<int, string> { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Converters\MultiTypeConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_correctly_get_namespace()
        {
            // Arrange
            var code = @"
namespace App.Framework { public abstract class VersionTokenJsonConverterBase<T> { } }
namespace App.Tokens { public record ProductVersion; }
namespace App.Domain.Converters {
    public class ProductVersionConverter : App.Framework.VersionTokenJsonConverterBase<App.Tokens.ProductVersion> { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetClassFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\Repo\App\Domain\Converters\ProductVersionConverter.cs");

            var sut = new AnalyzeVersionTokenOfTJsonConverter(classSymbol!, @"C:\Repo\App\");
            var list = new List<VersionTokenJsonConverterDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.Equal("App.Domain.Converters", list.First().Namespace);
        }
    }
}
