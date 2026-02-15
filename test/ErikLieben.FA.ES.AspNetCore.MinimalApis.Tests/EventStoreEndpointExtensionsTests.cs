#pragma warning disable CA2263 // Prefer generic overload - mock setups must match the non-generic GetFactory(Type) called by production code
using System.Reflection;
using System.Text.Json;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests;

public class EventStoreEndpointExtensionsTests
{
    public class TestAggregate : IBase
    {
        public bool FoldCalled { get; private set; }

        public Task Fold()
        {
            FoldCalled = true;
            return Task.CompletedTask;
        }

        public void Fold(IEvent @event) { }

        public void ProcessSnapshot(object snapshot) { }
    }

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

    private static DefaultHttpContext CreateHttpContext(IServiceProvider serviceProvider, RouteValueDictionary? routeValues = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.RouteValues = routeValues ?? new RouteValueDictionary();
        return context;
    }

    private static ServiceProvider CreateServiceProviderWithAggregate(TestAggregate aggregate)
    {
        var document = Substitute.For<IObjectDocument>();
        var eventStream = Substitute.For<IEventStream>();

        var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
        covarianceFactory.GetObjectName().Returns("TestAggregate");
        covarianceFactory.Create(eventStream).Returns(aggregate);

        var aggregateFactory = Substitute.For<IAggregateFactory>();
        aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        documentFactory.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(Task.FromResult(document));
        documentFactory.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(Task.FromResult(document));

        var streamFactory = Substitute.For<IEventStreamFactory>();
        streamFactory.Create(document).Returns(eventStream);

        var services = new ServiceCollection();
        services.AddSingleton(aggregateFactory);
        services.AddSingleton(documentFactory);
        services.AddSingleton(streamFactory);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateServiceProviderWithProjection(TestProjection projection)
    {
        var documentFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();

        var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
        genericFactory.GetOrCreateAsync(documentFactory, streamFactory, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(projection));

        var services = new ServiceCollection();
        services.AddSingleton(documentFactory);
        services.AddSingleton(streamFactory);
        services.AddSingleton(genericFactory);
        return services.BuildServiceProvider();
    }

    public class BindEventStreamAsync
    {
        [Fact]
        public async Task Should_bind_aggregate_using_default_id_parameter()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamAsync<TestAggregate>(context);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.FoldCalled);
        }

