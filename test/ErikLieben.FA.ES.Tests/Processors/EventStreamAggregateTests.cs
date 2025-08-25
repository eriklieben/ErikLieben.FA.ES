using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using NSubstitute;

namespace ErikLieben.FA.ES.Tests.Processors;

public class AggregateTests
{
    [Fact]
    public void Should_throw_when_stream_is_null()
    {
        // Arrange
        IEventStream stream = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestAggregate(stream));
    }

    [Fact]
    public void Should_initialize_stream_property()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();

        // Act
        var sut = new TestAggregate(stream);

        // Assert
        Assert.Equal(stream, sut.TestStream);
    }

    [Fact]
    public void Should_call_generated_setup_on_construction()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();

        // Act
        var sut = new TestAggregate(stream);

        // Assert
        Assert.True(sut.SetupCalled);
    }

    [Fact]
    public async Task Should_early_return_if_manual_folding_enabled()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.Settings.ManualFolding.Returns(true);
        var sut = new TestAggregate(stream);

        // Act
        await sut.Fold();

        // Assert
        await stream.DidNotReceive().ReadAsync();
        await stream.DidNotReceive().ReadAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Should_read_all_events_if_no_snapshots()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.Settings.ManualFolding.Returns(false);

        // Create the correct type of document substitute
        var document = Substitute.For<IObjectDocumentWithMethods>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = "123",
            StreamType = "blob",
            DocumentTagType = "blob",
            CurrentStreamVersion = 1,
            StreamConnectionName = "Store",
            DocumentTagConnectionName = "Store",
            StreamTagConnectionName = "Store",
            SnapShotConnectionName = "Store",
            SnapShots = []
        };
        document.Active.Returns(streamInfo);
        stream.Document.Returns(document);
        var events = new List<IEvent>
        {
            Substitute.For<IEvent>(),
            Substitute.For<IEvent>()
        };
        stream.ReadAsync().Returns(events);
        var sut = new TestAggregate(stream);

        // Act
        await sut.Fold();

        // Assert
        await stream.Received(1).ReadAsync();
        Assert.Equal(2, sut.FoldedEvents.Count);
    }

    [Fact]
    public async Task Should_process_snapshot_and_read_events_after_snapshot_if_snapshots_exist()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.Settings.ManualFolding.Returns(false);
        var document = Substitute.For<IObjectDocumentWithMethods>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = "123",
            StreamType = "blob",
            DocumentTagType = "blob",
            CurrentStreamVersion = 1,
            StreamConnectionName = "Store",
            DocumentTagConnectionName = "Store",
            StreamTagConnectionName = "Store",
            SnapShotConnectionName = "Store",
            SnapShots = [
                new StreamSnapShot { UntilVersion = 10 }
            ]
        };

        document.Active.Returns(streamInfo);
        stream.Document.Returns(document);

        var snapshotObject = new object();
        stream.GetSnapShot(10).Returns(snapshotObject);

        var events = new List<IEvent>
        {
            Substitute.For<IEvent>(),
            Substitute.For<IEvent>()
        };
        stream.ReadAsync(11).Returns(events);

        var sut = new TestAggregate(stream);

        // Act
        await sut.Fold();

        // Assert
        await stream.Received(1).GetSnapShot(10);
        await stream.Received(1).ReadAsync(11);
        Assert.Equal(2, sut.FoldedEvents.Count);
        Assert.Equal(snapshotObject, sut.ProcessedSnapshot);
    }

    [Fact]
    public async Task Should_handle_null_snapshot()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        stream.Settings.ManualFolding.Returns(false);
        var document = Substitute.For<IObjectDocumentWithMethods>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = "123",
            StreamType = "blob",
            DocumentTagType = "blob",
            CurrentStreamVersion = 1,
            StreamConnectionName = "Store",
            DocumentTagConnectionName = "Store",
            StreamTagConnectionName = "Store",
            SnapShotConnectionName = "Store",
            SnapShots = [
                new StreamSnapShot { UntilVersion = 10 }
            ]
        };
        document.Active.Returns(streamInfo);
        stream.Document.Returns(document);

        stream.GetSnapShot(10).Returns((object)null!);

        var events = new List<IEvent>
        {
            Substitute.For<IEvent>(),
            Substitute.For<IEvent>()
        };
        stream.ReadAsync(11).Returns(events);

        var sut = new TestAggregate(stream);

        // Act
        await sut.Fold();

        // Assert
        await stream.Received(1).GetSnapShot(10);
        await stream.Received(1).ReadAsync(11);
        Assert.Equal(2, sut.FoldedEvents.Count);
        Assert.Null(sut.ProcessedSnapshot);
    }

    [Fact]
    public void Should_fold_individual_event()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var sut = new TestAggregate(stream);
        var @event = Substitute.For<IEvent>();

        // Act
        sut.Fold(@event);

        // Assert
        Assert.Contains(@event, sut.FoldedEvents);
    }

    [Fact]
    public void Should_process_snapshot()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var sut = new TestAggregate(stream);
        var snapshot = new object();

        // Act
        sut.ProcessSnapshot(snapshot);

        // Assert
        Assert.Equal(snapshot, sut.ProcessedSnapshot);
    }

    [Fact]
    public void Should_have_empty_default_fold_event_implementation()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var sut = new MinimalAggregate(stream);
        var @event = Substitute.For<IEvent>();

        // Act
        sut.Fold(@event);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Should_not_throw_exception_with_default_process_snapshot_implementation()
    {
        // Arrange
        var stream = Substitute.For<IEventStream>();
        var sut = new MinimalAggregate(stream);
        var snapshot = new object();

        // Act & Assert
        var exception = Record.Exception(() => sut.ProcessSnapshot(snapshot));
        Assert.Null(exception);
    }

    private class MinimalAggregate : Aggregate
    {
        public MinimalAggregate(IEventStream stream) : base(stream)
        {
        }
    }


    private class TestAggregate : Aggregate
    {
        public bool SetupCalled { get; private set; }
        public List<IEvent> FoldedEvents { get; } = new();
        public object? ProcessedSnapshot { get; private set; }

        public IEventStream TestStream => Stream;

        public TestAggregate(IEventStream stream) : base(stream)
        {
        }

        protected override void GeneratedSetup()
        {
            SetupCalled = true;
        }

        public override void Fold(IEvent @event)
        {
            FoldedEvents.Add(@event);
        }

        public override void ProcessSnapshot(object snapshot)
        {
            ProcessedSnapshot = snapshot;
        }
    }
}
