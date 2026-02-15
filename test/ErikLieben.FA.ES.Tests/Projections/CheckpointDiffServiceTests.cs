using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Projections;

public class CheckpointDiffServiceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectionFactory<TestProjection> _projectionFactory;
    private readonly IObjectDocumentFactory _documentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;

    public CheckpointDiffServiceTests()
    {
        _projectionFactory = Substitute.For<IProjectionFactory<TestProjection>>();
        _documentFactory = Substitute.For<IObjectDocumentFactory>();
        _eventStreamFactory = Substitute.For<IEventStreamFactory>();

        var services = new ServiceCollection();
        services.AddSingleton(_projectionFactory);
        services.AddSingleton(_documentFactory);
        services.AddSingleton(_eventStreamFactory);
        _serviceProvider = services.BuildServiceProvider();
    }

    public class ConstructorTests : CheckpointDiffServiceTests
    {
        [Fact]
        public void Should_throw_when_serviceProvider_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CheckpointDiffService(null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_serviceProvider()
        {
            var sut = new CheckpointDiffService(_serviceProvider);
            Assert.NotNull(sut);
        }
    }

    public class CompareAsyncTests : CheckpointDiffServiceTests
    {
        [Fact]
        public async Task Should_return_synced_when_fingerprints_match()
        {
            // Arrange
            var source = CreateProjection("abc123");
            var target = CreateProjection("abc123");

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.CompareAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.True(result.IsSynced);
            Assert.Equal("abc123", result.SourceFingerprint);
            Assert.Null(result.Diff);
        }

        [Fact]
        public async Task Should_return_different_when_target_missing_streams()
        {
            // Arrange
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5),
                [new ObjectIdentifier("order", "2")] = new VersionIdentifier("stream2", 3)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.CompareAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.False(result.IsSynced);
            Assert.NotNull(result.Diff);
            Assert.Single(result.Diff.MissingStreams);
            Assert.Contains("order__2", result.Diff.MissingStreams);
        }

        [Fact]
        public async Task Should_return_different_when_tokens_differ()
        {
            // Arrange
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 10)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.CompareAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.False(result.IsSynced);
            Assert.NotNull(result.Diff);
            Assert.Single(result.Diff.StreamDiffs);
            Assert.Equal("order__1", result.Diff.StreamDiffs[0].StreamId);
            Assert.Equal(5, result.Diff.StreamDiffs[0].EstimatedMissingEvents);
        }

        [Fact]
        public async Task Should_return_synced_when_both_checkpoints_empty()
        {
            // Arrange
            var source = CreateProjection(null);
            var target = CreateProjection(null);

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.CompareAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.True(result.IsSynced);
        }

        [Fact]
        public async Task Should_return_synced_when_checkpoint_entries_match_but_fingerprints_differ()
        {
            // Arrange — same checkpoint entries, different fingerprints
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.CompareAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.True(result.IsSynced);
        }
    }

    public class ConvergentCatchUpAsyncTests : CheckpointDiffServiceTests
    {
        [Fact]
        public async Task Should_return_success_when_already_synced()
        {
            // Arrange
            var source = CreateProjection("abc123");
            var target = CreateProjection("abc123");

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);

            // Act
            var result = await sut.ConvergentCatchUpAsync<TestProjection>("obj-1", 1, 2);

            // Assert
            Assert.True(result.IsSynced);
            Assert.Equal(1, result.IterationsPerformed);
            Assert.Equal(0, result.TotalEventsApplied);
        }

        [Fact]
        public async Task Should_fail_when_max_iterations_reached()
        {
            // Arrange — checkpoints that never converge
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 100)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 50)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);
            var options = new ConvergentCatchUpOptions
            {
                MaxIterations = 2,
                IterationDelay = TimeSpan.FromMilliseconds(1)
            };

            // Act
            var result = await sut.ConvergentCatchUpAsync<TestProjection>("obj-1", 1, 2, options);

            // Assert
            Assert.False(result.IsSynced);
            Assert.Equal(2, result.IterationsPerformed);
            Assert.Contains("Max iterations", result.FailureReason);
        }

        [Fact]
        public async Task Should_fail_when_too_many_events_in_iteration()
        {
            // Arrange
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 2000)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 0)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);
            var options = new ConvergentCatchUpOptions
            {
                MaxEventsPerIteration = 1000
            };

            // Act
            var result = await sut.ConvergentCatchUpAsync<TestProjection>("obj-1", 1, 2, options);

            // Assert
            Assert.False(result.IsSynced);
            Assert.Contains("Too many events", result.FailureReason);
        }

        [Fact]
        public async Task Should_respect_cancellation_token()
        {
            // Arrange
            var source = CreateProjectionWithCheckpoint("abc", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 10)
            });
            var target = CreateProjectionWithCheckpoint("def", new()
            {
                [new ObjectIdentifier("order", "1")] = new VersionIdentifier("stream1", 5)
            });

            SetupFactory(source, target, 1, 2);
            var sut = new CheckpointDiffService(_serviceProvider);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.ConvergentCatchUpAsync<TestProjection>("obj-1", 1, 2, cancellationToken: cts.Token));
        }
    }

    private static TestProjection CreateProjection(string? fingerprint)
    {
        var projection = new TestProjection();
        projection.CheckpointFingerprint = fingerprint;
        return projection;
    }

    private static TestProjection CreateProjectionWithCheckpoint(string? fingerprint, Checkpoint checkpoint)
    {
        var projection = new TestProjection();
        projection.CheckpointFingerprint = fingerprint;
        projection.Checkpoint = checkpoint;
        return projection;
    }

    private void SetupFactory(TestProjection source, TestProjection target, int sourceVersion, int targetVersion)
    {
        var sourceBlobName = $"TestProjection_v{sourceVersion}";
        var targetBlobName = $"TestProjection_v{targetVersion}";

        _projectionFactory.GetOrCreateAsync(
            Arg.Any<IObjectDocumentFactory>(),
            Arg.Any<IEventStreamFactory>(),
            sourceBlobName,
            Arg.Any<CancellationToken>())
            .Returns(source);

        _projectionFactory.GetOrCreateAsync(
            Arg.Any<IObjectDocumentFactory>(),
            Arg.Any<IEventStreamFactory>(),
            targetBlobName,
            Arg.Any<CancellationToken>())
            .Returns(target);
    }
}

/// <summary>
/// Test projection for use in unit tests.
/// </summary>
public class TestProjection : Projection
{
    private Checkpoint _checkpoint = new();

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
