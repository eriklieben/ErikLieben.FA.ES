using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ErikLieben.FA.ES.Observability;

/// <summary>
/// Provides metrics recording for the ErikLieben.FA.ES library.
/// All metrics follow OpenTelemetry naming conventions with "faes." prefix.
/// </summary>
/// <remarks>
/// <para>
/// Available metrics:
/// </para>
/// <list type="bullet">
/// <item><description>faes.events.appended - Counter of events appended to streams</description></item>
/// <item><description>faes.events.read - Counter of events read from streams</description></item>
/// <item><description>faes.commits.total - Counter of commit operations</description></item>
/// <item><description>faes.projections.updates - Counter of projection update operations</description></item>
/// <item><description>faes.commit.duration - Histogram of commit operation durations (ms)</description></item>
/// <item><description>faes.projection.update.duration - Histogram of projection update durations (ms)</description></item>
/// <item><description>faes.storage.read.duration - Histogram of storage read durations (ms)</description></item>
/// <item><description>faes.storage.write.duration - Histogram of storage write durations (ms)</description></item>
/// <item><description>faes.events_per_commit - Histogram of events per commit operation</description></item>
/// <item><description>faes.projection.events_folded - Histogram of events folded per projection update</description></item>
/// </list>
/// </remarks>
public static class FaesMetrics
{
    #region Counters

    private static readonly Counter<long> EventsAppendedCounter =
        FaesInstrumentation.CoreMeter.CreateCounter<long>(
            name: "faes.events.appended",
            unit: "events",
            description: "Total number of events appended to streams");

    private static readonly Counter<long> EventsReadCounter =
        FaesInstrumentation.CoreMeter.CreateCounter<long>(
            name: "faes.events.read",
            unit: "events",
            description: "Total number of events read from streams");

    private static readonly Counter<long> CommitsTotalCounter =
        FaesInstrumentation.CoreMeter.CreateCounter<long>(
            name: "faes.commits.total",
            unit: "commits",
            description: "Total number of commit operations");

    private static readonly Counter<long> ProjectionUpdatesCounter =
        FaesInstrumentation.ProjectionsMeter.CreateCounter<long>(
            name: "faes.projections.updates",
            unit: "updates",
            description: "Total number of projection update operations");

    private static readonly Counter<long> SnapshotsCreatedCounter =
        FaesInstrumentation.CoreMeter.CreateCounter<long>(
            name: "faes.snapshots.created",
            unit: "snapshots",
            description: "Total number of snapshots created");

    private static readonly Counter<long> UpcastsPerformedCounter =
        FaesInstrumentation.CoreMeter.CreateCounter<long>(
            name: "faes.upcasts.performed",
            unit: "upcasts",
            description: "Total number of event upcasts performed");

    private static readonly Counter<long> CatchUpItemsProcessedCounter =
        FaesInstrumentation.ProjectionsMeter.CreateCounter<long>(
            name: "faes.catchup.items_processed",
            unit: "items",
            description: "Total number of catch-up work items processed");

    #endregion

    #region Histograms

    private static readonly Histogram<double> CommitDurationHistogram =
        FaesInstrumentation.CoreMeter.CreateHistogram<double>(
            name: "faes.commit.duration",
            unit: "ms",
            description: "Duration of commit operations in milliseconds");

    private static readonly Histogram<double> ProjectionUpdateDurationHistogram =
        FaesInstrumentation.ProjectionsMeter.CreateHistogram<double>(
            name: "faes.projection.update.duration",
            unit: "ms",
            description: "Duration of projection update operations in milliseconds");

    private static readonly Histogram<double> StorageReadDurationHistogram =
        FaesInstrumentation.StorageMeter.CreateHistogram<double>(
            name: "faes.storage.read.duration",
            unit: "ms",
            description: "Duration of storage read operations in milliseconds");

    private static readonly Histogram<double> StorageWriteDurationHistogram =
        FaesInstrumentation.StorageMeter.CreateHistogram<double>(
            name: "faes.storage.write.duration",
            unit: "ms",
            description: "Duration of storage write operations in milliseconds");

    private static readonly Histogram<long> EventsPerCommitHistogram =
        FaesInstrumentation.CoreMeter.CreateHistogram<long>(
            name: "faes.events_per_commit",
            unit: "events",
            description: "Number of events per commit operation");

    private static readonly Histogram<long> ProjectionEventsFoldedHistogram =
        FaesInstrumentation.ProjectionsMeter.CreateHistogram<long>(
            name: "faes.projection.events_folded",
            unit: "events",
            description: "Number of events folded per projection update");

    #endregion

    #region Counter Recording Methods

