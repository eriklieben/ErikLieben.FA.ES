using System;
using ErikLieben.FA.ES.Exceptions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Exceptions;

public class FactoryAndVersionExceptionsSerializationTests
{
    public class UnableToFindDocumentFactoryExceptionBasics
    {
        [Fact]
        public void Should_set_message_prefix()
        {
            var sut = new UnableToFindDocumentFactoryException("no factory");
            Assert.StartsWith("[ELFAES-CFG-0004] ", sut.Message);
        }
    }

    public class UnableToFindDocumentTagFactoryExceptionBasics
    {
        [Fact]
        public void Should_set_message_prefix()
        {
            var sut = new UnableToFindDocumentTagFactoryException("no tag factory");
            Assert.StartsWith("[ELFAES-CFG-0005] ", sut.Message);
        }
    }

    public class VersionTokenStreamMismatchExceptionBasics
    {
        [Fact]
        public void Should_preserve_properties()
        {
            var sut = new VersionTokenStreamMismatchException("A","B");
            Assert.Equal("A", sut.LeftObjectIdentifier);
            Assert.Equal("B", sut.RightObjectIdentifier);
            Assert.Contains("A", sut.Message);
            Assert.Contains("B", sut.Message);
        }

        [Fact]
        public void Should_support_inner_exception_constructor()
        {
            var inner = new InvalidOperationException("inner");
            var sut = new VersionTokenStreamMismatchException("L","R", inner);
            Assert.Same(inner, sut.InnerException);
        }
    }

    public class UnableToCreateEventStreamForStreamTypeExceptionCtor
    {
        [Fact]
        public void Should_support_inner_exception_constructor_and_preserve_properties()
        {
            var inner = new InvalidOperationException("inner");
            var sut = new UnableToCreateEventStreamForStreamTypeException("X","Y", inner);
            Assert.Same(inner, sut.InnerException);
            Assert.Equal("X", sut.StreamType);
            Assert.Equal("Y", sut.FallbackStreamType);
        }
    }
}
