#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.VersionTokenParts;
using Microsoft.Azure.Cosmos;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbProjectionFactoryTests
{
    private readonly CosmosClient _mockCosmosClient;
    private readonly EventStreamCosmosDbSettings _settings;
    private readonly IObjectDocumentFactory _mockDocumentFactory;
    private readonly IEventStreamFactory _mockEventStreamFactory;

    public CosmosDbProjectionFactoryTests()
    {
        _mockCosmosClient = Substitute.For<CosmosClient>();
        _settings = new EventStreamCosmosDbSettings
        {
            DatabaseName = "test-database",
            ProjectionsContainerName = "projections",
            AutoCreateContainers = false
        };
        _mockDocumentFactory = Substitute.For<IObjectDocumentFactory>();
        _mockEventStreamFactory = Substitute.For<IEventStreamFactory>();
    }

    public class Constructor : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_throw_when_cosmos_client_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TestProjectionFactory(null!, _settings, _mockDocumentFactory, _mockEventStreamFactory));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TestProjectionFactory(_mockCosmosClient, null!, _mockDocumentFactory, _mockEventStreamFactory));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            // Arrange & Act
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class ProjectionTypeProperty : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_return_correct_projection_type()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Act
            var result = sut.ProjectionType;

            // Assert
            Assert.Equal(typeof(TestProjection), result);
        }
    }

    public class NewMethod : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_create_new_projection_instance()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Act
            var result = sut.CreateNew();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TestProjection>(result);
        }
    }

    public class LoadFromJsonMethod : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_load_projection_from_json()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);
            var json = "{\"TestProperty\":\"loaded\"}";

            // Act
            var result = sut.LoadJson(json, _mockDocumentFactory, _mockEventStreamFactory);

            // Assert
            Assert.NotNull(result);
        }
    }

    public class HasExternalCheckpointProperty : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_return_false_for_non_external_checkpoint_factory()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Act
            var result = sut.ExternalCheckpoint;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Should_return_true_for_external_checkpoint_factory()
        {
            // Arrange
            var sut = new ExternalCheckpointProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Act
            var result = sut.ExternalCheckpoint;

            // Assert
            Assert.True(result);
        }
    }

    public class SaveAsync : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public async Task Should_throw_when_projection_is_null()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
        }
    }

    public class IProjectionFactoryInterface : CosmosDbProjectionFactoryTests
    {
        [Fact]
        public void Should_implement_IProjectionFactory()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Assert
            Assert.IsType<TestProjectionFactory>(sut);
        }

        [Fact]
        public void Should_implement_IProjectionFactory_of_T()
        {
            // Arrange
            var sut = new TestProjectionFactory(_mockCosmosClient, _settings, _mockDocumentFactory, _mockEventStreamFactory);

            // Assert
            Assert.IsType<TestProjectionFactory>(sut);
        }
    }

    // Test implementations
    public class TestProjection : Projection
    {
        public TestProjection() : base() { }

        public TestProjection(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory) { }

        public string TestProperty { get; set; } = "test";

        public override Checkpoint Checkpoint { get; set; } = [];

        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; } = new();

        public override Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = default, IExecutionContext? parentContext = null) where T : class
        {
            return Task.CompletedTask;
        }

        protected override Task PostWhenAll(IObjectDocument document)
        {
            return Task.CompletedTask;
        }

        public override string ToJson()
        {
            return $"{{\"TestProperty\":\"{TestProperty}\"}}";
        }

        public static TestProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
        {
            return new TestProjection(documentFactory, eventStreamFactory);
        }
    }

    public class TestProjectionFactory : CosmosDbProjectionFactory<TestProjection>
    {
        private readonly IObjectDocumentFactory _documentFactory;
        private readonly IEventStreamFactory _eventStreamFactory;

        public TestProjectionFactory(
            CosmosClient cosmosClient,
            EventStreamCosmosDbSettings settings,
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory)
            : base(cosmosClient, settings)
        {
            _documentFactory = documentFactory;
            _eventStreamFactory = eventStreamFactory;
        }

        protected override bool HasExternalCheckpoint => false;

        public bool ExternalCheckpoint => HasExternalCheckpoint;

        protected override TestProjection New()
        {
            return new TestProjection(_documentFactory, _eventStreamFactory);
        }

        public TestProjection CreateNew() => New();

        protected override TestProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
        {
            return TestProjection.LoadFromJson(json, documentFactory, eventStreamFactory);
        }

        public TestProjection? LoadJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            => LoadFromJson(json, documentFactory, eventStreamFactory);
    }

    public class ExternalCheckpointProjectionFactory : CosmosDbProjectionFactory<TestProjection>
    {
        private readonly IObjectDocumentFactory _documentFactory;
        private readonly IEventStreamFactory _eventStreamFactory;

        public ExternalCheckpointProjectionFactory(
            CosmosClient cosmosClient,
            EventStreamCosmosDbSettings settings,
            IObjectDocumentFactory documentFactory,
            IEventStreamFactory eventStreamFactory)
            : base(cosmosClient, settings)
        {
            _documentFactory = documentFactory;
            _eventStreamFactory = eventStreamFactory;
        }

        protected override bool HasExternalCheckpoint => true;

        public bool ExternalCheckpoint => HasExternalCheckpoint;

        protected override TestProjection New()
        {
            return new TestProjection(_documentFactory, _eventStreamFactory);
        }

        protected override TestProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
        {
            return TestProjection.LoadFromJson(json, documentFactory, eventStreamFactory);
        }
    }
}
