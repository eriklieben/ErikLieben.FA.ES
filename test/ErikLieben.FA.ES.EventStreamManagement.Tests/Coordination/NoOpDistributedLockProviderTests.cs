using ErikLieben.FA.ES.EventStreamManagement.Coordination;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Coordination;

public class NoOpDistributedLockProviderTests
{
    public class ProviderNameProperty
    {
        [Fact]
        public void Should_return_NoOp()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();

            // Act
            var result = sut.ProviderName;

            // Assert
            Assert.Equal("NoOp", result);
        }
    }

    public class AcquireLockAsyncMethod
    {
        [Fact]
        public async Task Should_always_return_lock()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();
            const string lockKey = "test-lock";
            var timeout = TimeSpan.FromMinutes(1);

            // Act
            var result = await sut.AcquireLockAsync(lockKey, timeout);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Should_return_lock_with_correct_lock_key()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();
            const string lockKey = "my-lock-key";
            var timeout = TimeSpan.FromMinutes(1);

            // Act
            var result = await sut.AcquireLockAsync(lockKey, timeout);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(lockKey, result.LockKey);
        }

        [Fact]
        public async Task Should_return_lock_with_unique_lock_id()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();
            const string lockKey = "test-lock";
            var timeout = TimeSpan.FromMinutes(1);

            // Act
            var result1 = await sut.AcquireLockAsync(lockKey, timeout);
            var result2 = await sut.AcquireLockAsync(lockKey, timeout);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotEqual(result1.LockId, result2.LockId);
        }

        [Theory]
        [InlineData("lock-1")]
        [InlineData("migration-12345")]
        [InlineData("")]
        public async Task Should_accept_various_lock_keys(string lockKey)
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();

            // Act
            var result = await sut.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(1));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(lockKey, result.LockKey);
        }
    }

    public class IsLockedAsyncMethod
    {
        [Fact]
        public async Task Should_always_return_false()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();
            const string lockKey = "test-lock";

            // Act
            var result = await sut.IsLockedAsync(lockKey);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Should_return_false_even_after_acquiring_lock()
        {
            // Arrange
            var sut = new NoOpDistributedLockProvider();
            const string lockKey = "test-lock";
            await sut.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(1));

            // Act
            var result = await sut.IsLockedAsync(lockKey);

            // Assert
            Assert.False(result);
        }
    }
}

public class NoOpDistributedLockTests
{
    private static async Task<IDistributedLock> CreateLockAsync()
    {
        var provider = new NoOpDistributedLockProvider();
        var result = await provider.AcquireLockAsync("test-lock", TimeSpan.FromMinutes(1));
        return result!;
    }

    public class LockIdProperty
    {
        [Fact]
        public async Task Should_return_non_empty_guid_string()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act
            var result = sut.LockId;

            // Assert
            Assert.NotEmpty(result);
            Assert.True(Guid.TryParse(result, out _));
        }
    }

    public class LockKeyProperty
    {
        [Fact]
        public async Task Should_return_lock_key_from_creation()
        {
            // Arrange
            var provider = new NoOpDistributedLockProvider();
            const string expectedKey = "my-lock-key";
            var sut = await provider.AcquireLockAsync(expectedKey, TimeSpan.FromMinutes(1));

            // Act
            var result = sut!.LockKey;

            // Assert
            Assert.Equal(expectedKey, result);
        }
    }

    public class AcquiredAtProperty
    {
        [Fact]
        public async Task Should_return_approximately_current_time()
        {
            // Arrange
            var beforeCreation = DateTimeOffset.UtcNow;
            var sut = await CreateLockAsync();
            var afterCreation = DateTimeOffset.UtcNow;

            // Act
            var result = sut.AcquiredAt;

            // Assert
            Assert.True(result >= beforeCreation);
            Assert.True(result <= afterCreation);
        }
    }

    public class ExpiresAtProperty
    {
        [Fact]
        public async Task Should_return_max_value()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act
            var result = sut.ExpiresAt;

            // Assert
            Assert.Equal(DateTimeOffset.MaxValue, result);
        }
    }

    public class IsValidProperty
    {
        [Fact]
        public async Task Should_always_return_true()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act
            var result = sut.IsValid;

            // Assert
            Assert.True(result);
        }
    }

    public class RenewAsyncMethod
    {
        [Fact]
        public async Task Should_always_return_true()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act
            var result = await sut.RenewAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_succeed_with_cancellation_token()
        {
            // Arrange
            var sut = await CreateLockAsync();
            var cts = new CancellationTokenSource();

            // Act
            var result = await sut.RenewAsync(cts.Token);

            // Assert
            Assert.True(result);
        }
    }

    public class IsValidAsyncMethod
    {
        [Fact]
        public async Task Should_always_return_true()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act
            var result = await sut.IsValidAsync();

            // Assert
            Assert.True(result);
        }
    }

    public class ReleaseAsyncMethod
    {
        [Fact]
        public async Task Should_complete_without_error()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act & Assert
            var exception = await Record.ExceptionAsync(() => sut.ReleaseAsync());
            Assert.Null(exception);
        }
    }

    public class DisposeAsyncMethod
    {
        [Fact]
        public async Task Should_complete_without_error()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () => await sut.DisposeAsync());
            Assert.Null(exception);
        }

        [Fact]
        public async Task Should_be_callable_multiple_times()
        {
            // Arrange
            var sut = await CreateLockAsync();

            // Act & Assert
            await sut.DisposeAsync();
            await sut.DisposeAsync();
            await sut.DisposeAsync();
            // No exception should be thrown
        }
    }
}
