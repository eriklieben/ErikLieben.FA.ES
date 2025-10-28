using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class AggregateSettingsCodeGeneratorTests
{
    public class ExtractEventStreamTypeSettings
    {
        [Fact]
        public void Should_not_add_assignments_when_attribute_is_null()
        {
            // Arrange
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(null, assignments);

            // Assert
            Assert.Empty(assignments);
        }

        [Fact]
        public void Should_add_stream_type_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData { StreamType = "blob" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.StreamType = \"blob\";", assignments);
        }

        [Fact]
        public void Should_add_document_type_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData { DocumentType = "cosmos" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DocumentType = \"cosmos\";", assignments);
        }

        [Fact]
        public void Should_add_document_tag_type_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData { DocumentTagType = "table" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DocumentTagType = \"table\";", assignments);
        }

        [Fact]
        public void Should_add_event_stream_tag_type_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData { EventStreamTagType = "tag-store" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.EventStreamTagType = \"tag-store\";", assignments);
        }

        [Fact]
        public void Should_add_document_ref_type_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData { DocumentRefType = "ref-store" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DocumentRefType = \"ref-store\";", assignments);
        }

        [Fact]
        public void Should_add_all_assignments_when_all_properties_specified()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData
            {
                StreamType = "blob",
                DocumentType = "cosmos",
                DocumentTagType = "table",
                EventStreamTagType = "tag-store",
                DocumentRefType = "ref-store"
            };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Equal(5, assignments.Count);
            Assert.Contains("document.Active.StreamType = \"blob\";", assignments);
            Assert.Contains("document.Active.DocumentType = \"cosmos\";", assignments);
            Assert.Contains("document.Active.DocumentTagType = \"table\";", assignments);
            Assert.Contains("document.Active.EventStreamTagType = \"tag-store\";", assignments);
            Assert.Contains("document.Active.DocumentRefType = \"ref-store\";", assignments);
        }

        [Fact]
        public void Should_not_add_assignments_for_null_properties()
        {
            // Arrange
            var attribute = new EventStreamTypeAttributeData
            {
                StreamType = "blob",
                DocumentType = null,  // Explicitly null
                DocumentTagType = null
            };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamTypeSettings(attribute, assignments);

            // Assert
            Assert.Single(assignments);
            Assert.Contains("document.Active.StreamType = \"blob\";", assignments);
        }
    }

    public class ExtractEventStreamBlobSettings
    {
        [Fact]
        public void Should_not_add_assignments_when_attribute_is_null()
        {
            // Arrange
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(null, assignments);

            // Assert
            Assert.Empty(assignments);
        }

        [Fact]
        public void Should_add_data_store_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData { DataStore = "Store1" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DataStore = \"Store1\";", assignments);
        }

        [Fact]
        public void Should_add_document_store_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData { DocumentStore = "DocStore" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DocumentStore = \"DocStore\";", assignments);
        }

        [Fact]
        public void Should_add_document_tag_store_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData { DocumentTagStore = "TagStore" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.DocumentTagStore = \"TagStore\";", assignments);
        }

        [Fact]
        public void Should_add_stream_tag_store_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData { StreamTagStore = "StreamStore" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.StreamTagStore = \"StreamStore\";", assignments);
        }

        [Fact]
        public void Should_add_snapshot_store_assignment_when_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData { SnapShotStore = "SnapshotStore" };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Contains("document.Active.SnapShotStore = \"SnapshotStore\";", assignments);
        }

        [Fact]
        public void Should_add_all_assignments_when_all_properties_specified()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData
            {
                DataStore = "Store1",
                DocumentStore = "DocStore",
                DocumentTagStore = "TagStore",
                StreamTagStore = "StreamStore",
                SnapShotStore = "SnapshotStore"
            };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Equal(5, assignments.Count);
            Assert.Contains("document.Active.DataStore = \"Store1\";", assignments);
            Assert.Contains("document.Active.DocumentStore = \"DocStore\";", assignments);
            Assert.Contains("document.Active.DocumentTagStore = \"TagStore\";", assignments);
            Assert.Contains("document.Active.StreamTagStore = \"StreamStore\";", assignments);
            Assert.Contains("document.Active.SnapShotStore = \"SnapshotStore\";", assignments);
        }

        [Fact]
        public void Should_not_add_assignments_for_null_properties()
        {
            // Arrange
            var attribute = new EventStreamBlobSettingsAttributeData
            {
                DataStore = "Store1",
                DocumentStore = null,  // Explicitly null
                DocumentTagStore = null
            };
            var assignments = new List<string>();

            // Act
            AggregateSettingsCodeGenerator.ExtractEventStreamBlobSettings(attribute, assignments);

            // Assert
            Assert.Single(assignments);
            Assert.Contains("document.Active.DataStore = \"Store1\";", assignments);
        }
    }

    public class BuildSettingsCodeBlock
    {
        [Fact]
        public void Should_generate_code_block_with_empty_assignments()
        {
            // Arrange
            var assignments = new List<string>();

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            Assert.Contains("// Apply aggregate-specific settings for new documents", result);
            Assert.Contains("if (document.Active.CurrentStreamVersion == -1)", result);
            Assert.Contains("await this.objectDocumentFactory.SetAsync(document);", result);
        }

        [Fact]
        public void Should_generate_code_block_with_single_assignment()
        {
            // Arrange
            var assignments = new List<string> { "document.Active.StreamType = \"blob\";" };

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            Assert.Contains("document.Active.StreamType = \"blob\";", result);
            Assert.Contains("await this.objectDocumentFactory.SetAsync(document);", result);
        }

        [Fact]
        public void Should_generate_code_block_with_multiple_assignments()
        {
            // Arrange
            var assignments = new List<string>
            {
                "document.Active.StreamType = \"blob\";",
                "document.Active.DataStore = \"Store1\";",
                "document.Active.DocumentType = \"cosmos\";"
            };

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            Assert.Contains("document.Active.StreamType = \"blob\";", result);
            Assert.Contains("document.Active.DataStore = \"Store1\";", result);
            Assert.Contains("document.Active.DocumentType = \"cosmos\";", result);
            Assert.Contains("await this.objectDocumentFactory.SetAsync(document);", result);
        }

        [Fact]
        public void Should_maintain_proper_indentation()
        {
            // Arrange
            var assignments = new List<string> { "document.Active.StreamType = \"blob\";" };

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            // Verify the code has proper spacing/indentation structure
            Assert.Contains("                                 // Apply aggregate-specific settings", result);
            Assert.Contains("                                 if (document.Active.CurrentStreamVersion == -1)", result);
            Assert.Contains("                                 {", result);
            Assert.Contains("                                     document.Active.StreamType = \"blob\";", result);
            Assert.Contains("                                     await this.objectDocumentFactory.SetAsync(document);", result);
            Assert.Contains("                                 }", result);
        }

        [Fact]
        public void Should_include_condition_check_for_new_documents()
        {
            // Arrange
            var assignments = new List<string> { "document.Active.StreamType = \"blob\";" };

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            Assert.Contains("if (document.Active.CurrentStreamVersion == -1)", result);
        }

        [Fact]
        public void Should_call_set_async_at_end()
        {
            // Arrange
            var assignments = new List<string> { "document.Active.StreamType = \"blob\";" };

            // Act
            var result = AggregateSettingsCodeGenerator.BuildSettingsCodeBlock(assignments);

            // Assert
            Assert.Contains("await this.objectDocumentFactory.SetAsync(document);", result);
            // Verify SetAsync appears after the assignments
            var streamTypeIndex = result.IndexOf("document.Active.StreamType", StringComparison.Ordinal);
            var setAsyncIndex = result.IndexOf("await this.objectDocumentFactory.SetAsync(document);", StringComparison.Ordinal);
            Assert.True(setAsyncIndex > streamTypeIndex);
        }
    }
}
