#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System.Collections.Generic;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Model;

public class ProjectionDefinitionTests
{
    public class DestinationProjectionDefinitionTests
    {
        [Fact]
        public void Should_default_to_not_destination_projection()
        {
            // Arrange & Act
            var definition = new DestinationProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace"
            };

            // Assert
            Assert.False(definition.IsDestinationProjection);
        }

        [Fact]
        public void Should_set_is_destination_projection()
        {
            // Arrange & Act
            var definition = new DestinationProjectionDefinition
            {
                IsDestinationProjection = true,
                Name = "TestDestination",
                Namespace = "Test.Namespace"
            };

            // Assert
            Assert.True(definition.IsDestinationProjection);
            Assert.Equal("TestDestination", definition.Name);
            Assert.Equal("Test.Namespace", definition.Namespace);
        }

        [Fact]
        public void Should_inherit_from_projection_definition()
        {
            // Arrange
            var definition = new DestinationProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace"
            };

            // Act & Assert
            Assert.IsAssignableFrom<ProjectionDefinition>(definition);
        }
    }

    public class RoutedProjectionDefinitionTests
    {
        [Fact]
        public void Should_have_default_collection_values()
        {
            // Arrange & Act
            var definition = new RoutedProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace"
            };

            // Assert
            Assert.False(definition.IsRoutedProjection);
            Assert.Null(definition.RouterType);
            Assert.Null(definition.DestinationType);
            Assert.Null(definition.PathTemplate);
            Assert.NotNull(definition.DestinationPathTemplates);
            Assert.Empty(definition.DestinationPathTemplates);
            Assert.NotNull(definition.DestinationsWithExternalCheckpoint);
            Assert.Empty(definition.DestinationsWithExternalCheckpoint);
        }

        [Fact]
        public void Should_set_all_properties()
        {
            // Arrange & Act
            var definition = new RoutedProjectionDefinition
            {
                IsRoutedProjection = true,
                RouterType = "LanguageRouter",
                DestinationType = "TranslationProjection",
                PathTemplate = "translations/{language}.json",
                Name = "RoutedTranslations",
                Namespace = "Test.Namespace",
                DestinationPathTemplates = new Dictionary<string, string>
                {
                    ["TranslationProjection"] = "translations/{language}.json"
                },
                DestinationsWithExternalCheckpoint = ["TranslationProjection"]
            };

            // Assert
            Assert.True(definition.IsRoutedProjection);
            Assert.Equal("LanguageRouter", definition.RouterType);
            Assert.Equal("TranslationProjection", definition.DestinationType);
            Assert.Equal("translations/{language}.json", definition.PathTemplate);
            Assert.Single(definition.DestinationPathTemplates);
            Assert.Equal("translations/{language}.json", definition.DestinationPathTemplates["TranslationProjection"]);
            Assert.Single(definition.DestinationsWithExternalCheckpoint);
            Assert.Contains("TranslationProjection", definition.DestinationsWithExternalCheckpoint);
        }

        [Fact]
        public void Should_inherit_from_projection_definition()
        {
            // Arrange
            var definition = new RoutedProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace"
            };

            // Act & Assert
            Assert.IsAssignableFrom<ProjectionDefinition>(definition);
        }

        [Fact]
        public void Should_support_multiple_destination_path_templates()
        {
            // Arrange & Act
            var definition = new RoutedProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace",
                DestinationPathTemplates = new Dictionary<string, string>
                {
                    ["Type1"] = "path1/{key}.json",
                    ["Type2"] = "path2/{key}.json",
                    ["Type3"] = "path3/{key}.json"
                }
            };

            // Assert
            Assert.Equal(3, definition.DestinationPathTemplates.Count);
        }
    }

    public class CosmosDbProjectionDefinitionTests
    {
        [Fact]
        public void Should_have_required_container_property()
        {
            // Arrange & Act
            var definition = new CosmosDbProjectionDefinition
            {
                Container = "projections",
                Connection = "cosmosdb"
            };

            // Assert
            Assert.Equal("projections", definition.Container);
            Assert.Equal("cosmosdb", definition.Connection);
        }

        [Fact]
        public void Should_have_default_partition_key_path()
        {
            // Arrange & Act
            var definition = new CosmosDbProjectionDefinition
            {
                Container = "projections",
                Connection = "cosmosdb"
            };

            // Assert
            Assert.Equal("/projectionName", definition.PartitionKeyPath);
        }

        [Fact]
        public void Should_allow_custom_partition_key_path()
        {
            // Arrange & Act
            var definition = new CosmosDbProjectionDefinition
            {
                Container = "projections",
                Connection = "cosmosdb",
                PartitionKeyPath = "/customKey"
            };

            // Assert
            Assert.Equal("/customKey", definition.PartitionKeyPath);
        }
    }

    public class ProjectionDefinitionCosmosDbPropertyTests
    {
        [Fact]
        public void Should_have_null_cosmosdb_projection_by_default()
        {
            // Arrange & Act
            var definition = new ProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace"
            };

            // Assert
            Assert.Null(definition.CosmosDbProjection);
        }

        [Fact]
        public void Should_allow_setting_cosmosdb_projection()
        {
            // Arrange & Act
            var definition = new ProjectionDefinition
            {
                Name = "Test",
                Namespace = "Test.Namespace",
                CosmosDbProjection = new CosmosDbProjectionDefinition
                {
                    Container = "projections",
                    Connection = "cosmosdb",
                    PartitionKeyPath = "/id"
                }
            };

            // Assert
            Assert.NotNull(definition.CosmosDbProjection);
            Assert.Equal("projections", definition.CosmosDbProjection.Container);
            Assert.Equal("cosmosdb", definition.CosmosDbProjection.Connection);
            Assert.Equal("/id", definition.CosmosDbProjection.PartitionKeyPath);
        }

        [Fact]
        public void Should_allow_blob_and_cosmosdb_to_be_mutually_exclusive()
        {
            // Arrange
            var blobDefinition = new ProjectionDefinition
            {
                Name = "BlobProjection",
                Namespace = "Test.Namespace",
                BlobProjection = new BlobProjectionDefinition
                {
                    Container = "container",
                    Connection = "blob"
                }
            };

            var cosmosDefinition = new ProjectionDefinition
            {
                Name = "CosmosDbProjection",
                Namespace = "Test.Namespace",
                CosmosDbProjection = new CosmosDbProjectionDefinition
                {
                    Container = "projections",
                    Connection = "cosmosdb"
                }
            };

            // Assert
            Assert.NotNull(blobDefinition.BlobProjection);
            Assert.Null(blobDefinition.CosmosDbProjection);

            Assert.Null(cosmosDefinition.BlobProjection);
            Assert.NotNull(cosmosDefinition.CosmosDbProjection);
        }
    }
}
