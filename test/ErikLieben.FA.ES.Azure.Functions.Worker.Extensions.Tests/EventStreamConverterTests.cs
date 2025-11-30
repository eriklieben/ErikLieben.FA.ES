using System;
using System.Text.Json;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class EventStreamConverterTests
{
    private sealed class TestAggregate : IBase
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

    [Fact]
    public void Should_throw_when_bindingData_is_null()
    {
        // Arrange
        ModelBindingData? binding = null;

        // Act
        Action act = () => EventStreamConverter.GetBindingDataContent(binding!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    // Note: We do not construct ModelBindingData here because the type is abstract.
    // The coverage for GetBindingDataContent is provided by exercising the null guard above.

    [Fact]
    public async Task Should_throw_when_factory_is_missing()
    {
        // Arrange
        var aggFactory = Substitute.For<IAggregateFactory>();
        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();
        // Explicitly configure to return null; NSubstitute would otherwise auto-create a substitute
        aggFactory.GetFactory(typeof(TestAggregate)).Returns((IAggregateCovarianceFactory<IBase>?)null);
        var sut = new EventStreamConverter(aggFactory, docFactory, streamFactory);

        var data = new EventStreamData("42", "Order", string.Empty, string.Empty, string.Empty, string.Empty, true);

        // Act
        async Task<object?> Act() => await sut.ConvertModelBindingDataAsync(typeof(TestAggregate), data);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Contains("factory for the requested target type is not configured", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_create_aggregate_and_fold_using_document_factories(bool createWhenMissing)
    {
        // Arrange
        var aggFactory = Substitute.For<IAggregateFactory>();
        var covFactory = Substitute.For<IAggregateCovarianceFactory<IBase>>();
        aggFactory.GetFactory(typeof(TestAggregate)).Returns(covFactory);
        covFactory.GetObjectName().Returns("order");

        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var doc = Substitute.For<IObjectDocument>();
        doc.ObjectId.Returns("42");
        doc.ObjectName.Returns("order");
        var stream = Substitute.For<IEventStream>();

        var streamFactory = Substitute.For<IEventStreamFactory>();
        streamFactory.Create(Arg.Any<IObjectDocument>()).Returns(stream);

        var testAgg = new TestAggregate();
        covFactory.Create(Arg.Any<IEventStream>()).Returns(testAgg);

        var sut = new EventStreamConverter(aggFactory, docFactory, streamFactory);

        var data = new EventStreamData("42", "Order", connection: string.Empty, documentType: string.Empty, defaultStreamType: string.Empty, defaultStreamConnection: string.Empty, createEmtpyObjectWhenNonExisting: createWhenMissing)
        {
            CreateEmptyObjectWhenNonExistent = createWhenMissing
        };

        if (createWhenMissing)
        {
            docFactory.GetOrCreateAsync("order", "42", string.Empty).Returns(Task.FromResult(doc));
        }
        else
        {
            docFactory.GetAsync("order", "42", string.Empty).Returns(Task.FromResult(doc));
        }

        // Act
        var result = await sut.ConvertModelBindingDataAsync(typeof(TestAggregate), data);

        // Assert
        Assert.Same(testAgg, result);
        Assert.True(testAgg.FoldCalled);

        if (createWhenMissing)
        {
            await docFactory.Received(1).GetOrCreateAsync("order", "42", string.Empty);
        }
        else
        {
            await docFactory.Received(1).GetAsync("order", "42", string.Empty);
        }
        streamFactory.Received(1).Create(Arg.Any<IObjectDocument>());
        covFactory.Received(1).Create(Arg.Any<IEventStream>());
    }

    [Fact]
    public async Task Should_throw_when_objectId_is_null()
    {
        // Arrange
        var aggFactory = Substitute.For<IAggregateFactory>();
        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();
        var sut = new EventStreamConverter(aggFactory, docFactory, streamFactory);

        var data = new EventStreamData("42", "Order", string.Empty, string.Empty, string.Empty, string.Empty, true);
        data.ObjectId = null;

        // Act
        Task<object?> Act() => sut.ConvertModelBindingDataAsync(typeof(TestAggregate), data);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task Should_throw_when_data_is_null()
    {
        // Arrange
        var aggFactory = Substitute.For<IAggregateFactory>();
        var docFactory = Substitute.For<IObjectDocumentFactory>();
        var streamFactory = Substitute.For<IEventStreamFactory>();
        var sut = new EventStreamConverter(aggFactory, docFactory, streamFactory);

        // Act
        Task<object?> Act() => sut.ConvertModelBindingDataAsync(typeof(TestAggregate), null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }
}
