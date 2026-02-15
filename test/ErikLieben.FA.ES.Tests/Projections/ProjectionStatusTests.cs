using ErikLieben.FA.ES.Projections;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Projections;

public class ProjectionStatusTests
{
    [Fact]
    public void Active_should_have_value_0()
    {
        Assert.Equal(0, (int)ProjectionStatus.Active);
    }

    [Fact]
    public void Rebuilding_should_have_value_1()
    {
        Assert.Equal(1, (int)ProjectionStatus.Rebuilding);
    }

    [Fact]
    public void Disabled_should_have_value_2()
    {
        Assert.Equal(2, (int)ProjectionStatus.Disabled);
    }

    [Fact]
    public void CatchingUp_should_have_value_3()
    {
        Assert.Equal(3, (int)ProjectionStatus.CatchingUp);
    }

    [Fact]
    public void Ready_should_have_value_4()
    {
        Assert.Equal(4, (int)ProjectionStatus.Ready);
    }

    [Fact]
    public void Archived_should_have_value_5()
    {
        Assert.Equal(5, (int)ProjectionStatus.Archived);
    }

    [Fact]
    public void Failed_should_have_value_6()
    {
        Assert.Equal(6, (int)ProjectionStatus.Failed);
    }

    [Fact]
    public void Default_value_should_be_Active()
    {
        ProjectionStatus status = default;
        Assert.Equal(ProjectionStatus.Active, status);
    }

    [Theory]
    [InlineData(ProjectionStatus.Active, "Active")]
    [InlineData(ProjectionStatus.Rebuilding, "Rebuilding")]
    [InlineData(ProjectionStatus.Disabled, "Disabled")]
    [InlineData(ProjectionStatus.CatchingUp, "CatchingUp")]
    [InlineData(ProjectionStatus.Ready, "Ready")]
    [InlineData(ProjectionStatus.Archived, "Archived")]
    [InlineData(ProjectionStatus.Failed, "Failed")]
    public void ToString_should_return_correct_value(ProjectionStatus status, string expected)
    {
        var result = status.ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, ProjectionStatus.Active)]
    [InlineData(1, ProjectionStatus.Rebuilding)]
    [InlineData(2, ProjectionStatus.Disabled)]
    [InlineData(3, ProjectionStatus.CatchingUp)]
    [InlineData(4, ProjectionStatus.Ready)]
    [InlineData(5, ProjectionStatus.Archived)]
    [InlineData(6, ProjectionStatus.Failed)]
    public void Should_convert_from_integer(int value, ProjectionStatus expected)
    {
        var status = (ProjectionStatus)value;
        Assert.Equal(expected, status);
    }

    [Fact]
    public void Enum_should_have_exactly_seven_values()
    {
        var values = System.Enum.GetValues<ProjectionStatus>();
        Assert.Equal(7, values.Length);
    }

    // Extension method tests
    [Theory]
    [InlineData(ProjectionStatus.Active, true)]
    [InlineData(ProjectionStatus.Rebuilding, false)]
    [InlineData(ProjectionStatus.Disabled, false)]
    [InlineData(ProjectionStatus.CatchingUp, false)]
    [InlineData(ProjectionStatus.Ready, false)]
    [InlineData(ProjectionStatus.Archived, false)]
    [InlineData(ProjectionStatus.Failed, false)]
    public void ShouldProcessInlineUpdates_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.ShouldProcessInlineUpdates());
    }

    [Theory]
    [InlineData(ProjectionStatus.Rebuilding, true)]
    [InlineData(ProjectionStatus.CatchingUp, true)]
    [InlineData(ProjectionStatus.Ready, true)]
    [InlineData(ProjectionStatus.Active, false)]
    [InlineData(ProjectionStatus.Disabled, false)]
    [InlineData(ProjectionStatus.Archived, false)]
    [InlineData(ProjectionStatus.Failed, false)]
    public void IsTransitioning_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsTransitioning());
    }

    [Theory]
    [InlineData(ProjectionStatus.Active, true)]
    [InlineData(ProjectionStatus.Ready, true)]
    [InlineData(ProjectionStatus.Archived, true)]
    [InlineData(ProjectionStatus.Rebuilding, false)]
    [InlineData(ProjectionStatus.CatchingUp, false)]
    [InlineData(ProjectionStatus.Disabled, false)]
    [InlineData(ProjectionStatus.Failed, false)]
    public void IsQueryable_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsQueryable());
    }

    [Theory]
    [InlineData(ProjectionStatus.Failed, true)]
    [InlineData(ProjectionStatus.Disabled, true)]
    [InlineData(ProjectionStatus.Active, false)]
    [InlineData(ProjectionStatus.Rebuilding, false)]
    [InlineData(ProjectionStatus.CatchingUp, false)]
    [InlineData(ProjectionStatus.Ready, false)]
    [InlineData(ProjectionStatus.Archived, false)]
    public void NeedsAttention_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.NeedsAttention());
    }

    [Theory]
    [InlineData(ProjectionStatus.Rebuilding, true)]
    [InlineData(ProjectionStatus.CatchingUp, true)]
    [InlineData(ProjectionStatus.Active, false)]
    [InlineData(ProjectionStatus.Ready, false)]
    [InlineData(ProjectionStatus.Disabled, false)]
    [InlineData(ProjectionStatus.Archived, false)]
    [InlineData(ProjectionStatus.Failed, false)]
    public void IsRebuilding_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsRebuilding());
    }

    [Theory]
    [InlineData(ProjectionStatus.Active, true)]
    [InlineData(ProjectionStatus.Disabled, true)]
    [InlineData(ProjectionStatus.Archived, true)]
    [InlineData(ProjectionStatus.Failed, true)]
    [InlineData(ProjectionStatus.Rebuilding, false)]
    [InlineData(ProjectionStatus.CatchingUp, false)]
    [InlineData(ProjectionStatus.Ready, false)]
    public void IsTerminal_ReturnsCorrectValue(ProjectionStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsTerminal());
    }

    [Fact]
    public void GetDescription_ReturnsNonEmptyStringForAllStatuses()
    {
        foreach (ProjectionStatus status in Enum.GetValues<ProjectionStatus>())
        {
            var description = status.GetDescription();
            Assert.False(string.IsNullOrEmpty(description));
            Assert.NotEqual("Unknown status", description);
        }
    }
}
