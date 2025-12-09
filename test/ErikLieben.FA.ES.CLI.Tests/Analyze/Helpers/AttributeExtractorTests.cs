#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public class ExtractUseUpcasterAttributes
    {
        [Fact]
        public void Should_return_empty_list_when_no_attribute_present()
        {
            // Arrange
            var code = @"
namespace App { public class TestClass { } }
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractUseUpcasterAttributes(symbol);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Should_extract_single_upcaster_type_from_generic_attribute()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Upcasting { public interface IUpcastEvent { } }
namespace ErikLieben.FA.ES.Attributes {
    public class UseUpcasterAttribute<T> : System.Attribute where T : ErikLieben.FA.ES.Upcasting.IUpcastEvent, new() { }
}
namespace App.Upcasters {
    public class OrderV1ToV2Upcaster : ErikLieben.FA.ES.Upcasting.IUpcastEvent { }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.UseUpcaster<App.Upcasters.OrderV1ToV2Upcaster>]
    public class Order { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractUseUpcasterAttributes(symbol);

            // Assert
            Assert.Single(result);
            Assert.Equal("OrderV1ToV2Upcaster", result[0].TypeName);
            Assert.Equal("App.Upcasters", result[0].Namespace);
        }

        [Fact]
        public void Should_extract_multiple_upcaster_types_from_generic_attributes()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Upcasting { public interface IUpcastEvent { } }
namespace ErikLieben.FA.ES.Attributes {
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class UseUpcasterAttribute<T> : System.Attribute where T : ErikLieben.FA.ES.Upcasting.IUpcastEvent, new() { }
}
namespace App.Upcasters {
    public class OrderV1ToV2Upcaster : ErikLieben.FA.ES.Upcasting.IUpcastEvent { }
    public class OrderV2ToV3Upcaster : ErikLieben.FA.ES.Upcasting.IUpcastEvent { }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.UseUpcaster<App.Upcasters.OrderV1ToV2Upcaster>]
    [ErikLieben.FA.ES.Attributes.UseUpcaster<App.Upcasters.OrderV2ToV3Upcaster>]
    public class Order { }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractUseUpcasterAttributes(symbol);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, u => u.TypeName == "OrderV1ToV2Upcaster");
            Assert.Contains(result, u => u.TypeName == "OrderV2ToV3Upcaster");
        }
    }

    public class ExtractEventVersionAttribute
    {
        [Fact]
        public void Should_return_1_when_attribute_not_present()
        {
            // Arrange
            var code = @"
namespace App { public record TestEvent(string Name); }
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public void Should_extract_version_from_constructor_argument()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Attributes {
    public class EventVersionAttribute : System.Attribute {
        public EventVersionAttribute(int version) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.EventVersion(2)]
    public record TestEvent(string Name);
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public void Should_extract_version_3()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Attributes {
    public class EventVersionAttribute : System.Attribute {
        public EventVersionAttribute(int version) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.EventVersion(3)]
    public record TestEvent(string Name);
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public void Should_return_1_when_attribute_has_no_constructor_arguments()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Attributes {
    public class EventVersionAttribute : System.Attribute {
        public EventVersionAttribute() { }
    }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.EventVersion]
    public record TestEvent(string Name);
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public void Should_return_1_when_constructor_argument_is_not_int()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Attributes {
    public class EventVersionAttribute : System.Attribute {
        public EventVersionAttribute(string version) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.EventVersion(""v2"")]
    public record TestEvent(string Name);
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(1, result);
        }

        [Fact]
        public void Should_work_with_struct_events()
        {
            // Arrange
            var code = @"
namespace ErikLieben.FA.ES.Attributes {
    public class EventVersionAttribute : System.Attribute {
        public EventVersionAttribute(int version) { }
    }
}
namespace App {
    [ErikLieben.FA.ES.Attributes.EventVersion(5)]
    public struct TestEvent { public string Name; }
}
";
            var symbol = GetTypeSymbol(code);

            // Act
            var result = AttributeExtractor.ExtractEventVersionAttribute(symbol);

            // Assert
            Assert.Equal(5, result);
        }
    }

    private static INamedTypeSymbol GetTypeSymbol(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Last();

        return semanticModel.GetDeclaredSymbol(classDeclaration)!;
    }

    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];
}
