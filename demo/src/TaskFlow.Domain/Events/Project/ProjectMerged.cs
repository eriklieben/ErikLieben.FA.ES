using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Project was merged into another project
/// </summary>
[EventName("Project.Merged")]
public record ProjectMerged(
    string TargetProjectId,
    string Reason,
    string MergedBy,
    DateTime MergedAt);
