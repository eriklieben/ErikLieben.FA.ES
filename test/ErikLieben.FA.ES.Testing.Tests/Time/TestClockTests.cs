using ErikLieben.FA.ES.Testing.Time;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Time;

public class TestClockTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithCurrentTime_WhenNoInitialTimeProvided()
    {
        // Arrange & Act
        var before = DateTimeOffset.UtcNow;
        var clock = new TestClock();
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.InRange(clock.UtcNow, before, after);
        Assert.False(clock.IsFrozen);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithProvidedTime()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var clock = new TestClock(initialTime);

        // Assert
        Assert.Equal(initialTime, clock.UtcNow);
        Assert.Equal(initialTime, clock.Now);
    }

    [Fact]
    public void Constructor_ShouldUseProvidedTimeZone()
    {
        // Arrange
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        // Act
        var clock = new TestClock(localTimeZone: timeZone);

        // Assert
        Assert.Equal(timeZone, clock.LocalTimeZone);
    }

    [Fact]
    public void SetTime_ShouldUpdateCurrentTime()
    {
        // Arrange
        var clock = new TestClock();
        var newTime = new DateTimeOffset(2025, 6, 15, 14, 0, 0, TimeSpan.Zero);

        // Act
        clock.SetTime(newTime);

        // Assert
        Assert.Equal(newTime, clock.UtcNow);
        Assert.Equal(newTime, clock.Now);
    }

    [Fact]
    public void SetUtcNow_ShouldUpdateCurrentTime()
    {
        // Arrange
        var clock = new TestClock();
        var newTime = new DateTimeOffset(2025, 6, 15, 14, 0, 0, TimeSpan.Zero);

        // Act
        clock.SetUtcNow(newTime);

        // Assert
        Assert.Equal(newTime, clock.UtcNow);
    }

    [Fact]
    public void AdvanceBy_ShouldAddDurationToCurrentTime()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initialTime);
        var duration = TimeSpan.FromHours(5);

        // Act
        clock.AdvanceBy(duration);

        // Assert
        Assert.Equal(initialTime.Add(duration), clock.UtcNow);
    }

    [Fact]
    public void Advance_ShouldAddDeltaToCurrentTime()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initialTime);
        var delta = TimeSpan.FromMinutes(30);

        // Act
        clock.Advance(delta);

        // Assert
        Assert.Equal(initialTime.Add(delta), clock.UtcNow);
    }

    [Fact]
    public void AdvanceBy_ShouldSupportNegativeDuration()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initialTime);
        var negativeDuration = TimeSpan.FromHours(-2);

        // Act
        clock.AdvanceBy(negativeDuration);

        // Assert
        Assert.Equal(initialTime.Add(negativeDuration), clock.UtcNow);
    }

    [Fact]
    public void Freeze_ShouldSetIsFrozenToTrue()
    {
        // Arrange
        var clock = new TestClock();

        // Act
        clock.Freeze();

        // Assert
        Assert.True(clock.IsFrozen);
    }

    [Fact]
    public void Unfreeze_ShouldSetIsFrozenToFalse()
    {
        // Arrange
        var clock = new TestClock();
        clock.Freeze();

        // Act
        clock.Unfreeze();

        // Assert
        Assert.False(clock.IsFrozen);
    }

    [Fact]
    public void GetUtcNow_ShouldReturnCurrentUtcTime()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 3, 20, 8, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initialTime);

        // Act
        var result = clock.GetUtcNow();

        // Assert
        Assert.Equal(initialTime, result);
    }

    [Fact]
    public void UtcNow_ShouldConvertToUniversalTime()
    {
        // Arrange
        var localTime = new DateTimeOffset(2024, 3, 20, 8, 0, 0, TimeSpan.FromHours(2));
        var clock = new TestClock(localTime);

        // Act
        var utcNow = clock.UtcNow;

        // Assert
        Assert.Equal(TimeSpan.Zero, utcNow.Offset);
        Assert.Equal(localTime.ToUniversalTime(), utcNow);
    }

    [Fact]
    public void Clock_ShouldInheritFromTimeProvider()
    {
        // Arrange
        var clock = new TestClock();

        // Assert
        Assert.IsAssignableFrom<TimeProvider>(clock);
    }

    [Fact]
    public void Clock_ShouldImplementITestClock()
    {
        // Arrange
        var clock = new TestClock();

        // Assert
        Assert.IsAssignableFrom<ITestClock>(clock);
    }

    [Fact]
    public void MultipleAdvances_ShouldAccumulate()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(initialTime);

        // Act
        clock.AdvanceBy(TimeSpan.FromHours(1));
        clock.AdvanceBy(TimeSpan.FromMinutes(30));
        clock.AdvanceBy(TimeSpan.FromSeconds(45));

        // Assert
        var expected = initialTime
            .AddHours(1)
            .AddMinutes(30)
            .AddSeconds(45);
        Assert.Equal(expected, clock.UtcNow);
    }
}
