#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.CosmosDb.Exceptions;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Exceptions;

public class CosmosDbContainerNotFoundExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_default_constructor()
        {
            var sut = new CosmosDbContainerNotFoundException();
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_message()
        {
            var message = "Container not found";
            var sut = new CosmosDbContainerNotFoundException(message);

            Assert.Equal(message, sut.Message);
        }

        [Fact]
        public void Should_create_instance_with_message_and_inner_exception()
        {
            var message = "Container not found";
            var innerException = new InvalidOperationException("Inner");
            var sut = new CosmosDbContainerNotFoundException(message, innerException);

            Assert.Equal(message, sut.Message);
            Assert.Same(innerException, sut.InnerException);
        }
    }
}

public class CosmosDbDocumentNotFoundExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_default_constructor()
        {
            var sut = new CosmosDbDocumentNotFoundException();
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_message()
        {
            var message = "Document not found";
            var sut = new CosmosDbDocumentNotFoundException(message);

            Assert.Equal(message, sut.Message);
        }

        [Fact]
        public void Should_create_instance_with_message_and_inner_exception()
        {
            var message = "Document not found";
            var innerException = new InvalidOperationException("Inner");
            var sut = new CosmosDbDocumentNotFoundException(message, innerException);

            Assert.Equal(message, sut.Message);
            Assert.Same(innerException, sut.InnerException);
        }
    }
}

public class CosmosDbProcessingExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance_with_default_constructor()
        {
            var sut = new CosmosDbProcessingException();
            Assert.NotNull(sut);
        }

        [Fact]
        public void Should_create_instance_with_message()
        {
            var message = "Processing failed";
            var sut = new CosmosDbProcessingException(message);

            Assert.Equal(message, sut.Message);
        }

        [Fact]
        public void Should_create_instance_with_message_and_inner_exception()
        {
            var message = "Processing failed";
            var innerException = new InvalidOperationException("Inner");
            var sut = new CosmosDbProcessingException(message, innerException);

            Assert.Equal(message, sut.Message);
            Assert.Same(innerException, sut.InnerException);
        }
    }
}
