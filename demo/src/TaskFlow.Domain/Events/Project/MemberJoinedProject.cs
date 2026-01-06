using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// SCHEMA VERSION 1 (Legacy) - New team member was added to the project
/// This version uses a simple string Role field.
/// Kept for backwards compatibility with older events in storage.
/// </summary>
[EventName("Project.MemberJoined")]
public record MemberJoinedProjectV1(
    string MemberId,
    string Role,
    string InvitedBy,
    DateTime JoinedAt);

/// <summary>
/// SCHEMA VERSION 2 (Current) - New team member was added to the project
/// Breaking change: Role is now split into Role + Permissions for finer-grained access control.
/// </summary>
[EventName("Project.MemberJoined")]
[EventVersion(2)]
public record MemberJoinedProject(
    string MemberId,
    string Role,
    MemberPermissions Permissions,
    string InvitedBy,
    DateTime JoinedAt);

/// <summary>
/// Permissions granted to a project member
/// </summary>
public record MemberPermissions(
    bool CanEdit,
    bool CanDelete,
    bool CanInvite,
    bool CanManageWorkItems);
