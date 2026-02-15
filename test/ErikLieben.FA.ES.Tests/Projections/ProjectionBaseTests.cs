using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ProjectionBaseTests
{
    public class SetStatusMethod
    {
        [Fact]
        public void Should_change_status_and_update_timestamp()
        {
            // Arrange
            var sut = new TestProjection();
            Assert.Equal(ProjectionStatus.Active, sut.Status);
            Assert.Null(sut.StatusChangedAt);

            // Act
            sut.SetStatus(ProjectionStatus.Rebuilding);

            // Assert
            Assert.Equal(ProjectionStatus.Rebuilding, sut.Status);
            Assert.NotNull(sut.StatusChangedAt);
        }

        [Fact]
        public void Should_not_update_timestamp_when_setting_same_status()
        {
            // Arrange
            var sut = new TestProjection();
            sut.SetStatus(ProjectionStatus.Rebuilding);
            var firstTimestamp = sut.StatusChangedAt;

            // Act
            sut.SetStatus(ProjectionStatus.Rebuilding);

            // Assert
            Assert.Equal(firstTimestamp, sut.StatusChangedAt);
        }

        [Fact]
        public void Should_update_timestamp_when_transitioning_between_statuses()
        {
            // Arrange
            var sut = new TestProjection();
            sut.SetStatus(ProjectionStatus.Rebuilding);
            var firstTimestamp = sut.StatusChangedAt;

            // Act
            sut.SetStatus(ProjectionStatus.Active);

            // Assert
            Assert.Equal(ProjectionStatus.Active, sut.Status);
            Assert.NotNull(sut.StatusChangedAt);
            Assert.True(sut.StatusChangedAt >= firstTimestamp);
        }
    }

    public class StartRebuildMethod
    {
        [Fact]
        public void Should_set_status_to_rebuilding_and_create_rebuild_info()
        {
            // Arrange
            var sut = new TestProjection();

            // Act
            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);

            // Assert
            Assert.Equal(ProjectionStatus.Rebuilding, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.Equal(RebuildStrategy.BlockingWithCatchUp, sut.RebuildInfo.Strategy);
            Assert.True(sut.RebuildInfo.IsInProgress);
        }

        [Fact]
        public void Should_set_source_version_for_blue_green_strategy()
        {
            // Arrange
            var sut = new TestProjection();

            // Act
            sut.StartRebuild(RebuildStrategy.BlueGreen, sourceVersion: 3);

            // Assert
            Assert.Equal(ProjectionStatus.Rebuilding, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.Equal(RebuildStrategy.BlueGreen, sut.RebuildInfo.Strategy);
            Assert.Equal(3, sut.RebuildInfo.SourceVersion);
        }

        [Fact]
        public void Should_capture_checkpoint_fingerprint()
        {
            // Arrange
            var sut = new TestProjection();
            sut.CheckpointFingerprint = "abc123";

            // Act
            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);

            // Assert
            Assert.NotNull(sut.RebuildInfo);
            Assert.Equal("abc123", sut.RebuildInfo.SourceCheckpointFingerprint);
        }
    }

    public class StartCatchUpMethod
    {
        [Fact]
        public void Should_transition_from_rebuilding_to_catching_up()
        {
            // Arrange
            var sut = new TestProjection();
            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);

            // Act
            sut.StartCatchUp();

            // Assert
            Assert.Equal(ProjectionStatus.CatchingUp, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
        }

        [Fact]
        public void Should_throw_when_not_in_rebuilding_status()
        {
            // Arrange
            var sut = new TestProjection();
            Assert.Equal(ProjectionStatus.Active, sut.Status);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => sut.StartCatchUp());
            Assert.Contains("Cannot start catch-up from status Active", ex.Message);
        }
    }

    public class MarkReadyMethod
    {
        [Fact]
        public void Should_transition_from_rebuilding_to_ready()
        {
            // Arrange
            var sut = new TestProjection();
            sut.StartRebuild(RebuildStrategy.BlueGreen);

            // Act
            sut.MarkReady();

            // Assert
            Assert.Equal(ProjectionStatus.Ready, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.NotNull(sut.RebuildInfo.CompletedAt);
        }

        [Fact]
        public void Should_transition_from_catching_up_to_ready()
        {
            // Arrange
            var sut = new TestProjection();
            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);
            sut.StartCatchUp();

            // Act
            sut.MarkReady();

            // Assert
            Assert.Equal(ProjectionStatus.Ready, sut.Status);
        }

        [Fact]
        public void Should_throw_when_not_in_valid_status()
        {
            // Arrange
            var sut = new TestProjection();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => sut.MarkReady());
            Assert.Contains("Cannot mark ready from status Active", ex.Message);
        }
    }

    public class ActivateMethod
    {
        [Fact]
        public void Should_set_status_to_active()
        {
            // Arrange
            var sut = new TestProjection();
            sut.StartRebuild(RebuildStrategy.BlueGreen);
            sut.MarkReady();

            // Act
            sut.Activate();

            // Assert
            Assert.Equal(ProjectionStatus.Active, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.NotNull(sut.RebuildInfo.CompletedAt);
        }
    }

    public class ArchiveMethod
    {
        [Fact]
        public void Should_set_status_to_archived()
        {
            // Arrange
            var sut = new TestProjection();

            // Act
            sut.Archive();

            // Assert
            Assert.Equal(ProjectionStatus.Archived, sut.Status);
        }
    }

    public class MarkFailedMethod
    {
        [Fact]
        public void Should_set_status_to_failed_and_record_error()
        {
            // Arrange
            var sut = new TestProjection();
            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);

            // Act
            sut.MarkFailed("Something went wrong");

            // Assert
            Assert.Equal(ProjectionStatus.Failed, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.Equal("Something went wrong", sut.RebuildInfo.Error);
            Assert.True(sut.RebuildInfo.IsFailed);
        }
    }

    public class DisableMethod
    {
        [Fact]
        public void Should_set_status_to_disabled()
        {
            // Arrange
            var sut = new TestProjection();

            // Act
            sut.Disable();

            // Assert
            Assert.Equal(ProjectionStatus.Disabled, sut.Status);
        }
    }

    public class ShouldProcessUpdatesMethod
    {
        [Fact]
        public void Should_return_true_when_active()
        {
            // Arrange
            var sut = new TestProjection();

            // Assert
            Assert.True(sut.ShouldProcessUpdatesPublic());
        }

        [Theory]
        [InlineData(ProjectionStatus.Rebuilding)]
        [InlineData(ProjectionStatus.Disabled)]
        [InlineData(ProjectionStatus.CatchingUp)]
        [InlineData(ProjectionStatus.Ready)]
        [InlineData(ProjectionStatus.Archived)]
        [InlineData(ProjectionStatus.Failed)]
        public void Should_return_false_for_non_active_statuses(ProjectionStatus status)
        {
            // Arrange
            var sut = new TestProjection();
            sut.SetStatus(status);

            // Assert
            Assert.False(sut.ShouldProcessUpdatesPublic());
        }
    }

    public class NeedsSchemaUpgradeProperty
    {
        [Fact]
        public void Should_return_false_when_versions_match()
        {
            // Arrange
            var sut = new TestProjection();
            // Default SchemaVersion is 1 and default CodeSchemaVersion is 1
            Assert.Equal(1, sut.SchemaVersion);
            Assert.Equal(1, sut.CodeSchemaVersion);

            // Assert
            Assert.False(sut.NeedsSchemaUpgrade);
        }

        [Fact]
        public void Should_return_true_when_versions_differ()
        {
            // Arrange
            var sut = new TestProjection();
            sut.SchemaVersion = 2;

            // Assert
            Assert.True(sut.NeedsSchemaUpgrade);
        }
    }

    public class UpdateCheckpointMethod
    {
        [Fact]
        public void Should_add_new_entry_to_checkpoint()
        {
            // Arrange
            var sut = new TestProjection();
            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("Order", objectId, versionId, 1);

            // Act
            sut.UpdateCheckpoint(token);

            // Assert
            Assert.True(sut.Checkpoint.ContainsKey(token.ObjectIdentifier));
            Assert.NotNull(sut.CheckpointFingerprint);
        }

        [Fact]
        public void Should_update_existing_entry_in_checkpoint()
        {
            // Arrange
            var sut = new TestProjection();
            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token1 = new VersionToken("Order", objectId, versionId, 1);
            sut.UpdateCheckpoint(token1);
            var fingerprint1 = sut.CheckpointFingerprint;

            // Act
            var token2 = new VersionToken("Order", objectId, versionId, 2);
            sut.UpdateCheckpoint(token2);

            // Assert
            Assert.True(sut.Checkpoint.ContainsKey(token2.ObjectIdentifier));
            Assert.NotNull(sut.CheckpointFingerprint);
            Assert.NotEqual(fingerprint1, sut.CheckpointFingerprint);
        }

        [Fact]
        public void Should_generate_deterministic_fingerprint()
        {
            // Arrange
            var sut1 = new TestProjection();
            var sut2 = new TestProjection();
            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("Order", objectId, versionId, 1);

            // Act
            sut1.UpdateCheckpoint(token);
            sut2.UpdateCheckpoint(token);

            // Assert
            Assert.Equal(sut1.CheckpointFingerprint, sut2.CheckpointFingerprint);
        }
    }

    public class UpdateToVersionSkipsDueToStatus
    {
        [Fact]
        public async Task Should_skip_when_status_is_rebuilding()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestProjection(documentFactory, eventStreamFactory);
            sut.SetStatus(ProjectionStatus.Rebuilding);

            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act
            var result = await sut.UpdateToVersion(token);

            // Assert
            Assert.True(result.Skipped);
            Assert.Equal(ProjectionStatus.Rebuilding, result.Status);
        }

        [Fact]
        public async Task Should_skip_when_status_is_disabled()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestProjection(documentFactory, eventStreamFactory);
            sut.SetStatus(ProjectionStatus.Disabled);

            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act
            var result = await sut.UpdateToVersion(token);

            // Assert
            Assert.True(result.Skipped);
            Assert.Equal(ProjectionStatus.Disabled, result.Status);
        }

        [Fact]
        public async Task Should_throw_when_event_stream_factory_is_null()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var sut = new TestProjectionWithDocumentFactory(documentFactory);

            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateToVersion(token));
            Assert.Equal("EventStreamFactory is not initialized on this Projection instance.", exception.Message);
        }
    }

    public class UpdateToVersionGenericSkipsDueToStatus
    {
        [Fact]
        public async Task Should_skip_when_status_is_disabled()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestProjection(documentFactory, eventStreamFactory);
            sut.SetStatus(ProjectionStatus.Disabled);

            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act
            var result = await sut.UpdateToVersion<TestData>(token);

            // Assert
            Assert.True(result.Skipped);
            Assert.Equal(ProjectionStatus.Disabled, result.Status);
        }

        [Fact]
        public async Task Should_throw_when_event_stream_factory_is_null_generic()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var sut = new TestProjectionWithDocumentFactory(documentFactory);

            var objectId = Guid.NewGuid().ToString();
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.UpdateToVersion<TestData>(token));
            Assert.Equal("EventStreamFactory is not initialized on this Projection instance.", exception.Message);
        }

        [Fact]
        public async Task Should_return_success_when_token_is_not_newer_and_not_try_latest()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            var objectId = Guid.NewGuid().ToString();
            var objectIdentifier = new ObjectIdentifier("TestObject", objectId);
            var versionId = Guid.NewGuid().ToString().Replace("-", "");
            var versionIdentifier = new VersionIdentifier(versionId, 2);

            var checkpoint = new Checkpoint
            {
                { objectIdentifier, versionIdentifier }
            };

            var sut = new TestProjection(documentFactory, eventStreamFactory, checkpoint, null);
            var token = new VersionToken("TestObject", objectId, versionId, 1);

            // Act
            var result = await sut.UpdateToVersion<TestData>(token);

            // Assert
            Assert.False(result.Skipped);
            Assert.Equal(0, sut.FoldCallCount);
        }
    }

    public class RebuildLifecycleIntegration
    {
        [Fact]
        public void Should_complete_full_blocking_rebuild_lifecycle()
        {
            // Arrange
            var sut = new TestProjection();

            // Act & Assert - Full lifecycle
            Assert.Equal(ProjectionStatus.Active, sut.Status);

            sut.StartRebuild(RebuildStrategy.BlockingWithCatchUp);
            Assert.Equal(ProjectionStatus.Rebuilding, sut.Status);

            sut.StartCatchUp();
            Assert.Equal(ProjectionStatus.CatchingUp, sut.Status);

            sut.MarkReady();
            Assert.Equal(ProjectionStatus.Ready, sut.Status);

            sut.Activate();
            Assert.Equal(ProjectionStatus.Active, sut.Status);
        }

        [Fact]
        public void Should_handle_failure_during_rebuild()
        {
            // Arrange
            var sut = new TestProjection();

            // Act
            sut.StartRebuild(RebuildStrategy.BlueGreen, sourceVersion: 1);
            sut.MarkFailed("Connection timeout");

            // Assert
            Assert.Equal(ProjectionStatus.Failed, sut.Status);
            Assert.NotNull(sut.RebuildInfo);
            Assert.True(sut.RebuildInfo.IsFailed);
            Assert.Equal("Connection timeout", sut.RebuildInfo.Error);
        }
    }

    #region Test helpers

    private class TestProjection : Projection
    {
        public int FoldCallCount { get; private set; }

        private readonly Dictionary<string, IProjectionWhenParameterValueFactory> whenParameterValueFactories = new();
        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => whenParameterValueFactories;

        private Checkpoint checkpoint = new();
        public override Checkpoint Checkpoint
        {
            get => checkpoint;
            set => checkpoint = value;
        }

        public TestProjection() : base() { }

        public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory) { }

        public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory,
            Checkpoint checkpoint, string? checkpointFingerprint)
            : base(documentFactory, eventStreamFactory, checkpoint, checkpointFingerprint) { }

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null,
            IExecutionContext? context = null) where T : class
        {
            FoldCallCount++;
            return Task.CompletedTask;
        }

        public override string ToJson() => "{}";

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        public bool ShouldProcessUpdatesPublic() => ShouldProcessUpdates();
    }

    /// <summary>
    /// A test projection that has a document factory but no event stream factory,
    /// to test the null guard for EventStreamFactory.
    /// </summary>
    private class TestProjectionWithDocumentFactory : Projection
    {
        private readonly Dictionary<string, IProjectionWhenParameterValueFactory> whenParameterValueFactories = new();
        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => whenParameterValueFactories;

        private Checkpoint checkpoint = new();
        public override Checkpoint Checkpoint
        {
            get => checkpoint;
            set => checkpoint = value;
        }

        public TestProjectionWithDocumentFactory(IObjectDocumentFactory documentFactory)
            : base(documentFactory, null!)
        {
        }

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null,
            IExecutionContext? context = null) where T : class
        {
            return Task.CompletedTask;
        }

        public override string ToJson() => "{}";

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
    }

    private class TestData
    {
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
