using System;
using System.Reflection;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class ProjectionOutputAttributeTests
{
    public class Constructor : ProjectionOutputAttributeTests
    {
        [Fact]
        public void Should_set_ProjectionType()
        {
            // Arrange
            var projectionType = typeof(TestProjection);

            // Act
            var sut = new ProjectionOutputAttribute(projectionType);

            // Assert
            Assert.Equal(projectionType, sut.ProjectionType);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_projectionType_is_null()
        {
            // Arrange
            Type? projectionType = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProjectionOutputAttribute(projectionType!));
        }

        [Fact]
        public void Should_set_default_values()
        {
            // Arrange
            var projectionType = typeof(TestProjection);

            // Act
            var sut = new ProjectionOutputAttribute(projectionType);

            // Assert
            Assert.Null(sut.BlobName);
            Assert.True(sut.SaveAfterUpdate);
        }
    }

    public class Properties : ProjectionOutputAttributeTests
    {
        [Fact]
        public void Should_allow_setting_BlobName()
        {
            // Arrange
            var sut = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act
            sut.BlobName = "custom-blob-name";

            // Assert
            Assert.Equal("custom-blob-name", sut.BlobName);
        }

        [Fact]
        public void Should_allow_setting_SaveAfterUpdate_to_false()
        {
            // Arrange
            var sut = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act
            sut.SaveAfterUpdate = false;

            // Assert
            Assert.False(sut.SaveAfterUpdate);
        }
    }

    public class AttributeMetadata : ProjectionOutputAttributeTests
    {
        [Fact]
        public void Should_have_AttributeUsage_targeting_Method()
        {
            // Arrange
            var attributeType = typeof(ProjectionOutputAttribute);

            // Act
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Method));
        }

        [Fact]
        public void Should_allow_multiple_attributes_on_same_method()
        {
            // Arrange
            var attributeType = typeof(ProjectionOutputAttribute);

            // Act
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage!.AllowMultiple);
        }
    }

    // Test projection used for testing - fully implements Projection abstract members
    private sealed class TestProjection : Projection
    {
        public TestProjection() : base() { }

        public override Checkpoint Checkpoint { get; set; } = new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = new();

        public override string ToJson() => "{}";
    }
}

public class ProjectionOutputAttributeGenericTests
{
    public class Constructor : ProjectionOutputAttributeGenericTests
    {
        [Fact]
        public void Should_set_ProjectionType_from_generic_parameter()
        {
            // Arrange & Act
            var sut = new ProjectionOutputAttribute<TestProjection>();

            // Assert
            Assert.Equal(typeof(TestProjection), sut.ProjectionType);
        }

        [Fact]
        public void Should_inherit_from_ProjectionOutputAttribute()
        {
            // Arrange & Act
            var sut = new ProjectionOutputAttribute<TestProjection>();

            // Assert
            Assert.IsAssignableFrom<ProjectionOutputAttribute>(sut);
        }

        [Fact]
        public void Should_set_default_values()
        {
            // Arrange & Act
            var sut = new ProjectionOutputAttribute<TestProjection>();

            // Assert
            Assert.Null(sut.BlobName);
            Assert.True(sut.SaveAfterUpdate);
        }
    }

    public class AttributeMetadata : ProjectionOutputAttributeGenericTests
    {
        [Fact]
        public void Should_have_AttributeUsage_targeting_Method()
        {
            // Arrange
            var attributeType = typeof(ProjectionOutputAttribute<>);

            // Act
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Method));
        }

        [Fact]
        public void Should_allow_multiple_attributes_on_same_method()
        {
            // Arrange
            var attributeType = typeof(ProjectionOutputAttribute<>);

            // Act
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            // Assert
            Assert.NotNull(usage);
            Assert.True(usage!.AllowMultiple);
        }
    }

    // Test projection used for testing - fully implements Projection abstract members
    private sealed class TestProjection : Projection
    {
        public TestProjection() : base() { }

        public override Checkpoint Checkpoint { get; set; } = new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = new();

        public override string ToJson() => "{}";
    }
}
