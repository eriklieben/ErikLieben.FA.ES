using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Snapshots;

namespace ErikLieben.FA.ES.Tests.Snapshots;

public class SnapshotPolicyTests
{
    [Fact]
    public void ShouldSnapshot_WhenDisabled_ReturnsFalse()
    {
        var policy = new SnapshotPolicy { Enabled = false, Every = 10 };

        var result = policy.ShouldSnapshot(100, 50, null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenBelowMinEvents_ReturnsFalse()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            Every = 10,
            MinEventsBeforeSnapshot = 100
        };

        var result = policy.ShouldSnapshot(50, 50, null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenEveryThresholdMet_ReturnsTrue()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            Every = 10,
            MinEventsBeforeSnapshot = 0
        };

        var result = policy.ShouldSnapshot(100, 10, null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenEveryThresholdNotMet_ReturnsFalse()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            Every = 10,
            MinEventsBeforeSnapshot = 0
        };

        var result = policy.ShouldSnapshot(100, 5, null);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenEventTypeMatches_ReturnsTrue()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            MinEventsBeforeSnapshot = 0
        };
        policy.OnEvents.Add(typeof(TestEvent));

        var result = policy.ShouldSnapshot(100, 1, typeof(TestEvent));

        Assert.True(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenEventTypeDoesNotMatch_ReturnsFalse()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            MinEventsBeforeSnapshot = 0,
            Every = 0 // Disable count-based
        };
        policy.OnEvents.Add(typeof(TestEvent));

        var result = policy.ShouldSnapshot(100, 1, typeof(OtherEvent));

        Assert.False(result);
    }

    [Fact]
    public void ShouldSnapshot_WhenEventTypeMatchesButBelowMinEvents_ReturnsFalse()
    {
        var policy = new SnapshotPolicy
        {
            Enabled = true,
            MinEventsBeforeSnapshot = 100
        };
        policy.OnEvents.Add(typeof(TestEvent));

        var result = policy.ShouldSnapshot(50, 1, typeof(TestEvent));

        Assert.False(result);
    }

    [Fact]
    public void FromAttribute_CreatesCorrectPolicy()
    {
        var attribute = new SnapshotPolicyAttribute
        {
            Every = 50,
            OnEvents = [typeof(TestEvent)],
            KeepSnapshots = 5,
            MaxAge = "30d",
            MinEventsBeforeSnapshot = 20,
            Enabled = true
        };

        var policy = SnapshotPolicy.FromAttribute(attribute);

        Assert.Equal(50, policy.Every);
        Assert.Equal([typeof(TestEvent)], policy.OnEvents);
        Assert.Equal(5, policy.KeepSnapshots);
        Assert.Equal(TimeSpan.FromDays(30), policy.MaxAge);
        Assert.Equal(20, policy.MinEventsBeforeSnapshot);
        Assert.True(policy.Enabled);
    }

    [Theory]
    [InlineData("30d", 30)]
    [InlineData("7d", 7)]
    [InlineData("1d", 1)]
    [InlineData("365d", 365)]
    public void ParseDuration_ParsesDays(string input, int expectedDays)
    {
        var result = SnapshotPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("6m", 180)]
    [InlineData("1m", 30)]
    [InlineData("12m", 360)]
    public void ParseDuration_ParsesMonths(string input, int expectedDays)
    {
        var result = SnapshotPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("1y", 365)]
    [InlineData("2y", 730)]
    public void ParseDuration_ParsesYears(string input, int expectedDays)
    {
        var result = SnapshotPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("24h", 1)]
    [InlineData("48h", 2)]
    [InlineData("12h", 0.5)]
    public void ParseDuration_ParsesHours(string input, double expectedDays)
    {
        var result = SnapshotPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ParseDuration_ReturnsNullForEmptyOrNull(string? input)
    {
        var result = SnapshotPolicy.ParseDuration(input);

        Assert.Null(result);
    }

    [Fact]
    public void ParseDuration_ThrowsForInvalidFormat()
    {
        Assert.Throws<ArgumentException>(() => SnapshotPolicy.ParseDuration("invalid"));
    }

    private record TestEvent;
    private record OtherEvent;
}
