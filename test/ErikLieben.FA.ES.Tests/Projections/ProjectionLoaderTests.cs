using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ProjectionLoaderTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectionFactory<TestProjection> _projectionFactory;
    private readonly IObjectDocumentFactory _documentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;
    private readonly IProjectionStatusCoordinator _statusCoordinator;
    private readonly ProjectionOptions _options;

    public ProjectionLoaderTests()
    {
        _projectionFactory = Substitute.For<IProjectionFactory<TestProjection>>();
        _documentFactory = Substitute.For<IObjectDocumentFactory>();
        _eventStreamFactory = Substitute.For<IEventStreamFactory>();
        _statusCoordinator = Substitute.For<IProjectionStatusCoordinator>();
        _options = new ProjectionOptions();

        var services = new ServiceCollection();
        services.AddSingleton(_projectionFactory);
        services.AddSingleton(_documentFactory);
        services.AddSingleton(_eventStreamFactory);
        _serviceProvider = services.BuildServiceProvider();
    }

    private ProjectionLoader CreateLoader(ProjectionOptions? options = null)
    {
        var opts = Options.Create(options ?? _options);
        return new ProjectionLoader(_serviceProvider, _statusCoordinator, opts);
    }

    public class ConstructorTests : ProjectionLoaderTests
    {
        [Fact]
        public void Should_throw_when_serviceProvider_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionLoader(null!, _statusCoordinator));
        }

        [Fact]
        public void Should_throw_when_statusCoordinator_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProjectionLoader(_serviceProvider, null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = CreateLoader();
            Assert.NotNull(sut);
        }
    }

    public class GetAsyncTests : ProjectionLoaderTests
    {
        [Fact]
        public async Task Should_return_null_when_projection_not_found()
        {
            // Arrange
            _projectionFactory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(false);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetAsync<TestProjection>("obj-1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_projection_when_found()
        {
            // Arrange
            var projection = new TestProjection();
            _projectionFactory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(true);
            _projectionFactory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                null,
                Arg.Any<CancellationToken>())
                .Returns(projection);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetAsync<TestProjection>("obj-1");

            // Assert
            Assert.NotNull(result);
            Assert.Same(projection, result);
        }
    }

    public class GetVersionAsyncTests : ProjectionLoaderTests
    {
        [Fact]
        public async Task Should_return_null_when_versioned_projection_not_found()
        {
            // Arrange
            _projectionFactory.ExistsAsync("TestProjection_v2", Arg.Any<CancellationToken>())
                .Returns(false);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetVersionAsync<TestProjection>("obj-1", 2);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_versioned_projection_when_found()
        {
            // Arrange
            var projection = new TestProjection();
            _projectionFactory.ExistsAsync("TestProjection_v2", Arg.Any<CancellationToken>())
                .Returns(true);
            _projectionFactory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                "TestProjection_v2",
                Arg.Any<CancellationToken>())
                .Returns(projection);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetVersionAsync<TestProjection>("obj-1", 2);

            // Assert
            Assert.NotNull(result);
        }
    }

    public class GetVersionMetadataAsyncTests : ProjectionLoaderTests
    {
        [Fact]
        public async Task Should_return_default_metadata_when_no_status()
        {
            // Arrange
            _statusCoordinator.GetStatusAsync("TestProjection", "obj-1", Arg.Any<CancellationToken>())
                .Returns((ProjectionStatusInfo?)null);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetVersionMetadataAsync<TestProjection>("obj-1");

            // Assert
            Assert.Equal(1, result.ActiveVersion);
            Assert.Null(result.RebuildingVersion);
            Assert.Single(result.AllVersions);
        }

        [Fact]
        public async Task Should_include_rebuilding_version_when_rebuilding()
        {
            // Arrange
            var status = new ProjectionStatusInfo(
                "TestProjection", "obj-1",
                ProjectionStatus.Rebuilding,
                DateTimeOffset.UtcNow,
                1,
                RebuildInfo.Start(RebuildStrategy.BlueGreen));

            _statusCoordinator.GetStatusAsync("TestProjection", "obj-1", Arg.Any<CancellationToken>())
                .Returns(status);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetVersionMetadataAsync<TestProjection>("obj-1");

            // Assert
            Assert.Equal(1, result.ActiveVersion);
            Assert.Equal(2, result.RebuildingVersion);
            Assert.Equal(2, result.AllVersions.Count);
        }
    }

    public class GetWithVersionCheckAsyncTests : ProjectionLoaderTests
    {
        [Fact]
        public async Task Should_return_not_found_when_projection_does_not_exist()
        {
            // Arrange
            _projectionFactory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(false);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetWithVersionCheckAsync<TestProjection>("obj-1");

            // Assert
            Assert.Null(result.Projection);
            Assert.False(result.SchemaMismatch);
        }

        [Fact]
        public async Task Should_return_success_when_no_schema_mismatch()
        {
            // Arrange
            var projection = new TestProjection();
            _projectionFactory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(true);
            _projectionFactory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                null,
                Arg.Any<CancellationToken>())
                .Returns(projection);
            var sut = CreateLoader();

            // Act
            var result = await sut.GetWithVersionCheckAsync<TestProjection>("obj-1");

            // Assert
            Assert.NotNull(result.Projection);
            Assert.False(result.SchemaMismatch);
        }

        [Fact]
        public async Task Should_throw_when_schema_mismatch_and_behavior_is_throw()
        {
            // Arrange
            var projection = new SchemaVersionTestProjection(2) { SchemaVersion = 1 };
            var factory = Substitute.For<IProjectionFactory<SchemaVersionTestProjection>>();
            factory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(true);
            factory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                null,
                Arg.Any<CancellationToken>())
                .Returns(projection);

            var services = new ServiceCollection();
            services.AddSingleton(factory);
            services.AddSingleton(_documentFactory);
            services.AddSingleton(_eventStreamFactory);
            var sp = services.BuildServiceProvider();

            var options = new ProjectionOptions { SchemaMismatchBehavior = SchemaMismatchBehavior.Throw };
            var sut = new ProjectionLoader(sp, _statusCoordinator, Options.Create(options));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetWithVersionCheckAsync<SchemaVersionTestProjection>("obj-1"));
        }

        [Fact]
        public async Task Should_trigger_rebuild_when_schema_mismatch_and_behavior_is_auto_rebuild()
        {
            // Arrange
            var projection = new SchemaVersionTestProjection(2) { SchemaVersion = 1 };
            var factory = Substitute.For<IProjectionFactory<SchemaVersionTestProjection>>();
            factory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(true);
            factory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                null,
                Arg.Any<CancellationToken>())
                .Returns(projection);

            var services = new ServiceCollection();
            services.AddSingleton(factory);
            services.AddSingleton(_documentFactory);
            services.AddSingleton(_eventStreamFactory);
            var sp = services.BuildServiceProvider();

            var options = new ProjectionOptions { SchemaMismatchBehavior = SchemaMismatchBehavior.AutoRebuild };
            var sut = new ProjectionLoader(sp, _statusCoordinator, Options.Create(options));

            _statusCoordinator.StartRebuildAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<RebuildStrategy>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
                .Returns(RebuildToken.Create("SchemaVersionTestProjection", "obj-1", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromHours(1)));

            // Act
            var result = await sut.GetWithVersionCheckAsync<SchemaVersionTestProjection>("obj-1");

            // Assert
            Assert.NotNull(result.Projection);
            Assert.True(result.SchemaMismatch);
            await _statusCoordinator.Received(1).StartRebuildAsync(
                "SchemaVersionTestProjection",
                "obj-1",
                Arg.Any<RebuildStrategy>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_warn_when_schema_mismatch_and_behavior_is_warn()
        {
            // Arrange
            var projection = new SchemaVersionTestProjection(2) { SchemaVersion = 1 };
            var factory = Substitute.For<IProjectionFactory<SchemaVersionTestProjection>>();
            factory.ExistsAsync(null, Arg.Any<CancellationToken>())
                .Returns(true);
            factory.GetOrCreateAsync(
                Arg.Any<IObjectDocumentFactory>(),
                Arg.Any<IEventStreamFactory>(),
                null,
                Arg.Any<CancellationToken>())
                .Returns(projection);

            var services = new ServiceCollection();
            services.AddSingleton(factory);
            services.AddSingleton(_documentFactory);
            services.AddSingleton(_eventStreamFactory);
            var sp = services.BuildServiceProvider();

            var options = new ProjectionOptions { SchemaMismatchBehavior = SchemaMismatchBehavior.Warn };
            var sut = new ProjectionLoader(sp, _statusCoordinator, Options.Create(options));

            // Act
            var result = await sut.GetWithVersionCheckAsync<SchemaVersionTestProjection>("obj-1");

            // Assert — should return result without throwing or triggering rebuild
            Assert.NotNull(result.Projection);
            Assert.True(result.SchemaMismatch);
            await _statusCoordinator.DidNotReceive().StartRebuildAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<RebuildStrategy>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>());
        }
    }
}

/// <summary>
/// Test projection with configurable CodeSchemaVersion for schema mismatch testing.
/// </summary>
public class SchemaVersionTestProjection : Projection
{
    private Checkpoint _checkpoint = new();
    private readonly int _codeSchemaVersion;

    public SchemaVersionTestProjection() : this(1) { }

    public SchemaVersionTestProjection(int codeSchemaVersion)
    {
        _codeSchemaVersion = codeSchemaVersion;
    }

    public override int CodeSchemaVersion => _codeSchemaVersion;

    public override Checkpoint Checkpoint
    {
        get => _checkpoint;
        set => _checkpoint = value;
    }

    public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? context = null)
        where T : class
    {
        return Task.CompletedTask;
    }

    public override string ToJson() => "{}";

    protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

    protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();
}
