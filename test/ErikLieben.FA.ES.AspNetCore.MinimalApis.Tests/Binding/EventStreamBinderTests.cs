using System.Reflection;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;
using ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Tests.Binding;

public class EventStreamBinderTests
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

    private static DefaultHttpContext CreateHttpContext(IServiceProvider serviceProvider, RouteValueDictionary? routeValues = null)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        context.Request.RouteValues = routeValues ?? new RouteValueDictionary();
        return context;
    }

    private static ParameterInfo CreateParameterInfo(EventStreamAttribute? attribute = null)
    {
        // Create a mock parameter info with the attribute
        var methodInfo = typeof(EventStreamBinderTests).GetMethod(nameof(TestMethod), BindingFlags.Static | BindingFlags.NonPublic);
        var parameter = methodInfo!.GetParameters()[0];
        return parameter;
    }

    private static void TestMethod([EventStream("id")] TestAggregate aggregate) { }

    private static void TestMethodCustomParam([EventStream("customParam")] TestAggregate aggregate) { }

    private static ParameterInfo GetParameterWithAttribute(string methodName)
    {
        var methodInfo = typeof(EventStreamBinderTests).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        return methodInfo!.GetParameters()[0];
    }

    public class BindAsync
    {
        [Fact]
        public async Task Should_throw_BindingException_when_route_parameter_is_missing()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary());
            var parameter = GetParameterWithAttribute(nameof(TestMethod));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BindingException>(
                () => EventStreamBinder.BindAsync<TestAggregate>(context, parameter).AsTask());

            Assert.Contains("id", exception.Message);
            Assert.Contains("not found or is empty", exception.Message);
        }

        [Fact]
        public async Task Should_throw_BindingException_when_route_parameter_is_empty()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethod));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BindingException>(
                () => EventStreamBinder.BindAsync<TestAggregate>(context, parameter).AsTask());

            Assert.Contains("id", exception.Message);
        }

        [Fact]
        public async Task Should_use_custom_route_parameter_name_from_attribute()
        {
            // Arrange
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethodCustomParam));

            // Act & Assert - should fail because we provided "id" but attribute expects "customParam"
            var exception = await Assert.ThrowsAsync<BindingException>(
                () => EventStreamBinder.BindAsync<TestAggregate>(context, parameter).AsTask());

            Assert.Contains("customParam", exception.Message);
        }

        [Fact]
        public async Task Should_throw_InvalidOperationException_when_aggregate_factory_not_registered()
        {
            // Arrange
            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns((IAggregateCovarianceFactory<IBase>?)null);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethod));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => EventStreamBinder.BindAsync<TestAggregate>(context, parameter).AsTask());

            Assert.Contains("No aggregate factory registered", exception.Message);
            Assert.Contains("TestAggregate", exception.Message);
        }

        [Fact]
        public async Task Should_successfully_bind_aggregate_when_exists()
        {
            // Arrange
            var testAggregate = new TestAggregate();
            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(testAggregate);

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync("TestAggregate", "test123", null).Returns(Task.FromResult(document));

            var streamFactory = Substitute.For<IEventStreamFactory>();
            streamFactory.Create(document).Returns(eventStream);

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethod));

            // Act
            var result = await EventStreamBinder.BindAsync<TestAggregate>(context, parameter);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.FoldCalled);
        }

        [Fact]
        public async Task Should_create_aggregate_when_CreateIfNotExists_is_true()
        {
            // Arrange
            var testAggregate = new TestAggregate();
            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(testAggregate);

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
            var methodInfo = typeof(BindAsync).GetMethod(nameof(TestMethodWithCreate), BindingFlags.Static | BindingFlags.NonPublic);
            var parameter = methodInfo!.GetParameters()[0];

            // Act
            var result = await EventStreamBinder.BindAsync<TestAggregate>(context, parameter);

            // Assert
            Assert.NotNull(result);
            await documentFactory.Received(1).GetOrCreateAsync("TestAggregate", "test123", null);
        }

        private static void TestMethodWithCreate([EventStream("id", CreateIfNotExists = true)] TestAggregate aggregate) { }

        [Fact]
        public async Task Should_throw_AggregateNotFoundException_when_not_found_and_CreateIfNotExists_is_false()
        {
            // Arrange
            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync("TestAggregate", "test123", null)
                .Returns(Task.FromException<IObjectDocument>(new Exception("Document not found")));

            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            var context = CreateHttpContext(serviceProvider, new RouteValueDictionary { { "id", "test123" } });
            var parameter = GetParameterWithAttribute(nameof(TestMethod));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AggregateNotFoundException>(
                () => EventStreamBinder.BindAsync<TestAggregate>(context, parameter).AsTask());

            Assert.Equal(typeof(TestAggregate), exception.AggregateType);
            Assert.Equal("test123", exception.ObjectId);
        }
    }

    public class BindCoreAsync
    {
        [Fact]
        public async Task Should_use_object_type_override_when_provided()
        {
            // Arrange
            var testAggregate = new TestAggregate();
            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(testAggregate);

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync("CustomObjectType", "test123", null).Returns(Task.FromResult(document));

            var streamFactory = Substitute.For<IEventStreamFactory>();
            streamFactory.Create(document).Returns(eventStream);

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var result = await EventStreamBinder.BindCoreAsync<TestAggregate>(
                serviceProvider,
                "test123",
                "CustomObjectType",
                createIfNotExists: false,
                store: null);

            // Assert
            Assert.NotNull(result);
            await documentFactory.Received(1).GetAsync("CustomObjectType", "test123", null);
        }

        [Fact]
        public async Task Should_use_store_when_provided()
        {
            // Arrange
            var testAggregate = new TestAggregate();
            var document = Substitute.For<IObjectDocument>();
            var eventStream = Substitute.For<IEventStream>();

            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");
            covarianceFactory.Create(eventStream).Returns(testAggregate);

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

            // Act
            var result = await EventStreamBinder.BindCoreAsync<TestAggregate>(
                serviceProvider,
                "test123",
                objectType: null,
                createIfNotExists: false,
                store: "customStore");

            // Assert
            Assert.NotNull(result);
            await documentFactory.Received(1).GetAsync("TestAggregate", "test123", "customStore");
        }

        [Fact]
        public async Task Should_detect_not_found_error_from_exception_message()
        {
            // Arrange
            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(Task.FromException<IObjectDocument>(new Exception("Resource does not exist in storage")));

            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AggregateNotFoundException>(
                () => EventStreamBinder.BindCoreAsync<TestAggregate>(
                    serviceProvider,
                    "test123",
                    objectType: null,
                    createIfNotExists: false,
                    store: null));

            Assert.Equal(typeof(TestAggregate), exception.AggregateType);
        }

        [Fact]
        public async Task Should_detect_404_error_from_exception_message()
        {
            // Arrange
            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(Task.FromException<IObjectDocument>(new Exception("HTTP 404 - Resource missing")));

            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            await Assert.ThrowsAsync<AggregateNotFoundException>(
                () => EventStreamBinder.BindCoreAsync<TestAggregate>(
                    serviceProvider,
                    "test123",
                    objectType: null,
                    createIfNotExists: false,
                    store: null));
        }

        [Fact]
        public async Task Should_rethrow_non_not_found_exceptions()
        {
            // Arrange
            var covarianceFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
            covarianceFactory.GetObjectName().Returns("TestAggregate");

            var aggregateFactory = Substitute.For<IAggregateFactory>();
            aggregateFactory.GetFactory(typeof(TestAggregate)).Returns(covarianceFactory);

            var documentFactory = Substitute.For<IObjectDocumentFactory>();
            documentFactory.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
                .Returns(Task.FromException<IObjectDocument>(new Exception("Network connection failed")));

            var streamFactory = Substitute.For<IEventStreamFactory>();

            var services = new ServiceCollection();
            services.AddSingleton(aggregateFactory);
            services.AddSingleton(documentFactory);
            services.AddSingleton(streamFactory);
            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => EventStreamBinder.BindCoreAsync<TestAggregate>(
                    serviceProvider,
                    "test123",
                    objectType: null,
                    createIfNotExists: false,
                    store: null));

            Assert.Contains("Network connection failed", exception.Message);
        }
    }
}
