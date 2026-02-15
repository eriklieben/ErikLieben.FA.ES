using ErikLieben.FA.ES.S3.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests;

public class S3DataStoreTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_client_factory_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DataStore(null!, new EventStreamS3Settings("s3")));
        }

        [Fact]
        public void Should_throw_when_settings_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new S3DataStore(Substitute.For<IS3ClientFactory>(), null!));
        }

        [Fact]
        public void Should_create_instance_with_valid_parameters()
        {
            var sut = new S3DataStore(
                Substitute.For<IS3ClientFactory>(),
                new EventStreamS3Settings("s3"));

            Assert.NotNull(sut);
        }
    }
}