        [Fact]
        public async Task Should_bind_aggregate_using_custom_route_parameter()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "orderId", "order456" } });

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamAsync<TestAggregate>(
                context,
                routeParameterName: "orderId");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_route_parameter_missing()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary());

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => EventStoreEndpointExtensions.BindEventStreamAsync<TestAggregate>(context));

            Assert.Contains("id", exception.Message);
            Assert.Contains("not found or is empty", exception.Message);
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_route_parameter_empty()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "" } });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => EventStoreEndpointExtensions.BindEventStreamAsync<TestAggregate>(context));

            Assert.Contains("not found or is empty", exception.Message);
        }

        [Fact]
        public async Task Should_pass_createIfNotExists_parameter()
        {

            var aggregate = new TestAggregate();

            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(aggregate);

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetOrCreateAsync("TestAggregate", "test123", null).Returns(Task.FromResult(document));

            var streamFactory = Substitute.For<IEventStreamFactory>();
            streamFactory.Create(document).Returns(eventStream);

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamAsync<TestAggregate>(
                context,
                createIfNotExists: true);

            // Assert
            await documentFactory.Received(1).GetOrCreateAsync("TestAggregate", "test123", null);
        }
    }

    public class BindEventStreamByIdAsync
    {
        [Fact]
        public async Task Should_bind_aggregate_by_explicit_id()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider);

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamByIdAsync<TestAggregate>(
                context,
                "explicit-id-123");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.FoldCalled);
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_objectId_is_null()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider);

            // Act & Assert - ArgumentNullException is thrown for null, which is a subclass of ArgumentException
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => EventStoreEndpointExtensions.BindEventStreamByIdAsync<TestAggregate>(context, null!));
        }

        [Fact]
        public async Task Should_throw_ArgumentException_when_objectId_is_empty()
        {

            var aggregate = new TestAggregate();
            var serviceProvider = CreateServiceProviderWithAggregate(aggregate);
            var context = CreateHttpContext(serviceProvider);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => EventStoreEndpointExtensions.BindEventStreamByIdAsync<TestAggregate>(context, ""));
        }

        [Fact]
        public async Task Should_use_custom_object_type()
        {

            var aggregate = new TestAggregate();

            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(aggregate);

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync("CustomType", "test123", null).Returns(Task.FromResult(document));

            var streamFactory = Substitute.For<IEventStreamFactory>();
            streamFactory.Create(document).Returns(eventStream);

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider);

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamByIdAsync<TestAggregate>(
                context,
                "test123",
                objectType: "CustomType");

            // Assert
            await documentFactory.Received(1).GetAsync("CustomType", "test123", null);
        }

        [Fact]
        public async Task Should_use_custom_store()
        {

            var aggregate = new TestAggregate();

            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(aggregate);

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync("TestAggregate", "test123", "customStore").Returns(Task.FromResult(document));

            var streamFactory = Substitute.For<IEventStreamFactory>();
            streamFactory.Create(document).Returns(eventStream);

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider);

            // Act
            var result = await EventStoreEndpointExtensions.BindEventStreamByIdAsync<TestAggregate>(
                context,
                "test123",
                store: "customStore");

            // Assert
            await documentFactory.Received(1).GetAsync("TestAggregate", "test123", "customStore");
        }
    }

    public class BindProjectionAsync
    {
        [Fact]
        public async Task Should_bind_projection_with_default_blob_name()
        {

            var projection = new TestProjection();
            var serviceProvider = CreateServiceProviderWithProjection(projection);
            var context = CreateHttpContext(serviceProvider);

            // Act
            var result = await EventStoreEndpointExtensions.BindProjectionAsync<TestProjection>(context);

            // Assert
            Assert.NotNull(result);
            Assert.Same(projection, result);
        }

        [Fact]
        public async Task Should_bind_projection_with_blob_name_pattern()
        {

            var projection = new TestProjection();

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, "order123", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(projection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "order123" } });

            // Act
            var result = await EventStoreEndpointExtensions.BindProjectionAsync<TestProjection>(
                context,
                blobNamePattern: "{id}");

            // Assert
            Assert.NotNull(result);
            await genericFactory.Received(1).GetOrCreateAsync(documentFactory, streamFactory, "order123", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_respect_createIfNotExists_parameter()
        {

            var projection = new TestProjection();

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var genericFactory = Substitute.For<IProjectionFactory<TestProjection>>();
            genericFactory.ExistsAsync(null, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            genericFactory.GetOrCreateAsync(documentFactory, streamFactory, null, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(projection));

            var services = new ServiceCollection();
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            services.AddSingleton(genericFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider);

            // Act
            var result = await EventStoreEndpointExtensions.BindProjectionAsync<TestProjection>(
                context,
                createIfNotExists: false);

            // Assert
            await genericFactory.Received(1).ExistsAsync(null, Arg.Any<CancellationToken>());
        }
    }

    public class CreateAggregateBindingDelegate
    {
        [Fact]
        public void Should_return_delegate_function()
        {
            // Act
            var bindingDelegate = EventStoreEndpointExtensions.CreateAggregateBindingDelegate<TestAggregate>();

            // Assert
            Assert.NotNull(bindingDelegate);
        }

        [Fact]
        public void Should_return_delegate_that_matches_expected_signature()
        {
            // Act
            Func<HttpContext, ParameterInfo, ValueTask<TestAggregate?>> bindingDelegate =
                EventStoreEndpointExtensions.CreateAggregateBindingDelegate<TestAggregate>();

            // Assert
            Assert.NotNull(bindingDelegate);
        }
    }

    public class CreateProjectionBindingDelegate
    {
        [Fact]
        public void Should_return_delegate_function()
        {
            // Act
            var bindingDelegate = EventStoreEndpointExtensions.CreateProjectionBindingDelegate<TestProjection>();

            // Assert
            Assert.NotNull(bindingDelegate);
        }

        [Fact]
        public void Should_return_delegate_that_matches_expected_signature()
        {
            // Act
            Func<HttpContext, ParameterInfo, ValueTask<TestProjection?>> bindingDelegate =
                EventStoreEndpointExtensions.CreateProjectionBindingDelegate<TestProjection>();

            // Assert
            Assert.NotNull(bindingDelegate);
        }
    }
}
