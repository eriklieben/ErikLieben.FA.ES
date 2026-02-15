namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Represents a single unit of work for projection catch-up.
/// Contains the object name and object ID that needs to be processed.
/// </summary>
/// <param name="ObjectName">The name of the object type (e.g., "project", "workitem").</param>
/// <param name="ObjectId">The unique identifier of the object instance.</param>
/// <param name="ProjectionTypeName">Optional projection type name for filtering or routing.</param>
public record CatchUpWorkItem(
    string ObjectName,
    string ObjectId,
    string? ProjectionTypeName = null);
