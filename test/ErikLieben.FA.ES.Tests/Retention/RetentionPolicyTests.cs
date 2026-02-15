using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Retention;

namespace ErikLieben.FA.ES.Tests.Retention;

public class RetentionPolicyTests
{
    [Fact]
    public void CheckViolation_WhenDisabled_ReturnsNull()
    {
        var policy = new RetentionPolicy { Enabled = false, MaxEvents = 100 };

        var result = policy.CheckViolation(200, DateTimeOffset.UtcNow.AddYears(-2));

        Assert.Null(result);
    }

    [Fact]
    public void CheckViolation_WhenUnderLimits_ReturnsNull()
    {
        var policy = new RetentionPolicy
        {
            Enabled = true,
            MaxEvents = 1000,
            MaxAge = TimeSpan.FromDays(365)
        };

        var result = policy.CheckViolation(500, DateTimeOffset.UtcNow.AddDays(-100));

        Assert.Null(result);
    }

    [Fact]
    public void CheckViolation_WhenExceedsMaxEvents_ReturnsExceedsMaxEvents()
    {
        var policy = new RetentionPolicy
        {
            Enabled = true,
            MaxEvents = 100,
            MaxAge = TimeSpan.FromDays(365)
        };

        var result = policy.CheckViolation(150, DateTimeOffset.UtcNow.AddDays(-100));

        Assert.Equal(RetentionViolationType.ExceedsMaxEvents, result);
    }

    [Fact]
    public void CheckViolation_WhenExceedsMaxAge_ReturnsExceedsMaxAge()
    {
        var policy = new RetentionPolicy
        {
            Enabled = true,
            MaxEvents = 1000,
            MaxAge = TimeSpan.FromDays(365)
        };

        var result = policy.CheckViolation(500, DateTimeOffset.UtcNow.AddDays(-400));

        Assert.Equal(RetentionViolationType.ExceedsMaxAge, result);
    }

    [Fact]
    public void CheckViolation_WhenExceedsBoth_ReturnsBoth()
    {
        var policy = new RetentionPolicy
        {
            Enabled = true,
            MaxEvents = 100,
            MaxAge = TimeSpan.FromDays(365)
        };

        var result = policy.CheckViolation(150, DateTimeOffset.UtcNow.AddDays(-400));

        Assert.Equal(RetentionViolationType.Both, result);
    }

    [Fact]
    public void FromAttribute_CreatesCorrectPolicy()
    {
        var attribute = new RetentionPolicyAttribute
        {
            MaxAge = "365d",
            MaxEvents = 1000,
            Action = RetentionAction.Migrate,
            Enabled = true,
            KeepRecentEvents = 50,
            CreateSummaryOnMigration = false
        };

        var policy = RetentionPolicy.FromAttribute(attribute);

        Assert.Equal(TimeSpan.FromDays(365), policy.MaxAge);
        Assert.Equal(1000, policy.MaxEvents);
        Assert.Equal(RetentionAction.Migrate, policy.Action);
        Assert.True(policy.Enabled);
        Assert.Equal(50, policy.KeepRecentEvents);
        Assert.False(policy.CreateSummaryOnMigration);
    }

    [Theory]
    [InlineData("30d", 30)]
    [InlineData("7d", 7)]
    [InlineData("365d", 365)]
    public void ParseDuration_ParsesDays(string input, int expectedDays)
    {
        var result = RetentionPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("6m", 180)]
    [InlineData("12m", 360)]
    public void ParseDuration_ParsesMonths(string input, int expectedDays)
    {
        var result = RetentionPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("1y", 365)]
    [InlineData("2y", 730)]
    public void ParseDuration_ParsesYears(string input, int expectedDays)
    {
        var result = RetentionPolicy.ParseDuration(input);

        Assert.Equal(TimeSpan.FromDays(expectedDays), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ParseDuration_ReturnsNullForEmptyOrNull(string? input)
    {
        var result = RetentionPolicy.ParseDuration(input);

        Assert.Null(result);
    }

    [Fact]
    public void ParseDuration_ThrowsForInvalidFormat()
    {
        Assert.Throws<ArgumentException>(() => RetentionPolicy.ParseDuration("invalid"));
    }
}
