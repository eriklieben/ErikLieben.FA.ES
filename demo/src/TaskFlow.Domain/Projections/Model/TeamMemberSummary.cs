namespace TaskFlow.Domain.Projections.Model;

/// <summary>
/// Summary of a team member's contributions to a project
/// This demonstrates deeply nested types for JSON serialization testing
/// </summary>
public class TeamMemberSummary
{
    public string MemberId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ContributionItem> Contributions { get; set; } = new();
    public int TotalWorkItemsCompleted { get; set; }
}

/// <summary>
/// Represents a contribution made by a team member
/// Contains nested ActivityDetail items
/// </summary>
public class ContributionItem
{
    public string WorkItemId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public List<ActivityDetail> Activities { get; set; } = new();
}

/// <summary>
/// Nested detail about an activity within a contribution
/// This is the deepest level of nesting (similar to user's TextItem)
/// </summary>
public class ActivityDetail
{
    public string ActivityType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
}
