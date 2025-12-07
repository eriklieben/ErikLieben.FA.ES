namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Types of activities that can be logged during analysis and generation
/// </summary>
public enum ActivityType
{
    Info,
    Warning,
    Error,
    Progress,
    FileGenerated,
    FileSkipped,
    AnalysisStarted,
    AnalysisCompleted,
    GenerationStarted,
    GenerationCompleted
}

/// <summary>
/// Represents a single activity log entry
/// </summary>
public record ActivityLogEntry(
    DateTime Timestamp,
    ActivityType Type,
    string Message,
    string? EntityType = null,
    string? EntityName = null,
    Exception? Exception = null);

/// <summary>
/// Abstraction for logging activities during analysis and code generation.
/// Allows different implementations for CLI, watch mode, silent mode, and testing.
/// </summary>
public interface IActivityLogger
{
    /// <summary>
    /// Log an activity
    /// </summary>
    void Log(ActivityType type, string message, string? entityType = null, string? entityName = null);

    /// <summary>
    /// Log an error with optional exception
    /// </summary>
    void LogError(string message, Exception? exception = null);

    /// <summary>
    /// Log progress update
    /// </summary>
    void LogProgress(int current, int total, string message);

    /// <summary>
    /// Event raised when an activity is logged. Consumers can subscribe to receive real-time updates.
    /// </summary>
    event Action<ActivityLogEntry>? OnActivity;

    /// <summary>
    /// Gets all logged entries (for testing and verification)
    /// </summary>
    IReadOnlyList<ActivityLogEntry> GetActivityLog();
}
