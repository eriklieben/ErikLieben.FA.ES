using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using TaskFlow.Domain.Messaging;

namespace TaskFlow.Domain.Actions;

/// <summary>
/// Post-commit action that publishes projection update events.
/// Uses a static publisher that must be configured at startup.
/// </summary>
public class PublishProjectionUpdateAction : IAsyncPostCommitAction
{
    private static IProjectionEventPublisher? publisher;
    private static readonly AsyncLocal<bool> _isDisabled = new();

    /// <summary>
    /// Gets or sets whether projection publishing is disabled for the current async context.
    /// Use this during seeding operations to prevent automatic projection updates.
    /// </summary>
    public static bool IsDisabled
    {
        get => _isDisabled.Value;
        set => _isDisabled.Value = value;
    }

    /// <summary>
    /// Creates a scope where projection publishing is disabled.
    /// Use in a using statement to automatically re-enable when done.
    /// </summary>
    public static IDisposable DisableScope() => new DisabledScope();

    private class DisabledScope : IDisposable
    {
        private readonly bool _previousValue;

        public DisabledScope()
        {
            _previousValue = IsDisabled;
            IsDisabled = true;
        }

        public void Dispose()
        {
            IsDisabled = _previousValue;
        }
    }

    /// <summary>
    /// Configures the global projection event publisher.
    /// Call this once during application startup.
    /// </summary>
    public static void Configure(IProjectionEventPublisher eventPublisher)
    {
        PublishProjectionUpdateAction.publisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Filter for which object types to log. Set to null to log all, or specify types like ["sprint", "epic"]
    /// </summary>
    public static HashSet<string>? DebugLogFilter { get; set; } = new() { "sprint" };

    private static bool ShouldLog(string objectName) =>
        DebugLogFilter == null || DebugLogFilter.Contains(objectName.ToLowerInvariant());

    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        // Skip projection publishing if disabled (e.g., during data seeding)
        if (IsDisabled)
        {
            if (ShouldLog(document.ObjectName))
                Console.WriteLine($"[PublishProjectionUpdateAction] SKIPPED - IsDisabled=true for {document.ObjectName}");
            return;
        }

        if (publisher == null)
        {
            throw new InvalidOperationException(
                "PublishProjectionUpdateAction has not been configured. " +
                "Call PublishProjectionUpdateAction.Configure() during startup.");
        }

        var eventsList = events.ToList();
        if (eventsList.Count == 0)
        {
            if (ShouldLog(document.ObjectName))
                Console.WriteLine($"[PublishProjectionUpdateAction] SKIPPED - No events for {document.ObjectName}");
            return;
        }

        // Create version token for the latest event in this commit
        var latestEvent = eventsList[^1];
        var versionToken = new VersionToken(latestEvent, document);

        // Publish the projection update event
        var updateEvent = new ProjectionUpdateRequested
        {
            VersionToken = versionToken,
            ObjectName = document.ObjectName,
            StreamIdentifier = document.Active.StreamIdentifier,
            EventCount = eventsList.Count,
            TargetProjections = DetermineTargetProjections(document.ObjectName)
        };

        if (ShouldLog(document.ObjectName))
            Console.WriteLine($"[PublishProjectionUpdateAction] PUBLISHING: ObjectName={document.ObjectName}, StreamId={document.Active.StreamIdentifier}, EventCount={eventsList.Count}, Version={versionToken.Version}");

        await publisher.PublishAsync(updateEvent);
    }

    /// <summary>
    /// Determines which projections should be updated based on the object type.
    /// </summary>
    private static List<string>? DetermineTargetProjections(string objectName)
    {
        return objectName switch
        {
            "project" => ["ProjectDashboard"],
            "workitem" => ["ActiveWorkItems", "ProjectDashboard"],
            "sprint" => ["SprintDashboard"],
            "epic" => ["EpicSummary"],
            _ => null // null means all projections
        };
    }
}
