using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class RoutedProjectionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_initialize_with_parameterless_constructor()
        {
            // Arrange & Act
            var sut = new TestRoutedProjection();

            // Assert
            Assert.NotNull(sut);
            Assert.NotNull(sut.Destinations);
            Assert.Empty(sut.Destinations);
            Assert.NotNull(sut.RoutingMetadata);
            Assert.NotNull(sut.Registry);
        }

        [Fact]
        public void Should_initialize_with_factories()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();

            // Act
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            // Assert
            Assert.NotNull(sut);
            Assert.Empty(sut.Destinations);
        }
    }

    public class AddDestinationTests
    {
        [Fact]
        public void Should_throw_when_called_outside_fold()
        {
            // Arrange
            var sut = new TestRoutedProjection();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1"));
            Assert.Contains("within When methods", ex.Message);
        }

        [Fact]
        public async Task Should_add_destination_during_fold()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) => sut.AddDestinationPublic<TestDestinationProjection>("dest-1");

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.Single(sut.Destinations);
            Assert.True(sut.Destinations.ContainsKey("dest-1"));
            Assert.IsType<TestDestinationProjection>(sut.Destinations["dest-1"]);
        }

        [Fact]
        public async Task Should_add_destination_with_metadata()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            var metadata = new Dictionary<string, string> { ["language"] = "en-GB" };
            sut.OnWhen = (e, vt) => sut.AddDestinationPublic<TestDestinationProjection>("dest-1", metadata);

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.Single(sut.Destinations);
            Assert.Contains("dest-1", sut.Registry.Destinations.Keys);
            Assert.Equal("en-GB", sut.Registry.Destinations["dest-1"].UserMetadata["language"]);
        }

        [Fact]
        public async Task Should_be_idempotent_when_adding_same_destination_twice()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1"); // Second call should be idempotent
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.Single(sut.Destinations);
        }
    }

    public class RouteToDestinationTests
    {
        [Fact]
        public void Should_throw_when_called_outside_fold()
        {
            // Arrange
            var sut = new TestRoutedProjection();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                sut.RouteToDestinationPublic("dest-1"));
            Assert.Contains("within When methods", ex.Message);
        }

        [Fact]
        public async Task Should_route_event_to_destination()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.RouteToDestinationPublic("dest-1");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            var destination = sut.Destinations["dest-1"] as TestDestinationProjection;
            Assert.NotNull(destination);
            Assert.Equal(1, destination!.FoldCallCount);
        }

        [Fact]
        public async Task Should_throw_when_destination_does_not_exist()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                // Route without adding destination first
                sut.RouteToDestinationPublic("nonexistent");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.Fold(@event, versionToken));
            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public async Task Should_route_custom_event_to_destination()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var originalEvent = CreateTestEvent();
            var customEvent = Substitute.For<IEvent>();
            customEvent.EventType.Returns("CustomEvent");

            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.RouteToDestinationPublic("dest-1", customEvent);
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(originalEvent, versionToken);

            // Assert
            var destination = sut.Destinations["dest-1"] as TestDestinationProjection;
            Assert.NotNull(destination);
            Assert.Same(customEvent, destination!.LastFoldEvent);
        }

        [Fact]
        public async Task Should_route_with_custom_context()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var customContext = Substitute.For<IExecutionContext>();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.RouteToDestinationPublic("dest-1", customContext);
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            var destination = sut.Destinations["dest-1"] as TestDestinationProjection;
            Assert.NotNull(destination);
            Assert.Same(customContext, destination!.LastFoldContext);
        }
    }

    public class RouteToDestinationsTests
    {
        [Fact]
        public async Task Should_route_to_multiple_destinations_with_params()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.AddDestinationPublic<TestDestinationProjection>("dest-2");
                sut.RouteToDestinationsPublic("dest-1", "dest-2");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            var dest1 = sut.Destinations["dest-1"] as TestDestinationProjection;
            var dest2 = sut.Destinations["dest-2"] as TestDestinationProjection;
            Assert.Equal(1, dest1!.FoldCallCount);
            Assert.Equal(1, dest2!.FoldCallCount);
        }

        [Fact]
        public async Task Should_route_to_multiple_destinations_with_enumerable()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            var destinations = new List<string> { "dest-1", "dest-2", "dest-3" };
            sut.OnWhen = (e, vt) =>
            {
                foreach (var d in destinations)
                    sut.AddDestinationPublic<TestDestinationProjection>(d);
                sut.RouteToDestinationsPublic(destinations);
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.Equal(3, sut.Destinations.Count);
            foreach (var d in destinations)
            {
                var dest = sut.Destinations[d] as TestDestinationProjection;
                Assert.Equal(1, dest!.FoldCallCount);
            }
        }
    }

    public class TryGetDestinationTests
    {
        [Fact]
        public async Task Should_return_true_and_destination_when_exists()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) => sut.AddDestinationPublic<TestDestinationProjection>("dest-1");

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);
            await sut.Fold(@event, versionToken);

            // Act
            var result = sut.TryGetDestination<TestDestinationProjection>("dest-1", out var destination);

            // Assert
            Assert.True(result);
            Assert.NotNull(destination);
            Assert.IsType<TestDestinationProjection>(destination);
        }

        [Fact]
        public void Should_return_false_when_destination_does_not_exist()
        {
            // Arrange
            var sut = new TestRoutedProjection();

            // Act
            var result = sut.TryGetDestination<TestDestinationProjection>("nonexistent", out var destination);

            // Assert
            Assert.False(result);
            Assert.Null(destination);
        }

        [Fact]
        public async Task Should_return_false_when_type_does_not_match()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) => sut.AddDestinationPublic<TestDestinationProjection>("dest-1");

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);
            await sut.Fold(@event, versionToken);

            // Act - try to get as wrong type
            var result = sut.TryGetDestination<OtherDestinationProjection>("dest-1", out var destination);

            // Assert
            Assert.False(result);
            Assert.Null(destination);
        }
    }

    public class GetDestinationKeysTests
    {
        [Fact]
        public async Task Should_return_all_destination_keys()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("en-GB");
                sut.AddDestinationPublic<TestDestinationProjection>("de-DE");
                sut.AddDestinationPublic<TestDestinationProjection>("fr-FR");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);
            await sut.Fold(@event, versionToken);

            // Act
            var keys = sut.GetDestinationKeys();

            // Assert
            Assert.Contains("en-GB", keys);
            Assert.Contains("de-DE", keys);
            Assert.Contains("fr-FR", keys);
        }
    }

    public class ClearDestinationsTests
    {
        [Fact]
        public async Task Should_clear_all_destinations()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.AddDestinationPublic<TestDestinationProjection>("dest-2");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);
            await sut.Fold(@event, versionToken);
            Assert.Equal(2, sut.Destinations.Count);

            // Act
            sut.ClearDestinations();

            // Assert
            Assert.Empty(sut.Destinations);
        }
    }

    public class UpdateCheckpointTests
    {
        [Fact]
        public async Task Should_update_checkpoint_after_fold()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.RouteToDestinationPublic("dest-1");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.NotEmpty(sut.Checkpoint);
            Assert.NotNull(sut.CheckpointFingerprint);
        }

        [Fact]
        public void Should_generate_fingerprint_on_update()
        {
            // Arrange
            var sut = new TestRoutedProjection();
            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            sut.UpdateCheckpoint(versionToken);

            // Assert
            Assert.NotNull(sut.CheckpointFingerprint);
            Assert.NotEmpty(sut.CheckpointFingerprint);
        }
    }

    public class RegistryTests
    {
        [Fact]
        public void Should_get_and_set_registry_through_metadata()
        {
            // Arrange
            var sut = new TestRoutedProjection();
            var registry = new DestinationRegistry();
            registry.Destinations["test"] = new DestinationMetadata { DestinationTypeName = "Test" };

            // Act
            sut.Registry = registry;

            // Assert
            Assert.Same(registry, sut.Registry);
            Assert.Same(registry, sut.RoutingMetadata.Registry);
        }

        [Fact]
        public async Task Should_update_registry_after_fold()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();
            documentFactory.GetAsync("TestObject", "obj-123").Returns(document);

            sut.OnWhen = (e, vt) =>
            {
                sut.AddDestinationPublic<TestDestinationProjection>("dest-1");
                sut.RouteToDestinationPublic("dest-1");
            };

            var versionToken = new VersionToken("TestObject", "obj-123", "stream-456", 1);

            // Act
            await sut.Fold(@event, versionToken);

            // Assert
            Assert.NotEqual(default, sut.Registry.LastUpdated);
            Assert.Contains("dest-1", sut.Registry.Destinations.Keys);
        }
    }

    public class PathTemplateTests
    {
        [Fact]
        public void Should_get_and_set_path_template()
        {
            // Arrange
            var sut = new TestRoutedProjection();

            // Act
            sut.PathTemplate = "projections/{language}.json";

            // Assert
            Assert.Equal("projections/{language}.json", sut.PathTemplate);
        }
    }

    public class FoldWithObsoleteOverloadTests
    {
        [Fact]
        public async Task Should_delegate_to_version_token_overload()
        {
            // Arrange
            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var eventStreamFactory = Substitute.For<IEventStreamFactory>();
            var sut = new TestRoutedProjection(documentFactory, eventStreamFactory);

            var @event = CreateTestEvent();
            var document = CreateTestDocument();

            sut.OnWhen = (e, vt) => { }; // No-op

            // Act
#pragma warning disable CS0618
            await sut.Fold(@event, document);
#pragma warning restore CS0618

            // Assert
            Assert.True(sut.FoldCalled);
        }
    }

    // Helper methods
    private static IEvent CreateTestEvent()
    {
        var @event = Substitute.For<IEvent>();
        @event.EventType.Returns("TestEvent");
        @event.EventVersion.Returns(1);
        return @event;
    }

    private static IObjectDocument CreateTestDocument()
    {
        var document = Substitute.For<IObjectDocument>();
        document.ObjectName.Returns("TestObject");
        document.ObjectId.Returns("obj-123");
        var streamInfo = Substitute.For<StreamInformation>();
        streamInfo.StreamIdentifier = "stream-456";
        document.Active.Returns(streamInfo);
        return document;
    }

    // Test implementations
    private class TestRoutedProjection : RoutedProjection
    {
        public Action<IEvent, VersionToken>? OnWhen { get; set; }
        public bool FoldCalled { get; private set; }

        public TestRoutedProjection() : base() { }

        public TestRoutedProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory) { }

        protected override void DispatchToWhen(IEvent @event, VersionToken versionToken)
        {
            FoldCalled = true;
            OnWhen?.Invoke(@event, versionToken);
        }

        protected override TDestination CreateDestinationInstance<TDestination>(string destinationKey)
        {
            if (typeof(TDestination) == typeof(TestDestinationProjection))
            {
                return (TDestination)(object)new TestDestinationProjection();
            }
            if (typeof(TDestination) == typeof(OtherDestinationProjection))
            {
                return (TDestination)(object)new OtherDestinationProjection();
            }
            throw new InvalidOperationException($"Unknown destination type: {typeof(TDestination)}");
        }

        public override string ToJson() => "{}";

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;

        // Public wrappers for protected methods
        public void AddDestinationPublic<T>(string key) where T : Projection
            => AddDestination<T>(key);

        public void AddDestinationPublic<T>(string key, Dictionary<string, string>? metadata) where T : Projection
            => AddDestination<T>(key, metadata);

        public void RouteToDestinationPublic(string key)
            => RouteToDestination(key);

        public void RouteToDestinationPublic(string key, IEvent customEvent)
            => RouteToDestination(key, customEvent);

        public void RouteToDestinationPublic(string key, IExecutionContext context)
            => RouteToDestination(key, context);

        public void RouteToDestinationsPublic(params string[] keys)
            => RouteToDestinations(keys);

        public void RouteToDestinationsPublic(IEnumerable<string> keys)
            => RouteToDestinations(keys);
    }

    private class TestDestinationProjection : Projection
    {
        public int FoldCallCount { get; private set; }
        public IEvent? LastFoldEvent { get; private set; }
        public IExecutionContext? LastFoldContext { get; private set; }

        private Checkpoint _checkpoint = new();
        public override Checkpoint Checkpoint
        {
            get => _checkpoint;
            set => _checkpoint = value;
        }

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null) where T : class
        {
            FoldCallCount++;
            LastFoldEvent = @event;
            LastFoldContext = parentContext;
            return Task.CompletedTask;
        }

        public override string ToJson() => "{}";

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
    }

    private class OtherDestinationProjection : Projection
    {
        private Checkpoint _checkpoint = new();
        public override Checkpoint Checkpoint
        {
            get => _checkpoint;
            set => _checkpoint = value;
        }

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null) where T : class
            => Task.CompletedTask;

        public override string ToJson() => "{}";

        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
    }
}
