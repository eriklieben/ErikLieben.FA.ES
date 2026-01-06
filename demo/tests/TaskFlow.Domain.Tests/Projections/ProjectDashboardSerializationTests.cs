using System.Text.Json;
using TaskFlow.Domain.Projections;
using TaskFlow.Domain.Projections.Model;
using Xunit;

namespace TaskFlow.Domain.Tests.Projections;

/// <summary>
/// Tests that verify JSON serialization works for projections with nested complex types.
/// This test would FAIL with the bug reported by the user where nested types weren't
/// included in [JsonSerializable] attributes.
/// </summary>
public class ProjectDashboardSerializationTests
{
    [Fact]
    public void ProjectDashboard_with_nested_complex_types_can_serialize_to_json()
    {
        // Arrange - Create a projection with deeply nested types
        var dashboard = new ProjectDashboard();

        // Add a team member with nested contribution and activity data
        dashboard.AllTeamMembers.Add(new TeamMemberSummary
        {
            MemberId = "member-001",
            Name = "Alice Developer",
            TotalWorkItemsCompleted = 5,
            Contributions = new List<ContributionItem>
            {
                new ContributionItem
                {
                    WorkItemId = "work-123",
                    Title = "Implement user authentication",
                    CompletedAt = new DateTime(2024, 1, 15),
                    Activities = new List<ActivityDetail>
                    {
                        new ActivityDetail
                        {
                            ActivityType = "Development",
                            Timestamp = new DateTime(2024, 1, 10),
                            Description = "Created login component",
                            DurationMinutes = 120
                        },
                        new ActivityDetail
                        {
                            ActivityType = "Testing",
                            Timestamp = new DateTime(2024, 1, 12),
                            Description = "Wrote unit tests",
                            DurationMinutes = 60
                        }
                    }
                }
            }
        });

        // Act - Serialize to JSON using the projection's ToJson method (uses source-generated serialization)
        // This will throw NotSupportedException if [JsonSerializable] attributes are missing for nested types
        var json = dashboard.ToJson();

        // Assert - Should succeed without throwing
        Assert.NotNull(json);
        Assert.Contains("Alice Developer", json);
        Assert.Contains("Development", json);
        Assert.Contains("Testing", json);
    }

    [Fact]
    public void ProjectDashboard_json_can_deserialize_back_to_object()
    {
        // Arrange - Create JSON with nested structure
        var dashboard = new ProjectDashboard();
        dashboard.AllTeamMembers.Add(new TeamMemberSummary
        {
            MemberId = "member-002",
            Name = "Bob Tester",
            TotalWorkItemsCompleted = 3,
            Contributions = new List<ContributionItem>
            {
                new ContributionItem
                {
                    WorkItemId = "work-456",
                    Title = "Fix critical bug",
                    CompletedAt = new DateTime(2024, 2, 20),
                    Activities = new List<ActivityDetail>
                    {
                        new ActivityDetail
                        {
                            ActivityType = "Debugging",
                            Timestamp = new DateTime(2024, 2, 18),
                            Description = "Identified root cause",
                            DurationMinutes = 90
                        }
                    }
                }
            }
        });

        var json = dashboard.ToJson();

        // Act - Deserialize back to object
        // This will also fail if serialization attributes are missing
        var deserialized = ProjectDashboard.LoadFromJson(
            json,
            null!, // documentFactory not needed for this test
            null!  // eventStreamFactory not needed for this test
        );

        // Assert - Should successfully deserialize with all nested data intact
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.AllTeamMembers);
        Assert.Equal("Bob Tester", deserialized.AllTeamMembers[0].Name);
        Assert.Single(deserialized.AllTeamMembers[0].Contributions);
        Assert.Equal("work-456", deserialized.AllTeamMembers[0].Contributions[0].WorkItemId);
        Assert.Single(deserialized.AllTeamMembers[0].Contributions[0].Activities);
        Assert.Equal("Debugging", deserialized.AllTeamMembers[0].Contributions[0].Activities[0].ActivityType);
        Assert.Equal(90, deserialized.AllTeamMembers[0].Contributions[0].Activities[0].DurationMinutes);
    }

    [Fact]
    public void ProjectDashboard_serialization_handles_empty_nested_collections()
    {
        // Arrange - Team member with no contributions
        var dashboard = new ProjectDashboard();
        dashboard.AllTeamMembers.Add(new TeamMemberSummary
        {
            MemberId = "member-003",
            Name = "Charlie Manager",
            TotalWorkItemsCompleted = 0,
            Contributions = new List<ContributionItem>() // Empty list
        });

        // Act & Assert - Should serialize/deserialize without issues
        var json = dashboard.ToJson();
        Assert.NotNull(json);

        var deserialized = ProjectDashboard.LoadFromJson(json, null!, null!);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.AllTeamMembers);
        Assert.Empty(deserialized.AllTeamMembers[0].Contributions);
    }
}
