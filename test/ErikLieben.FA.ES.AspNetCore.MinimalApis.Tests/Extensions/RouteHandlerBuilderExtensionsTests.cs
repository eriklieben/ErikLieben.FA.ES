using System.Text.Json;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Extensions;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Filters;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Extensions;

public class RouteHandlerBuilderExtensionsTests
{
    private class TestProjection : Projection
    {
        public TestProjection() : base() { }

        public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory) { }

        private Checkpoint _checkpoint = new();
        public override Checkpoint Checkpoint { get => _checkpoint; set => _checkpoint = value; }

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories
            => new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document)
        {
            return Task.CompletedTask;
        }

        public override string ToJson() => JsonSerializer.Serialize(this);
    }

    public class WithProjectionOutput_NoParams
    {
        [Fact]
        public void Should_add_filter_to_builder()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>();

            // Assert
            Assert.NotNull(result);
            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act - can chain multiple extensions
            var result = builder
                .WithProjectionOutput<TestProjection>()
                .WithName("testEndpoint");

            // Assert
            Assert.NotNull(result);
        }
    }

    public class WithProjectionOutput_WithBlobNamePattern
    {
        [Fact]
        public void Should_add_filter_with_pattern()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>("{id}");

            // Assert
            Assert.NotNull(result);
            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_accept_complex_patterns()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>("{tenantId}/projections/{id}");

            // Assert
            Assert.NotNull(result);
        }
    }

    public class WithProjectionOutput_FullConfig
    {
        [Fact]
        public void Should_add_filter_with_pattern_and_save_option()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>("{id}", saveAfterUpdate: true);

            // Assert
            Assert.NotNull(result);
            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_add_filter_with_null_pattern_and_save_false()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>(null, saveAfterUpdate: false);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void Should_accept_pattern_with_save_false()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.WithProjectionOutput<TestProjection>("{id}", saveAfterUpdate: false);

            // Assert
            Assert.NotNull(result);
        }
    }

    public class AndUpdateProjectionToLatest_NoParams
    {
        [Fact]
        public void Should_add_filter_to_builder()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.AndUpdateProjectionToLatest<TestProjection>();

            // Assert
            Assert.NotNull(result);
            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder
                .AndUpdateProjectionToLatest<TestProjection>()
                .WithName("testEndpoint");

            // Assert
            Assert.NotNull(result);
        }
    }

    public class AndUpdateProjectionToLatest_WithBlobNamePattern
    {
        [Fact]
        public void Should_add_filter_with_pattern()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.AndUpdateProjectionToLatest<TestProjection>("{id}");

            // Assert
            Assert.NotNull(result);
            Assert.Same(builder, result);
        }

        [Fact]
        public void Should_accept_complex_patterns()
        {
            // Arrange
            var builder = CreateRouteHandlerBuilder();

            // Act
            var result = builder.AndUpdateProjectionToLatest<TestProjection>("{tenantId}/projections/{orderId}");

            // Assert
            Assert.NotNull(result);
        }
    }

    private static RouteHandlerBuilder CreateRouteHandlerBuilder()
    {
        // Create a minimal app and map an endpoint to get a RouteHandlerBuilder
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        return app.MapGet("/test", () => "Hello");
    }
}
