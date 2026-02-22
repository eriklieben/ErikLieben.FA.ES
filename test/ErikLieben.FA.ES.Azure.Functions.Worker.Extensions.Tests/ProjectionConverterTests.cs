#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class ProjectionConverterTests
{
    private sealed class TestProjection : Projection
    {
        public TestProjection() : base() { }

        public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory) { }

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
            return Task.FromResult(_projection);
        }

        public Task<Projection> GetOrCreateProjectionAsync(
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory,
            string? blobName = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Projection>(_projection);
        }

        public Task SaveAsync(TestProjection projection, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveProjectionAsync(Projection projection, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ExistsAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<DateTimeOffset?> GetLastModifiedAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task SetStatusAsync(ProjectionStatus status, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProjectionStatus> GetStatusAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectionStatus.Active);
    }

    [Fact]
    public void Should_throw_when_bindingData_is_null()
    {
        // Arrange
        Microsoft.Azure.Functions.Worker.Core.ModelBindingData? binding = null;

        // Act
        Action act = () => ProjectionConverter.GetBindingDataContent(binding!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Should_throw_when_no_factory_registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();

        var sut = new ProjectionConverter(serviceProvider, docFactory, streamFactory);

        var data = new ProjectionData();

        // Act
        async Task<object?> Act() => await sut.ConvertModelBindingDataAsync(typeof(TestProjection), data);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Contains("No projection factory registered", ex.Message);
    }

    [Fact]
    public async Task Should_resolve_projection_using_generic_factory()
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

        var sut = new ProjectionConverter(serviceProvider, docFactory, streamFactory);

        var data = new ProjectionData();

        // Act
        var result = await sut.ConvertModelBindingDataAsync(typeof(TestProjection), data);

        // Assert
        Assert.Same(testProjection, result);
    }

    [Fact]
    public async Task Should_resolve_projection_using_non_generic_factory()
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

        var sut = new ProjectionConverter(serviceProvider, docFactory, streamFactory);

        var data = new ProjectionData();

        // Act
        var result = await sut.ConvertModelBindingDataAsync(typeof(TestProjection), data);

        // Assert
        Assert.Same(testProjection, result);
    }

    [Fact]
    public async Task Should_pass_blob_name_to_factory()
    {
        // Arrange
        var testProjection = new TestProjection();
        string? capturedBlobName = null;

        var factory = new BlobNameCapturingFactory(testProjection, blobName => capturedBlobName = blobName);

        var services = new ServiceCollection();
        services.AddSingleton<IProjectionFactory<TestProjection>>(factory);
        services.AddSingleton<IProjectionFactory>(factory);
        var serviceProvider = services.BuildServiceProvider();

        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();

        var sut = new ProjectionConverter(serviceProvider, docFactory, streamFactory);

        var data = new ProjectionData("custom-blob-name.json");

        // Act
        await sut.ConvertModelBindingDataAsync(typeof(TestProjection), data);

        // Assert
        Assert.Equal("custom-blob-name.json", capturedBlobName);
    }

    private sealed class BlobNameCapturingFactory : IProjectionFactory<TestProjection>, IProjectionFactory
    {
        private readonly TestProjection _projection;
        private readonly Action<string?> _onBlobName;

        public BlobNameCapturingFactory(TestProjection projection, Action<string?> onBlobName)
        {
            _projection = projection;
            _onBlobName = onBlobName;
        }

        public Type ProjectionType => typeof(TestProjection);

        public Task<TestProjection> GetOrCreateAsync(
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory,
            string? blobName = null,
            CancellationToken cancellationToken = default)
        {
            _onBlobName(blobName);
            return Task.FromResult(_projection);
        }

        public Task<Projection> GetOrCreateProjectionAsync(
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory,
            string? blobName = null,
            CancellationToken cancellationToken = default)
        {
            _onBlobName(blobName);
            return Task.FromResult<Projection>(_projection);
        }

        public Task SaveAsync(TestProjection projection, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveProjectionAsync(Projection projection, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ExistsAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<DateTimeOffset?> GetLastModifiedAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);

        public Task SetStatusAsync(ProjectionStatus status, string? blobName = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ProjectionStatus> GetStatusAsync(string? blobName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectionStatus.Active);
    }
}
