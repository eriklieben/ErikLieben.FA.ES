using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using NSubstitute;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class CosmosDbEventStreamTests
{
    private readonly IObjectDocumentWithMethods document;
    private readonly IStreamDependencies streamDependencies;

    public CosmosDbEventStreamTests()
    {
        document = Substitute.For<IObjectDocumentWithMethods>();
        streamDependencies = Substitute.For<IStreamDependencies>();

        var streamInformation = new StreamInformation
        {
            StreamIdentifier = "test-stream-0000000000",
            CurrentStreamVersion = -1
        };

        document.Active.Returns(streamInformation);
        document.ObjectName.Returns("TestObject");
        document.ObjectId.Returns("test-id");
    }

    public class Constructor : CosmosDbEventStreamTests
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new CosmosDbEventStream(document, streamDependencies);
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_inherit_from_base_event_stream()
        {
            var sut = new CosmosDbEventStream(document, streamDependencies);
            Assert.IsType<CosmosDbEventStream>(sut);
        }
    }
}
