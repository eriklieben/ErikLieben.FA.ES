using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.EventStream;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Tests.EventStream
{
    public class StreamDependenciesTests
    {
        [Fact]
        public void Should_set_and_get_data_store()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            var snapshotStore = Substitute.For<ISnapShotStore>();
            var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
            var aggregateFactory = Substitute.For<IAggregateFactory>();

            // Act
            var sut = new StreamDependencies
            {
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
                AggregateFactory = aggregateFactory
            };

            // Assert
            Assert.Equal(dataStore, sut.DataStore);
        }

        [Fact]
        public void Should_set_and_get_snapshot_store()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            var snapshotStore = Substitute.For<ISnapShotStore>();
            var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
            var aggregateFactory = Substitute.For<IAggregateFactory>();

            // Act
            var sut = new StreamDependencies
            {
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
                AggregateFactory = aggregateFactory
            };

            // Assert
            Assert.Equal(snapshotStore, sut.SnapshotStore);
        }

        [Fact]
        public void Should_set_and_get_object_document_factory()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            var snapshotStore = Substitute.For<ISnapShotStore>();
            var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
            var aggregateFactory = Substitute.For<IAggregateFactory>();

            // Act
            var sut = new StreamDependencies
            {
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
                AggregateFactory = aggregateFactory
            };

            // Assert
            Assert.Equal(objectDocumentFactory, sut.ObjectDocumentFactory);
        }

        [Fact]
        public void Should_set_and_get_aggregate_factory()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            var snapshotStore = Substitute.For<ISnapShotStore>();
            var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
            var aggregateFactory = Substitute.For<IAggregateFactory>();

            // Act
            var sut = new StreamDependencies
            {
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
                AggregateFactory = aggregateFactory
            };

            // Assert
            Assert.Equal(aggregateFactory, sut.AggregateFactory);
        }

        [Fact]
        public void Should_implement_istream_dependencies_interface()
        {
            // Arrange & Act
            var sut = new StreamDependencies
            {
                DataStore = Substitute.For<IDataStore>(),
                SnapshotStore = Substitute.For<ISnapShotStore>(),
                ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>(),
                AggregateFactory = Substitute.For<IAggregateFactory>()
            };

            // Assert
            Assert.IsType<IStreamDependencies>(sut, exactMatch: false);
        }

        [Fact]
        public void Should_initialize_all_properties_in_object_initializer()
        {
            // Arrange
            var dataStore = Substitute.For<IDataStore>();
            var snapshotStore = Substitute.For<ISnapShotStore>();
            var objectDocumentFactory = Substitute.For<IObjectDocumentFactory>();
            var aggregateFactory = Substitute.For<IAggregateFactory>();

            // Act
            var sut = new StreamDependencies
            {
                DataStore = dataStore,
                SnapshotStore = snapshotStore,
                ObjectDocumentFactory = objectDocumentFactory,
                AggregateFactory = aggregateFactory
            };

            // Assert
            Assert.Equal(dataStore, sut.DataStore);
            Assert.Equal(snapshotStore, sut.SnapshotStore);
            Assert.Equal(objectDocumentFactory, sut.ObjectDocumentFactory);
            Assert.Equal(aggregateFactory, sut.AggregateFactory);
        }

        // [Fact]
        // public void Should_throw_when_data_store_not_provided()
        // {
        //     // Arrange & Act & Assert
        //     var ex = Assert.Throws<System.InvalidOperationException>(() =>
        //     {
        //         // This code will throw due to required property not being initialized
        //         var sut = new StreamDependencies
        //         {
        //             // DataStore intentionally missing
        //             SnapshotStore = Substitute.For<ISnapShotStore>(),
        //             ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>(),
        //             ObjectSerializer = Substitute.For<IObjectSerializer>(),
        //             AggregateFactory = Substitute.For<IAggregateFactory>()
        //         };
        //     });
        //
        //     Assert.Contains("DataStore", ex.Message);
        // }

        // [Fact]
        // public void Should_throw_when_snapshot_store_not_provided()
        // {
        //     // Arrange & Act & Assert
        //     var ex = Assert.Throws<System.InvalidOperationException>(() =>
        //     {
        //         // This code will throw due to required property not being initialized
        //         var sut = new StreamDependencies
        //         {
        //             DataStore = Substitute.For<IDataStore>(),
        //             // SnapshotStore intentionally missing
        //             ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>(),
        //             ObjectSerializer = Substitute.For<IObjectSerializer>(),
        //             AggregateFactory = Substitute.For<IAggregateFactory>()
        //         };
        //     });
        //
        //     Assert.Contains("SnapshotStore", ex.Message);
        // }

        // [Fact]
        // public void Should_throw_when_object_document_factory_not_provided()
        // {
        //     // Arrange & Act & Assert
        //     var ex = Assert.Throws<System.InvalidOperationException>(() =>
        //     {
        //         // This code will throw due to required property not being initialized
        //         var sut = new StreamDependencies
        //         {
        //             DataStore = Substitute.For<IDataStore>(),
        //             SnapshotStore = Substitute.For<ISnapShotStore>(),
        //             // ObjectDocumentFactory intentionally missing
        //             ObjectSerializer = Substitute.For<IObjectSerializer>(),
        //             AggregateFactory = Substitute.For<IAggregateFactory>()
        //         };
        //     });
        //
        //     Assert.Contains("ObjectDocumentFactory", ex.Message);
        // }

        // [Fact]
        // public void Should_throw_when_object_serializer_not_provided()
        // {
        //     // Arrange & Act & Assert
        //     var ex = Assert.Throws<System.InvalidOperationException>(() =>
        //     {
        //         // This code will throw due to required property not being initialized
        //         var sut = new StreamDependencies
        //         {
        //             DataStore = Substitute.For<IDataStore>(),
        //             SnapshotStore = Substitute.For<ISnapShotStore>(),
        //             ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>(),
        //             // ObjectSerializer intentionally missing
        //             AggregateFactory = Substitute.For<IAggregateFactory>()
        //         };
        //     });
        //
        //     Assert.Contains("ObjectSerializer", ex.Message);
        // }

        // [Fact]
        // public void Should_throw_when_aggregate_factory_not_provided()
        // {
        //     // Arrange & Act & Assert
        //     var ex = Assert.Throws<System.InvalidOperationException>(() =>
        //     {
        //         // This code will throw due to required property not being initialized
        //         var sut = new StreamDependencies
        //         {
        //             DataStore = Substitute.For<IDataStore>(),
        //             SnapshotStore = Substitute.For<ISnapShotStore>(),
        //             ObjectDocumentFactory = Substitute.For<IObjectDocumentFactory>(),
        //             ObjectSerializer = Substitute.For<IObjectSerializer>()
        //             // AggregateFactory intentionally missing
        //         };
        //     });
        //
        //     Assert.Contains("AggregateFactory", ex.Message);
        // }
    }
}
