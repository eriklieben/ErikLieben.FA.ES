namespace TaskFlow.Domain.ValueObjects.Sprint;

/// <summary>
/// Represents the status of a sprint
/// </summary>
public enum SprintStatus
{
    /// <summary>Sprint is planned but not yet started</summary>
    Planned,

    /// <summary>Sprint is currently active</summary>
    Active,

    /// <summary>Sprint has been completed</summary>
    Completed,

    /// <summary>Sprint was cancelled before completion</summary>
    Cancelled
}
