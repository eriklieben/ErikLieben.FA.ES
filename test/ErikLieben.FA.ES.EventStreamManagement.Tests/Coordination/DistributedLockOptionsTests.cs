using ErikLieben.FA.ES.EventStreamManagement.Coordination;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Coordination;

public class DistributedLockOptionsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_default_lock_timeout_of_30_minutes()
        {
            // Arrange & Act
            var sut = new DistributedLockOptions();

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(30), sut.LockTimeoutValue);
        }

        [Fact]
        public void Should_have_default_heartbeat_interval_of_10_seconds()
        {
            // Arrange & Act
            var sut = new DistributedLockOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(10), sut.HeartbeatIntervalValue);
        }

        [Fact]
        public void Should_have_null_lock_location_by_default()
        {
            // Arrange & Act
            var sut = new DistributedLockOptions();

            // Assert
            Assert.Null(sut.LockLocation);
        }

        [Fact]
        public void Should_have_blob_lease_as_default_provider()
        {
            // Arrange & Act
            var sut = new DistributedLockOptions();

            // Assert
            Assert.Equal("blob-lease", sut.ProviderName);
        }
    }

    public class LockTimeoutMethod
    {
        [Fact]
        public void Should_set_lock_timeout_value()
        {
            // Arrange
            var sut = new DistributedLockOptions();
            var timeout = TimeSpan.FromHours(1);

            // Act
            sut.LockTimeout(timeout);

            // Assert
            Assert.Equal(timeout, sut.LockTimeoutValue);
        }

        [Fact]
        public void Should_return_self_for_fluent_chaining()
        {
            // Arrange
            var sut = new DistributedLockOptions();

            // Act
            var result = sut.LockTimeout(TimeSpan.FromMinutes(15));

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(60)]
        [InlineData(3600)]
        public void Should_accept_various_timeout_values(int seconds)
        {
            // Arrange
            var sut = new DistributedLockOptions();
            var timeout = TimeSpan.FromSeconds(seconds);

            // Act
            sut.LockTimeout(timeout);

            // Assert
            Assert.Equal(timeout, sut.LockTimeoutValue);
        }
    }

    public class HeartbeatIntervalMethod
    {
        [Fact]
        public void Should_set_heartbeat_interval_value()
        {
            // Arrange
            var sut = new DistributedLockOptions();
            var interval = TimeSpan.FromSeconds(30);

            // Act
            sut.HeartbeatInterval(interval);

            // Assert
            Assert.Equal(interval, sut.HeartbeatIntervalValue);
        }

        [Fact]
        public void Should_return_self_for_fluent_chaining()
        {
            // Arrange
            var sut = new DistributedLockOptions();

            // Act
            var result = sut.HeartbeatInterval(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class UseLeaseMethod
    {
        [Fact]
        public void Should_set_lock_location()
        {
            // Arrange
            var sut = new DistributedLockOptions();
            const string location = "https://storage.blob.core.windows.net/locks/migration";

            // Act
            sut.UseLease(location);

            // Assert
            Assert.Equal(location, sut.LockLocation);
        }

        [Fact]
        public void Should_return_self_for_fluent_chaining()
        {
            // Arrange
            var sut = new DistributedLockOptions();

            // Act
            var result = sut.UseLease("test-location");

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class UseProviderMethod
    {
        [Fact]
        public void Should_set_provider_name()
        {
            // Arrange
            var sut = new DistributedLockOptions();
            const string providerName = "redis";

            // Act
            sut.UseProvider(providerName);

            // Assert
            Assert.Equal(providerName, sut.ProviderName);
        }

        [Fact]
        public void Should_return_self_for_fluent_chaining()
        {
            // Arrange
            var sut = new DistributedLockOptions();

            // Act
            var result = sut.UseProvider("custom-provider");

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData("blob-lease")]
        [InlineData("redis")]
        [InlineData("cosmos-db")]
        [InlineData("custom")]
        public void Should_accept_various_provider_names(string providerName)
        {
            // Arrange
            var sut = new DistributedLockOptions();

            // Act
            sut.UseProvider(providerName);

            // Assert
            Assert.Equal(providerName, sut.ProviderName);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_chained_method_calls()
        {
            // Arrange & Act
            var sut = new DistributedLockOptions();
            sut.LockTimeout(TimeSpan.FromHours(1))
               .HeartbeatInterval(TimeSpan.FromSeconds(30))
               .UseLease("test-location")
               .UseProvider("redis");

            // Assert
            Assert.Equal(TimeSpan.FromHours(1), sut.LockTimeoutValue);
            Assert.Equal(TimeSpan.FromSeconds(30), sut.HeartbeatIntervalValue);
            Assert.Equal("test-location", sut.LockLocation);
            Assert.Equal("redis", sut.ProviderName);
        }
    }
}
