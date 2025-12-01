namespace ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Represents a migration plan generated during dry-run analysis.
/// </summary>
public interface IMigrationPlan
{
    /// <summary>
    /// Gets the unique identifier for this plan.
    /// </summary>
    Guid PlanId { get; }

    /// <summary>
    /// Gets analysis of the source stream.
    /// </summary>
    StreamAnalysis SourceAnalysis { get; }

    /// <summary>
    /// Gets the simulation results of transformations on sample data.
    /// </summary>
    TransformationSimulation TransformationSimulation { get; }

    /// <summary>
    /// Gets estimated resource requirements for the migration.
    /// </summary>
    ResourceEstimate ResourceEstimate { get; }

    /// <summary>
    /// Gets prerequisites that must be met before migration.
    /// </summary>
    IReadOnlyList<Prerequisite> Prerequisites { get; }

    /// <summary>
    /// Gets identified risks and recommended mitigations.
    /// </summary>
    IReadOnlyList<MigrationRisk> Risks { get; }

    /// <summary>
    /// Gets the recommended migration phases.
    /// </summary>
    IReadOnlyList<string> RecommendedPhases { get; }

    /// <summary>
    /// Gets a value indicating whether the migration is feasible.
    /// </summary>
    bool IsFeasible { get; }
}

/// <summary>
/// Contains analysis information about a stream.
/// </summary>
public record StreamAnalysis
{
    /// <summary>
    /// Gets or sets the total number of events in the stream.
    /// </summary>
    public long EventCount { get; set; }

    /// <summary>
    /// Gets or sets the total size of the stream in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the distribution of event types.
    /// </summary>
    public Dictionary<string, long> EventTypeDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets the earliest event timestamp.
    /// </summary>
    public DateTimeOffset? EarliestEvent { get; set; }

    /// <summary>
    /// Gets or sets the latest event timestamp.
    /// </summary>
    public DateTimeOffset? LatestEvent { get; set; }

    /// <summary>
    /// Gets or sets the current stream version.
    /// </summary>
    public int CurrentVersion { get; set; }
}

/// <summary>
/// Contains results of transformation simulation on sample data.
/// </summary>
public record TransformationSimulation
{
    /// <summary>
    /// Gets or sets the number of events sampled.
    /// </summary>
    public int SampleSize { get; set; }

    /// <summary>
    /// Gets or sets the number of events successfully transformed.
    /// </summary>
    public int SuccessfulTransformations { get; set; }

    /// <summary>
    /// Gets or sets the number of transformation failures.
    /// </summary>
    public int FailedTransformations { get; set; }

    /// <summary>
    /// Gets or sets example transformation failures.
    /// </summary>
    public List<TransformationFailure> Failures { get; set; } = new();

    /// <summary>
    /// Gets or sets the average transformation time per event.
    /// </summary>
    public TimeSpan AverageTransformTime { get; set; }
}

/// <summary>
/// Represents a transformation failure during simulation.
/// </summary>
public record TransformationFailure
{
    /// <summary>
    /// Gets or sets the event version that failed.
    /// </summary>
    public int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    public required string EventName { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Error { get; set; }
}

/// <summary>
/// Contains resource estimates for the migration.
/// </summary>
public record ResourceEstimate
{
    /// <summary>
    /// Gets or sets the estimated duration of the migration.
    /// </summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>
    /// Gets or sets the estimated storage required in bytes.
    /// </summary>
    public long EstimatedStorageBytes { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost (in configured currency).
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Gets or sets the estimated bandwidth usage in bytes.
    /// </summary>
    public long EstimatedBandwidthBytes { get; set; }
}

/// <summary>
/// Represents a prerequisite that must be met before migration.
/// </summary>
public record Prerequisite
{
    /// <summary>
    /// Gets or sets the name of the prerequisite.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this prerequisite is met.
    /// </summary>
    public bool IsMet { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a blocking prerequisite.
    /// </summary>
    public bool IsBlocking { get; set; }
}

/// <summary>
/// Represents a migration risk with mitigation recommendations.
/// </summary>
public record MigrationRisk
{
    /// <summary>
    /// Gets or sets the risk category.
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Gets or sets the risk description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the severity (Low, Medium, High, Critical).
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Gets or sets recommended mitigation actions.
    /// </summary>
    public List<string> Mitigations { get; set; } = new();
}
