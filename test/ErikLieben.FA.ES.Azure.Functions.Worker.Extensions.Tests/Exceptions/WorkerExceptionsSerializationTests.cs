using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class WorkerExceptionsSerializationTests
{
    public class InvalidBindingSourceExceptionBasics
    {
        [Fact]
        public void Should_format_message()
        {
            var sut = new InvalidBindingSourceException("X", "Y");
            Assert.Contains("Unexpected binding source", sut.Message);
        }
    }

    public class InvalidContentTypeExceptionBasics
    {
        [Fact]
        public void Should_format_message()
        {
            var sut = new InvalidContentTypeException("A", "B");
            Assert.Contains("Unexpected content-type", sut.Message);
        }
    }
}
