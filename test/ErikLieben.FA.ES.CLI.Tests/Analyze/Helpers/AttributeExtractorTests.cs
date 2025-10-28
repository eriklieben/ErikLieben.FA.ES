using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.InteropServices;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class AttributeExtractorTests
{
    public class ExtractEventStreamTypeAttribute
    {
        [Fact]
        public void Should_return_null_when_attribute_not_present()
        {
            // Arrange
            var code = @"
namespace App { public class TestClass { } }
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_extract_all_values_when_using_single_all_parameter_constructor()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamTypeAttribute : System.Attribute {
        public EventStreamTypeAttribute(string all) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamType(""blob"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("blob", result.StreamType);
            Assert.Equal("blob", result.DocumentType);
            Assert.Equal("blob", result.DocumentTagType);
            Assert.Equal("blob", result.EventStreamTagType);
            Assert.Equal("blob", result.DocumentRefType);
        }

        [Fact]
        public void Should_extract_stream_type_from_named_argument()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamTypeAttribute : System.Attribute {
        public string? StreamType { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamType(StreamType = ""blob"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("blob", result.StreamType);
        }

        [Fact]
        public void Should_extract_document_type_from_named_argument()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamTypeAttribute : System.Attribute {
        public string? DocumentType { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamType(DocumentType = ""cosmos"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("cosmos", result.DocumentType);
        }

        [Fact]
        public void Should_extract_all_named_arguments()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamTypeAttribute : System.Attribute {
        public string? StreamType { get; set; }
        public string? DocumentType { get; set; }
        public string? DocumentTagType { get; set; }
        public string? EventStreamTagType { get; set; }
        public string? DocumentRefType { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamType(
        StreamType = ""blob"",
        DocumentType = ""cosmos"",
        DocumentTagType = ""table"",
        EventStreamTagType = ""tag-store"",
        DocumentRefType = ""ref-store"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("blob", result.StreamType);
            Assert.Equal("cosmos", result.DocumentType);
            Assert.Equal("table", result.DocumentTagType);
            Assert.Equal("tag-store", result.EventStreamTagType);
            Assert.Equal("ref-store", result.DocumentRefType);
        }

        [Fact]
        public void Should_ignore_unknown_named_arguments()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamTypeAttribute : System.Attribute {
        public string? StreamType { get; set; }
        public string? UnknownProperty { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamType(
        StreamType = ""blob"",
        UnknownProperty = ""ignored"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamTypeAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("blob", result.StreamType);
        }
    }

    public class ExtractEventStreamBlobSettingsAttribute
    {
        [Fact]
        public void Should_return_null_when_attribute_not_present()
        {
            // Arrange
            var code = @"
namespace App { public class TestClass { } }
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(symbol);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_extract_all_values_when_using_single_all_parameter_constructor()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamBlobSettingsAttribute : System.Attribute {
        public EventStreamBlobSettingsAttribute(string all) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamBlobSettings(""Store1"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Store1", result.DataStore);
            Assert.Equal("Store1", result.DocumentStore);
            Assert.Equal("Store1", result.DocumentTagStore);
            Assert.Equal("Store1", result.StreamTagStore);
            Assert.Equal("Store1", result.SnapShotStore);
        }

        [Fact]
        public void Should_extract_data_store_from_named_argument()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamBlobSettingsAttribute : System.Attribute {
        public string? DataStore { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamBlobSettings(DataStore = ""Store1"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Store1", result.DataStore);
        }

        [Fact]
        public void Should_extract_all_named_arguments()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamBlobSettingsAttribute : System.Attribute {
        public string? DataStore { get; set; }
        public string? DocumentStore { get; set; }
        public string? DocumentTagStore { get; set; }
        public string? StreamTagStore { get; set; }
        public string? SnapShotStore { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamBlobSettings(
        DataStore = ""Store1"",
        DocumentStore = ""DocStore"",
        DocumentTagStore = ""TagStore"",
        StreamTagStore = ""StreamStore"",
        SnapShotStore = ""SnapshotStore"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Store1", result.DataStore);
            Assert.Equal("DocStore", result.DocumentStore);
            Assert.Equal("TagStore", result.DocumentTagStore);
            Assert.Equal("StreamStore", result.StreamTagStore);
            Assert.Equal("SnapshotStore", result.SnapShotStore);
        }

        [Fact]
        public void Should_ignore_unknown_named_arguments()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES {
    public class EventStreamBlobSettingsAttribute : System.Attribute {
        public string? DataStore { get; set; }
        public string? UnknownProperty { get; set; }
    }
}
namespace App {
    [ErikLieben.FA.ES.EventStreamBlobSettings(
        DataStore = ""Store1"",
        UnknownProperty = ""ignored"")]
    public class TestClass { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventStreamBlobSettingsAttribute(symbol);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Store1", result.DataStore);
        }
    }

    private static INamedTypeSymbol GetTypeSymbol(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Last();

        return semanticModel.GetDeclaredSymbol(classDeclaration)!;
    }

    private static List<PortableExecutableReference> References { get; } = new()
    {
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    };
}