    /// <summary>
    /// Records that events were appended to a stream.
    /// </summary>
    /// <param name="count">The number of events appended.</param>
    /// <param name="objectName">The name of the object type (e.g., "order").</param>
    /// <param name="storageProvider">The storage provider used (e.g., "blob", "table", "cosmosdb").</param>
    public static void RecordEventsAppended(long count, string objectName, string storageProvider)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName },
            { FaesSemanticConventions.StorageProvider, storageProvider }
        };
        EventsAppendedCounter.Add(count, tags);
    }

    /// <summary>
    /// Records that events were read from a stream.
    /// </summary>
    /// <param name="count">The number of events read.</param>
    /// <param name="objectName">The name of the object type.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    public static void RecordEventsRead(long count, string objectName, string storageProvider)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName },
            { FaesSemanticConventions.StorageProvider, storageProvider }
        };
        EventsReadCounter.Add(count, tags);
    }

    /// <summary>
    /// Records a commit operation.
    /// </summary>
    /// <param name="objectName">The name of the object type.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    /// <param name="success">Whether the commit was successful.</param>
    public static void RecordCommit(string objectName, string storageProvider, bool success = true)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName },
            { FaesSemanticConventions.StorageProvider, storageProvider },
            { FaesSemanticConventions.Success, success }
        };
        CommitsTotalCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a projection update operation.
    /// </summary>
    /// <param name="projectionType">The type name of the projection.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    public static void RecordProjectionUpdate(string projectionType, string storageProvider)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ProjectionType, projectionType },
            { FaesSemanticConventions.StorageProvider, storageProvider }
        };
        ProjectionUpdatesCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a snapshot creation.
    /// </summary>
    /// <param name="objectName">The name of the object type.</param>
    public static void RecordSnapshotCreated(string objectName)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName }
        };
        SnapshotsCreatedCounter.Add(1, tags);
    }

    /// <summary>
    /// Records an event upcast operation.
    /// </summary>
    /// <param name="eventType">The event type being upcasted.</param>
    /// <param name="fromVersion">The source schema version.</param>
    /// <param name="toVersion">The target schema version.</param>
    public static void RecordUpcast(string eventType, int fromVersion, int toVersion)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.EventType, eventType },
            { FaesSemanticConventions.UpcastFromVersion, fromVersion },
            { FaesSemanticConventions.UpcastToVersion, toVersion }
        };
        UpcastsPerformedCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a catch-up work item being processed.
    /// </summary>
    /// <param name="objectName">The name of the object type.</param>
    public static void RecordCatchUpItemProcessed(string objectName)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName }
        };
        CatchUpItemsProcessedCounter.Add(1, tags);
    }

    #endregion

    #region Histogram Recording Methods

    /// <summary>
    /// Records the duration of a commit operation.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="objectName">The name of the object type.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    public static void RecordCommitDuration(double durationMs, string objectName, string storageProvider)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName },
            { FaesSemanticConventions.StorageProvider, storageProvider }
        };
        CommitDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records the duration of a projection update operation.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="projectionType">The type name of the projection.</param>
    public static void RecordProjectionUpdateDuration(double durationMs, string projectionType)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ProjectionType, projectionType }
        };
        ProjectionUpdateDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records the duration of a storage read operation.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    /// <param name="objectName">The name of the object type.</param>
    public static void RecordStorageReadDuration(double durationMs, string storageProvider, string objectName)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.StorageProvider, storageProvider },
            { FaesSemanticConventions.ObjectName, objectName }
        };
        StorageReadDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records the duration of a storage write operation.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="storageProvider">The storage provider used.</param>
    /// <param name="objectName">The name of the object type.</param>
    public static void RecordStorageWriteDuration(double durationMs, string storageProvider, string objectName)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.StorageProvider, storageProvider },
            { FaesSemanticConventions.ObjectName, objectName }
        };
        StorageWriteDurationHistogram.Record(durationMs, tags);
    }

    /// <summary>
    /// Records the number of events in a commit operation.
    /// </summary>
    /// <param name="eventCount">The number of events committed.</param>
    /// <param name="objectName">The name of the object type.</param>
    public static void RecordEventsPerCommit(long eventCount, string objectName)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ObjectName, objectName }
        };
        EventsPerCommitHistogram.Record(eventCount, tags);
    }

    /// <summary>
    /// Records the number of events folded during a projection update.
    /// </summary>
    /// <param name="eventCount">The number of events folded.</param>
    /// <param name="projectionType">The type name of the projection.</param>
    public static void RecordProjectionEventsFolded(long eventCount, string projectionType)
    {
        var tags = new TagList
        {
            { FaesSemanticConventions.ProjectionType, projectionType }
        };
        ProjectionEventsFoldedHistogram.Record(eventCount, tags);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a Stopwatch and starts it. Use with <see cref="StopAndGetElapsedMs"/> for timing operations.
    /// </summary>
    /// <returns>A started Stopwatch instance.</returns>
    public static Stopwatch StartTimer() => Stopwatch.StartNew();

    /// <summary>
    /// Stops the stopwatch and returns the elapsed time in milliseconds.
    /// </summary>
    /// <param name="stopwatch">The stopwatch to stop.</param>
    /// <returns>The elapsed time in milliseconds.</returns>
    public static double StopAndGetElapsedMs(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    #endregion
}
