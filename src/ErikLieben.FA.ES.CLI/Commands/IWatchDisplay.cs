using ErikLieben.FA.ES.CLI.Abstractions;

namespace ErikLieben.FA.ES.CLI.Commands;

/// <summary>
/// Interface for watch mode display to allow testing.
/// </summary>
public interface IWatchDisplay : IDisposable
{
    event Action? OnFullRegenRequested;
    event Action? OnClearActivityRequested;

    Task RunAsync(Func<Task> watchLoop, CancellationToken cancellationToken);
    void Refresh();
    void SetStatus(WatchDisplay.WatchStatus status, string? operation = null);
    void SetAnalysisProgress(int current, int total, string message);
    void LogAnalysisProgress(string message);
    void SetEntityCounts(int aggregates, int projections, int inherited, int events = 0);
    void SetFileCounts(int filesWatched, int entitiesCached);
    void LogActivity(WatchDisplay.ActivityType type, string message);
    void LogFileChange(string filePath);
    void LogFileCreated(string filePath);
    void LogFileDeleted(string filePath);
    void LogRegenStarted(bool isIncremental, int entityCount = 0);
    void LogRegenCompleted(bool isIncremental, long elapsedMs);
    void LogRegenFailed(string error);
    void LogEntityRegenerated(string entityType, string entityName);
    void LogChange(DetectedChange change);
    void LogChanges(IReadOnlyList<DetectedChange> changes, bool isInitial = false);
}
