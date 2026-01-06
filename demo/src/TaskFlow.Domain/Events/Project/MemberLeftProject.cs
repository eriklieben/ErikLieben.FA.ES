using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// Team member departed from the project
/// </summary>
[EventName("Project.MemberLeft")]
public record MemberLeftProject(
    string MemberId,
    string RemovedBy,
    DateTime LeftAt);
