using System.Collections.Generic;
using ErikLieben.FA.ES.Projections;
using Xunit;
using System.Linq;

namespace ErikLieben.FA.ES.Tests.Projections;

public class BlobPathTemplateResolverTests
{
    public class ResolveMethod
    {
        [Fact]
        public void Should_resolve_single_placeholder_with_dictionary()
        {
            // Arrange
            var template = "questions/{language}.json";
            var values = new Dictionary<string, string> { ["language"] = "en-GB" };

            // Act
            var result = BlobPathTemplateResolver.Resolve(template, values);

            // Assert
            Assert.Equal("questions/en-GB.json", result);
        }

        [Fact]
        public void Should_resolve_multiple_placeholders()
        {
            // Arrange
            var template = "projections/{entityType}/{language}.json";
            var values = new Dictionary<string, string>
            {
                ["entityType"] = "questions",
                ["language"] = "de-DE"
            };

            // Act
            var result = BlobPathTemplateResolver.Resolve(template, values);

            // Assert
            Assert.Equal("projections/questions/de-DE.json", result);
        }

        [Fact]
        public void Should_add_json_extension_if_missing()
        {
            // Arrange
            var template = "kanban/{projectId}";
            var values = new Dictionary<string, string> { ["projectId"] = "proj-123" };

            // Act
            var result = BlobPathTemplateResolver.Resolve(template, values);

            // Assert
            Assert.Equal("kanban/proj-123.json", result);
        }

        [Fact]
        public void Should_not_add_json_extension_if_already_present()
        {
            // Arrange
            var template = "data/{id}.json";
            var values = new Dictionary<string, string> { ["id"] = "test" };

            // Act
            var result = BlobPathTemplateResolver.Resolve(template, values);

            // Assert
            Assert.Equal("data/test.json", result);
        }

        [Fact]
        public void Should_resolve_with_partitionKey_shorthand()
        {
            // Arrange
            var template = "items/{partitionKey}.json";

            // Act
            var result = BlobPathTemplateResolver.Resolve(template, "partition-value");

            // Assert
            Assert.Equal("items/partition-value.json", result);
        }
    }

    public class GetPlaceholdersMethod
    {
        [Fact]
        public void Should_extract_single_placeholder()
        {
            // Arrange
            var template = "questions/{language}.json";

            // Act
            var placeholders = BlobPathTemplateResolver.GetPlaceholders(template).ToList();

            // Assert
            Assert.Single(placeholders);
            Assert.Equal("language", placeholders[0]);
        }

        [Fact]
        public void Should_extract_multiple_placeholders()
        {
            // Arrange
            var template = "projections/{entityType}/{language}.json";

            // Act
            var placeholders = BlobPathTemplateResolver.GetPlaceholders(template).ToList();

            // Assert
            Assert.Equal(2, placeholders.Count);
            Assert.Contains("entityType", placeholders);
            Assert.Contains("language", placeholders);
        }

        [Fact]
        public void Should_return_empty_for_template_without_placeholders()
        {
            // Arrange
            var template = "static/data.json";

            // Act
            var placeholders = BlobPathTemplateResolver.GetPlaceholders(template).ToList();

            // Assert
            Assert.Empty(placeholders);
        }
    }

    public class ExtractValuesMethod
    {
        [Fact]
        public void Should_return_empty_dictionary_for_non_matching_path()
        {
            // Arrange
            var template = "questions/{language}.json";
            var resolvedPath = "answers/en-GB.json"; // Different prefix

            // Act
            var values = BlobPathTemplateResolver.ExtractValues(template, resolvedPath);

            // Assert
            // Non-matching path returns empty dictionary
            Assert.Empty(values);
        }

        [Fact]
        public void Should_handle_simple_template_without_placeholders()
        {
            // Arrange
            var template = "static/data.json";
            var resolvedPath = "static/data.json";

            // Act
            var values = BlobPathTemplateResolver.ExtractValues(template, resolvedPath);

            // Assert
            // No placeholders = empty values, but still matches
            Assert.Empty(values);
        }
    }

    public class GetContainerNameMethod
    {
        [Fact]
        public void Should_extract_container_name()
        {
            // Arrange
            var template = "projections/questions/{language}.json";

            // Act
            var containerName = BlobPathTemplateResolver.GetContainerName(template);

            // Assert
            Assert.Equal("projections", containerName);
        }

        [Fact]
        public void Should_return_first_part_as_container()
        {
            // Arrange
            var template = "mycontainer/{id}.json";

            // Act
            var containerName = BlobPathTemplateResolver.GetContainerName(template);

            // Assert
            Assert.Equal("mycontainer", containerName);
        }

        [Fact]
        public void Should_return_default_for_empty_template()
        {
            // Arrange
            var template = "";

            // Act
            var containerName = BlobPathTemplateResolver.GetContainerName(template);

            // Assert
            Assert.Equal("projections", containerName);
        }
    }
}
