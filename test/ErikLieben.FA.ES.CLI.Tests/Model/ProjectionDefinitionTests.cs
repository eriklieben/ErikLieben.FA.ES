using ErikLieben.FA.ES.CLI.Model;

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
                DestinationsWithExternalCheckpoint = new HashSet<string> { "TranslationProjection" }
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
}
