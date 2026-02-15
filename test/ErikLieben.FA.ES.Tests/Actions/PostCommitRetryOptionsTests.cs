using ErikLieben.FA.ES.Actions;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Actions;

public class PostCommitRetryOptionsTests
{
    [Fact]
    public void Default_Should_have_expected_values()
    {
        // Act
        var sut = new PostCommitRetryOptions();

        // Assert
        Assert.Equal(3, sut.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(200), sut.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), sut.MaxDelay);
        Assert.Equal(2.0, sut.BackoffMultiplier);
        Assert.True(sut.UseJitter);
    }

    [Fact]
    public void Default_static_Should_return_new_instance_with_default_values()
    {
        // Act
        var sut = PostCommitRetryOptions.Default;

        // Assert
        Assert.Equal(3, sut.MaxRetries);
        Assert.True(sut.UseJitter);
    }

    [Fact]
    public void NoRetry_Should_have_zero_retries()
    {
        // Act
        var sut = PostCommitRetryOptions.NoRetry;

        // Assert
        Assert.Equal(0, sut.MaxRetries);
    }

    [Fact]
    public void Should_allow_setting_custom_values()
    {
        // Arrange
        var sut = new PostCommitRetryOptions();

        // Act
        sut.MaxRetries = 5;
        sut.InitialDelay = TimeSpan.FromMilliseconds(500);
        sut.MaxDelay = TimeSpan.FromSeconds(30);
        sut.BackoffMultiplier = 3.0;
        sut.UseJitter = false;

        // Assert
        Assert.Equal(5, sut.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), sut.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.MaxDelay);
        Assert.Equal(3.0, sut.BackoffMultiplier);
        Assert.False(sut.UseJitter);
    }
}
