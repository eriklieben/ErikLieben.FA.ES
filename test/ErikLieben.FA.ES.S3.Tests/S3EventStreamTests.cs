using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3EventStreamTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var document = Substitute.For<IObjectDocumentWithMethods>();
            var dependencies = Substitute.For<IStreamDependencies>();

            var sut = new S3EventStream(document, dependencies);

            Assert.NotNull(sut);
            Assert.IsType<S3EventStream>(sut);
        }
    }
}
