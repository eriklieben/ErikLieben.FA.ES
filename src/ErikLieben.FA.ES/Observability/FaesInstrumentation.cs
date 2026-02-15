using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace ErikLieben.FA.ES.Observability;

/// <summary>
/// Central registry for all OpenTelemetry instrumentation in the ErikLieben.FA.ES library.
/// Provides ActivitySource instances for distributed tracing and Meter instances for metrics.
/// </summary>
/// <remarks>
/// <para>
/// To enable tracing and metrics in your application, add the following sources/meters to your OpenTelemetry configuration:
/// </para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t
///         .AddSource(FaesInstrumentation.ActivitySources.Core)
///         .AddSource(FaesInstrumentation.ActivitySources.Storage)
///         .AddSource(FaesInstrumentation.ActivitySources.Projections))
///     .WithMetrics(m => m
///         .AddMeter(FaesInstrumentation.Meters.Core)
///         .AddMeter(FaesInstrumentation.Meters.Storage)
///         .AddMeter(FaesInstrumentation.Meters.Projections));
/// </code>
/// <para>
/// Or use the extension methods:
/// </para>
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithTracing(t => t.AddFaesTracing())
///     .WithMetrics(m => m.AddFaesMetrics());
/// </code>
/// </remarks>
public static class FaesInstrumentation
{
    /// <summary>
    /// The library name used for instrumentation identification.
    /// </summary>
    public const string LibraryName = "ErikLieben.FA.ES";

    private static readonly string LibraryVersion = GetLibraryVersion();

    /// <summary>
    /// ActivitySource for core event stream operations (read, write, session, aggregate).
    /// </summary>
    public static readonly ActivitySource Core = new(ActivitySources.Core, LibraryVersion);

    /// <summary>
    /// ActivitySource for storage provider operations (Blob, Table, CosmosDB).
    /// </summary>
    public static readonly ActivitySource Storage = new(ActivitySources.Storage, LibraryVersion);

    /// <summary>
    /// ActivitySource for projection operations (update, catch-up, factory operations).
    /// </summary>
    public static readonly ActivitySource Projections = new(ActivitySources.Projections, LibraryVersion);

    /// <summary>
    /// Meter for core event stream metrics (events appended/read, commits, durations).
    /// </summary>
    public static readonly Meter CoreMeter = new(Meters.Core, LibraryVersion);

    /// <summary>
    /// Meter for storage provider metrics (read/write latency, operation counts).
    /// </summary>
    public static readonly Meter StorageMeter = new(Meters.Storage, LibraryVersion);

    /// <summary>
    /// Meter for projection metrics (update counts, durations, events folded).
    /// </summary>
    public static readonly Meter ProjectionsMeter = new(Meters.Projections, LibraryVersion);

    /// <summary>
    /// Contains the ActivitySource names for configuring OpenTelemetry tracing.
    /// </summary>
    public static class ActivitySources
    {
        /// <summary>
        /// ActivitySource name for core event stream operations.
        /// Covers: EventStream.Read, EventStream.Session, Session.Commit, Aggregate.Fold.
        /// </summary>
        public const string Core = "ErikLieben.FA.ES";

        /// <summary>
        /// ActivitySource name for storage provider operations.
        /// Covers: DataStore read/write operations, SnapshotStore operations.
        /// </summary>
        public const string Storage = "ErikLieben.FA.ES.Storage";

        /// <summary>
        /// ActivitySource name for projection operations.
        /// Covers: Projection.UpdateToVersion, ProjectionFactory operations, CatchUp operations.
        /// </summary>
        public const string Projections = "ErikLieben.FA.ES.Projections";
    }

    /// <summary>
    /// Contains the Meter names for configuring OpenTelemetry metrics.
    /// </summary>
    public static class Meters
    {
        /// <summary>
        /// Meter name for core event stream metrics.
        /// </summary>
        public const string Core = "ErikLieben.FA.ES";

        /// <summary>
        /// Meter name for storage provider metrics.
        /// </summary>
        public const string Storage = "ErikLieben.FA.ES.Storage";

        /// <summary>
        /// Meter name for projection metrics.
        /// </summary>
        public const string Projections = "ErikLieben.FA.ES.Projections";
    }

    /// <summary>
    /// Records an exception on an activity following OpenTelemetry semantic conventions.
    /// This is a library-internal helper that doesn't require the OpenTelemetry.Api package.
    /// </summary>
    /// <param name="activity">The activity to record the exception on.</param>
    /// <param name="exception">The exception to record.</param>
    public static void RecordException(Activity? activity, Exception? exception)
    {
        if (activity == null || exception == null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);
    }

    private static string GetLibraryVersion()
    {
        var assembly = typeof(FaesInstrumentation).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        // Remove commit hash suffix if present (e.g., "2.0.0+abc123" -> "2.0.0")
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}
