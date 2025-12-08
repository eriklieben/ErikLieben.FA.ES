using System.Reflection;
using System.Text.Json;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Binding;

public class ProjectionBinderTests
{
    public class TestProjection : Projection
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

    private static HttpContext CreateHttpContext(IServiceProvider serviceProvider, RouteValueDictionary? routeValues = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.RouteValues = routeValues ?? new RouteValueDictionary();
        return context;
    }

    private static void TestMethodNoPattern([Projection] TestProjection projection) { }

    private static void TestMethodWithPattern([Projection("{id}")] TestProjection projection) { }

    private static void TestMethodNoCreate([Projection(CreateIfNotExists = false)] TestProjection projection) { }

    private static void TestMethodComplexPattern([Projection("{tenantId}/projections/{id}")] TestProjection projection) { }

    private static ParameterInfo GetParameterWithAttribute(string methodName)
    {
        var methodInfo = typeof(ProjectionBinderTests).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        return methodInfo!.GetParameters()[0];
    }

    public class BindAsync
    {
        [Fact]
        public async Task Should_bind_projection_with_generic_factory()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, null, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider);
            var parameter = GetParameterWithAttribute(nameof(TestMethodNoPattern));

            // Act
            var result = await ProjectionBinder.BindAsync<TestProjection>(context, parameter);

            // Assert
            Assert.NotNull(result);
            Assert.Same(testProjection, result);
        }

        [Fact]
        public async Task Should_substitute_route_values_in_blob_name_pattern()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, "order123", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "order123" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethodWithPattern));

            // Act
            var result = await ProjectionBinder.BindAsync<TestProjection>(context, parameter);

            // Assert
            Assert.NotNull(result);
            await genericFactory.Received(1).GetOrCreateAsync(documentFactory, streamFactory, "order123", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_substitute_multiple_route_values_in_complex_pattern()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, "tenant1/projections/proj123", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary
            {
                { "tenantId", "tenant1" },
                { "id", "proj123" }
            });
            var parameter = GetParameterWithAttribute(nameof(TestMethodComplexPattern));

            // Act
            var result = await ProjectionBinder.BindAsync<TestProjection>(context, parameter);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_throw_ProjectionNotFoundException_when_CreateIfNotExists_is_false_and_not_exists()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.ExistsAsync(null, Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider);
            var parameter = GetParameterWithAttribute(nameof(TestMethodNoCreate));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ProjectionNotFoundException>(
                () => ProjectionBinder.BindAsync<TestProjection>(context, parameter).AsTask());

            Assert.Equal(typeof(TestProjection), exception.ProjectionType);
        }
    }

    public class BindCoreAsync
    {
        [Fact]
        public async Task Should_use_generic_factory_when_available()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, "test-blob", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var result = await ProjectionBinder.BindCoreAsync<TestProjection>(
                serviceProvider,
                "test-blob",
                createIfNotExists: true);

            // Assert
            Assert.NotNull(result);
            Assert.Same(testProjection, result);
        }

        [Fact]
        public async Task Should_fallback_to_non_generic_factory()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var nonGenericFactory = Substitute.For<IProjectionFactory>();
            nonGenericFactory.ProjectionType.Returns(typeof(TestProjection));
            nonGenericFactory.GetOrCreateProjectionAsync(documentFactory, streamFactory, "test-blob", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Projection>(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton<IProjectionFactory>(nonGenericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var result = await ProjectionBinder.BindCoreAsync<TestProjection>(
                serviceProvider,
                "test-blob",
                createIfNotExists: true);

            // Assert
            Assert.NotNull(result);
            Assert.Same(testProjection, result);
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_no_factory_registered()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ProjectionBinder.BindCoreAsync<TestProjection>(
                    serviceProvider,
                    "test-blob",
                    createIfNotExists: true));

            Assert.Contains("No projection factory registered", exception.Message);
            Assert.Contains("TestProjection", exception.Message);
        }

        [Fact]
        public async Task Should_check_existence_with_generic_factory_when_CreateIfNotExists_is_false()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.ExistsAsync("test-blob", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, "test-blob", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var result = await ProjectionBinder.BindCoreAsync<TestProjection>(
                serviceProvider,
                "test-blob",
                createIfNotExists: false);

            // Assert
            Assert.NotNull(result);
            await genericFactory.Received(1).ExistsAsync("test-blob", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_ProjectionNotFoundException_when_non_generic_factory_throws_not_found()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var nonGenericFactory = Substitute.For<IProjectionFactory>();
            nonGenericFactory.ProjectionType.Returns(typeof(TestProjection));
            nonGenericFactory.GetOrCreateProjectionAsync(documentFactory, streamFactory, "test-blob", Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Projection>(new Exception("Projection not found in storage")));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton<IProjectionFactory>(nonGenericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ProjectionNotFoundException>(
                () => ProjectionBinder.BindCoreAsync<TestProjection>(
                    serviceProvider,
                    "test-blob",
                    createIfNotExists: false));

            Assert.Equal(typeof(TestProjection), exception.ProjectionType);
            Assert.Equal("test-blob", exception.BlobName);
        }

        [Fact]
        public async Task Should_rethrow_non_not_found_exceptions_from_non_generic_factory()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var nonGenericFactory = Substitute.For<IProjectionFactory>();
            nonGenericFactory.ProjectionType.Returns(typeof(TestProjection));
            nonGenericFactory.GetOrCreateProjectionAsync(documentFactory, streamFactory, "test-blob", Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Projection>(new Exception("Network timeout occurred")));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton<IProjectionFactory>(nonGenericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => ProjectionBinder.BindCoreAsync<TestProjection>(
                    serviceProvider,
                    "test-blob",
                    createIfNotExists: false));

            Assert.Contains("Network timeout occurred", exception.Message);
        }

        [Fact]
        public async Task Should_create_projection_with_non_generic_factory_when_CreateIfNotExists_is_true()
        {
            // Arrange
            var testProjection = new TestProjection();
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var nonGenericFactory = Substitute.For<IProjectionFactory>();
            nonGenericFactory.ProjectionType.Returns(typeof(TestProjection));
            nonGenericFactory.GetOrCreateProjectionAsync(documentFactory, streamFactory, null, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Projection>(testProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton<IProjectionFactory>(nonGenericFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var result = await ProjectionBinder.BindCoreAsync<TestProjection>(
                serviceProvider,
                blobName: null,
                createIfNotExists: true);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_skip_non_matching_non_generic_factories()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            // Factory for different projection type
            var otherFactory = Substitute.For<IProjectionFactory>();
            otherFactory.ProjectionType.Returns(typeof(OtherProjection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton<IProjectionFactory>(otherFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ProjectionBinder.BindCoreAsync<TestProjection>(
                    serviceProvider,
                    "test-blob",
                    createIfNotExists: true));

            Assert.Contains("No projection factory registered", exception.Message);
        }

        public class OtherProjection : Projection
        {
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

            public override string ToJson() => "{}";
        }
    }
}
