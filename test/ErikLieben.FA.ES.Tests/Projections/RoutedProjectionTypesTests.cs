using System;
using System.Collections.Generic;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class RoutedProjectionTypesTests
{
    public class DestinationMetadataTests
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var metadata = new DestinationMetadata();

            // Assert
            Assert.Equal(string.Empty, metadata.DestinationTypeName);
            Assert.Equal(default, metadata.CreatedAt);
            Assert.Equal(default, metadata.LastModified);
            Assert.Null(metadata.CheckpointFingerprint);
            Assert.NotNull(metadata.Metadata);
            Assert.Empty(metadata.Metadata);
            Assert.NotNull(metadata.UserMetadata);
            Assert.Empty(metadata.UserMetadata);
        }

        [Fact]
        public void Should_set_and_get_all_properties()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;

            // Act
            var metadata = new DestinationMetadata
            {
                DestinationTypeName = "TestProjection",
                CreatedAt = now,
                LastModified = now.AddHours(1),
                CheckpointFingerprint = "fingerprint-123",
                Metadata = new Dictionary<string, string> { ["blobPath"] = "test/path.json" },
                UserMetadata = new Dictionary<string, string> { ["language"] = "en-GB" }
            };

            // Assert
            Assert.Equal("TestProjection", metadata.DestinationTypeName);
            Assert.Equal(now, metadata.CreatedAt);
            Assert.Equal(now.AddHours(1), metadata.LastModified);
            Assert.Equal("fingerprint-123", metadata.CheckpointFingerprint);
            Assert.Equal("test/path.json", metadata.Metadata["blobPath"]);
            Assert.Equal("en-GB", metadata.UserMetadata["language"]);
        }
    }

    public class DestinationRegistryTests
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var registry = new DestinationRegistry();

            // Assert
            Assert.NotNull(registry.Destinations);
            Assert.Empty(registry.Destinations);
            Assert.Equal(default, registry.LastUpdated);
        }

        [Fact]
        public void Should_add_and_retrieve_destinations()
        {
            // Arrange
            var registry = new DestinationRegistry();
            var metadata = new DestinationMetadata
            {
                DestinationTypeName = "TestProjection",
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Act
            registry.Destinations["dest-key-1"] = metadata;
            registry.LastUpdated = DateTimeOffset.UtcNow;

            // Assert
            Assert.Single(registry.Destinations);
            Assert.Equal("TestProjection", registry.Destinations["dest-key-1"].DestinationTypeName);
        }

        [Fact]
        public void Should_serialize_with_json_property_names()
        {
            // Arrange
            var registry = new DestinationRegistry
            {
                Destinations = new Dictionary<string, DestinationMetadata>
                {
                    ["key1"] = new DestinationMetadata { DestinationTypeName = "Type1" }
                },
                LastUpdated = DateTimeOffset.Parse("2024-01-15T10:30:00Z")
            };

            // Act
            var json = JsonSerializer.Serialize(registry);

            // Assert
            Assert.Contains("\"destinations\"", json);
            Assert.Contains("\"lastUpdated\"", json);
        }
    }

    public class RouteTargetTests
    {
        [Fact]
        public void Should_have_default_values()
        {
            // Arrange & Act
            var target = new RouteTarget();

            // Assert
            Assert.Equal(string.Empty, target.DestinationKey);
            Assert.Null(target.CustomEvent);
            Assert.NotNull(target.Metadata);
            Assert.Empty(target.Metadata);
            Assert.Null(target.Context);
        }

        [Fact]
        public void Should_set_destination_key()
        {
            // Arrange & Act
            var target = new RouteTarget
            {
                DestinationKey = "en-GB"
            };

            // Assert
            Assert.Equal("en-GB", target.DestinationKey);
        }

        [Fact]
        public void Should_allow_custom_event()
        {
            // Arrange
            var customEvent = Substitute.For<IEvent>();
            customEvent.EventType.Returns("CustomEventType");

            // Act
            var target = new RouteTarget
            {
                DestinationKey = "dest-1",
                CustomEvent = customEvent
            };

            // Assert
            Assert.NotNull(target.CustomEvent);
            Assert.Equal("CustomEventType", target.CustomEvent.EventType);
        }

        [Fact]
        public void Should_allow_metadata()
        {
            // Arrange & Act
            var target = new RouteTarget
            {
                DestinationKey = "dest-1",
                Metadata = new Dictionary<string, object>
                {
                    ["priority"] = 1,
                    ["category"] = "high"
                }
            };

            // Assert
            Assert.Equal(2, target.Metadata.Count);
            Assert.Equal(1, target.Metadata["priority"]);
            Assert.Equal("high", target.Metadata["category"]);
        }

        [Fact]
        public void Should_allow_custom_context()
        {
            // Arrange
            var context = Substitute.For<IExecutionContext>();

            // Act
            var target = new RouteTarget
            {
                DestinationKey = "dest-1",
                Context = context
            };

            // Assert
            Assert.NotNull(target.Context);
        }
    }

    public class RoutedProjectionMetadataTests
    {
        [Fact]
        public void Should_have_default_registry()
        {
            // Arrange & Act
            var metadata = new RoutedProjectionMetadata();

            // Assert
            Assert.NotNull(metadata.Registry);
            Assert.Empty(metadata.Registry.Destinations);
        }

        [Fact]
        public void Should_serialize_with_json_property_name()
        {
            // Arrange
            var metadata = new RoutedProjectionMetadata();
            metadata.Registry.Destinations["key1"] = new DestinationMetadata
            {
                DestinationTypeName = "TestType"
            };

            // Act
            var json = JsonSerializer.Serialize(metadata);

            // Assert
            Assert.Contains("\"registry\"", json);
        }
    }
}
