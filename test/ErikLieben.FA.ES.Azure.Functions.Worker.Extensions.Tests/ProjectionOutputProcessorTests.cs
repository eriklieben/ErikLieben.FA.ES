using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class ProjectionOutputProcessorTests
{
    // Test projection for testing - fully implements Projection abstract members
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

    private sealed class TestProjectionFactory : IProjectionFactory<TestProjection>, IProjectionFactory
    {
        private readonly TestProjection _projection;
        public string? LastBlobName { get; private set; }
        public bool SaveAsyncCalled { get; private set; }

        public TestProjectionFactory(TestProjection projection)
        {
            _projection = projection;
        }

        public Type ProjectionType => typeof(TestProjection);

        public Task<TestProjection> GetOrCreateAsync(
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory,
            string? blobName = null,
            CancellationToken cancellationToken = default)
        {
            LastBlobName = blobName;
            return Task.FromResult(_projection);
        }

        public Task<Projection> GetOrCreateProjectionAsync(
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory,
            string? blobName = null,
            CancellationToken cancellationToken = default)
        {
            LastBlobName = blobName;
            return Task.FromResult<Projection>(_projection);
        }

        public Task SaveAsync(TestProjection projection, string? blobName = null, CancellationToken cancellationToken = default)
        {
            SaveAsyncCalled = true;
            LastBlobName = blobName;
            return Task.CompletedTask;
        }

        public Task SaveProjectionAsync(Projection projection, string? blobName = null, CancellationToken cancellationToken = default)
        {
            SaveAsyncCalled = true;
            LastBlobName = blobName;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<DateTimeOffset?> GetLastModifiedAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task SetStatusAsync(ProjectionStatus status, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProjectionStatus> GetStatusAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectionStatus.Active);
    }

    public class Constructor : ProjectionOutputProcessorTests
    {
        [Fact]
        public void Should_throw_ArgumentNullException_when_serviceProvider_is_null()
        {
            // Arrange
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionOutputProcessor(null!, docFactory, streamFactory, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_objectDocumentFactory_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionOutputProcessor(services, null!, streamFactory, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_eventStreamFactory_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var logger = Substitute.For<ILogger>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionOutputProcessor(services, docFactory, null!, logger));
        }

        [Fact]
        public void Should_throw_ArgumentNullException_when_logger_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionOutputProcessor(services, docFactory, streamFactory, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            // Act
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ProcessProjectionOutputsAsyncMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public async Task Should_return_immediately_when_outputAttributes_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            // Act
            await sut.ProcessProjectionOutputsAsync(null!, "TestFunction");

            // Assert - method completes without exception
            Assert.True(true);
        }

        [Fact]
        public async Task Should_return_immediately_when_outputAttributes_is_empty()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            // Act
            await sut.ProcessProjectionOutputsAsync(Array.Empty<ProjectionOutputAttribute>(), "TestFunction");

            // Assert - method completes without exception
            Assert.True(true);
        }

        [Fact]
        public async Task Should_update_and_save_projection()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attributes = new List<ProjectionOutputAttribute>
            {
                new ProjectionOutputAttribute(typeof(TestProjection))
            };

            // Act
            await sut.ProcessProjectionOutputsAsync(attributes, "TestFunction");

            // Assert - verify factory was used to get projection and save was called
            Assert.True(factory.SaveAsyncCalled);
        }

        [Fact]
        public async Task Should_not_save_when_SaveAfterUpdate_is_false()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attributes = new List<ProjectionOutputAttribute>
            {
                new ProjectionOutputAttribute(typeof(TestProjection)) { SaveAfterUpdate = false }
            };

            // Act
            await sut.ProcessProjectionOutputsAsync(attributes, "TestFunction");

            // Assert - save should NOT be called when SaveAfterUpdate is false
            Assert.False(factory.SaveAsyncCalled);
        }

        [Fact]
        public async Task Should_pass_blobName_to_factory()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attributes = new List<ProjectionOutputAttribute>
            {
                new ProjectionOutputAttribute(typeof(TestProjection)) { BlobName = "custom-blob.json" }
            };

            // Act
            await sut.ProcessProjectionOutputsAsync(attributes, "TestFunction");

            // Assert
            Assert.Equal("custom-blob.json", factory.LastBlobName);
        }

        [Fact]
        public async Task Should_throw_AggregateException_when_no_factory_registered()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            var attributes = new List<ProjectionOutputAttribute>
            {
                new ProjectionOutputAttribute(typeof(TestProjection))
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AggregateException>(() =>
                sut.ProcessProjectionOutputsAsync(attributes, "TestFunction"));
            Assert.Contains("Failed to update", ex.Message);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attributes = new List<ProjectionOutputAttribute>
            {
                new ProjectionOutputAttribute(typeof(TestProjection))
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.ProcessProjectionOutputsAsync(attributes, "TestFunction", cts.Token));
        }
    }

    public class LoadAndUpdateProjectionAsyncMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_attribute_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.LoadAndUpdateProjectionAsync(null!));
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_no_factory_registered()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            var attribute = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.LoadAndUpdateProjectionAsync(attribute));
            Assert.Contains("No projection factory registered", ex.Message);
        }

        [Fact]
        public async Task Should_load_and_update_projection()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attribute = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act
            var result = await sut.LoadAndUpdateProjectionAsync(attribute);

            // Assert - verify the projection was retrieved from factory
            Assert.Same(testProjection, result);
        }
    }

    public class SaveProjectionAsyncMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public async Task Should_throw_ArgumentNullException_when_projection_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            var attribute = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SaveProjectionAsync(null!, attribute));
        }

        [Fact]
        public async Task Should_throw_ArgumentNullException_when_attribute_is_null()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            var projection = new TestProjection();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                sut.SaveProjectionAsync(projection, null!));
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_no_factory_found()
        {
            // Arrange
            var services = new ServiceCollection().BuildServiceProvider();
            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();
            var sut = new ProjectionOutputProcessor(services, docFactory, streamFactory, logger);

            var projection = new TestProjection();
            var attribute = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SaveProjectionAsync(projection, attribute));
            Assert.Contains("No projection factory found", ex.Message);
        }

        [Fact]
        public async Task Should_save_projection_using_factory()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attribute = new ProjectionOutputAttribute(typeof(TestProjection)) { BlobName = "save-blob.json" };

            // Act
            await sut.SaveProjectionAsync(testProjection, attribute);

            // Assert
            Assert.True(factory.SaveAsyncCalled);
            Assert.Equal("save-blob.json", factory.LastBlobName);
        }
    }

    public class ResolveTargetMethodMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public void Should_return_null_when_entryPoint_is_null()
        {
            // Arrange & Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_return_null_when_entryPoint_is_empty()
        {
            // Arrange & Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_return_null_when_entryPoint_has_no_dot()
        {
            // Arrange & Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod("MethodNameOnly");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_return_null_when_type_not_found()
        {
            // Arrange & Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod("NonExistent.Namespace.ClassName.MethodName");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Should_resolve_method_from_valid_entryPoint()
        {
            // Arrange
            var entryPoint = $"{typeof(TestClassWithMethod).FullName}.{nameof(TestClassWithMethod.TestMethod)}";

            // Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod(entryPoint);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(nameof(TestClassWithMethod.TestMethod), result!.Name);
        }

        [Fact]
        public void Should_resolve_static_method()
        {
            // Arrange
            var entryPoint = $"{typeof(TestClassWithMethod).FullName}.{nameof(TestClassWithMethod.StaticTestMethod)}";

            // Act
            var result = ProjectionOutputProcessor.ResolveTargetMethod(entryPoint);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(nameof(TestClassWithMethod.StaticTestMethod), result!.Name);
        }
    }

    public class GetProjectionOutputAttributesMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public void Should_return_empty_array_when_method_is_null()
        {
            // Arrange & Act
            var result = ProjectionOutputProcessor.GetProjectionOutputAttributes(null);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Should_return_empty_array_when_method_has_no_attributes()
        {
            // Arrange
            var method = typeof(TestClassWithMethod).GetMethod(nameof(TestClassWithMethod.TestMethod));

            // Act
            var result = ProjectionOutputProcessor.GetProjectionOutputAttributes(method);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Should_return_attributes_from_method()
        {
            // Arrange
            var method = typeof(TestClassWithMethod).GetMethod(nameof(TestClassWithMethod.MethodWithProjectionOutput));

            // Act
            var result = ProjectionOutputProcessor.GetProjectionOutputAttributes(method);

            // Assert
            Assert.Single(result);
            Assert.Equal(typeof(TestProjection), result[0].ProjectionType);
        }

        [Fact]
        public void Should_return_multiple_attributes_from_method()
        {
            // Arrange
            var method = typeof(TestClassWithMethod).GetMethod(nameof(TestClassWithMethod.MethodWithMultipleProjectionOutputs));

            // Act
            var result = ProjectionOutputProcessor.GetProjectionOutputAttributes(method);

            // Assert
            Assert.Equal(2, result.Count);
        }
    }

    public class GetProjectionUsingNonGenericFactoryMethod : ProjectionOutputProcessorTests
    {
        [Fact]
        public async Task Should_use_non_generic_factory_when_generic_not_available()
        {
            // Arrange
            var testProjection = new TestProjection();
            var factory = new TestProjectionFactory(testProjection);

            var services = new ServiceCollection();
            // Only register non-generic factory
            services.AddSingleton<IProjectionFactory>(factory);
            var serviceProvider = services.BuildServiceProvider();

            var docFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();
            var logger = Substitute.For<ILogger>();

            var sut = new ProjectionOutputProcessor(serviceProvider, docFactory, streamFactory, logger);

            var attribute = new ProjectionOutputAttribute(typeof(TestProjection));

            // Act
            var result = await sut.LoadAndUpdateProjectionAsync(attribute);

            // Assert
            Assert.Same(testProjection, result);
        }
    }

    // Helper class for method resolution tests
    public class TestClassWithMethod
    {
        public void TestMethod() { }

        public static void StaticTestMethod() { }

        [ProjectionOutput(typeof(TestProjection))]
        public void MethodWithProjectionOutput() { }

        [ProjectionOutput(typeof(TestProjection))]
        [ProjectionOutput(typeof(TestProjection), BlobName = "second.json")]
        public void MethodWithMultipleProjectionOutputs() { }
    }
}
