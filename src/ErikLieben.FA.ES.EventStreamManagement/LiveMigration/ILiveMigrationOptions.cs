namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

/// <summary>
/// Options for configuring live migration behavior.
/// Live migration allows migrating events from a source stream to a target stream
/// while the source stream remains active and continues to receive new events.
/// </summary>
public interface ILiveMigrationOptions
{
    /// <summary>
    /// Sets the maximum time to spend attempting to close the source stream.
    /// If the source stream is very active, the migration may not converge within this timeout.
    /// Default: 5 minutes.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for convergence.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    ILiveMigrationOptions WithCloseTimeout(TimeSpan timeout);

    /// <summary>
    /// Sets the minimum delay between catch-up attempts when the source stream is active.
    /// This prevents tight loops when the source stream has continuous writes.
    /// Default: 100ms.
    /// </summary>
    /// <param name="delay">The delay between catch-up iterations.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    ILiveMigrationOptions WithCatchUpDelay(TimeSpan delay);

    /// <summary>
    /// Sets a callback invoked each time a catch-up iteration completes.
    /// Use this to monitor migration progress.
    /// </summary>
    /// <param name="callback">The progress callback.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    ILiveMigrationOptions OnCatchUpProgress(Action<LiveMigrationProgress> callback);

    /// <summary>
    /// Sets the strategy to use when the migration cannot converge
    /// (i.e., the source stream is too active).
    /// Default: <see cref="ConvergenceFailureStrategy.KeepTrying"/>.
    /// </summary>
    /// <param name="strategy">The convergence failure strategy.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    ILiveMigrationOptions OnConvergenceFailure(ConvergenceFailureStrategy strategy);

    /// <summary>
    /// Sets the maximum number of catch-up iterations before giving up.
    /// Set to 0 for unlimited iterations (only timeout applies).
    /// Default: 0 (unlimited).
    /// </summary>
    /// <param name="maxIterations">The maximum number of iterations.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    ILiveMigrationOptions WithMaxIterations(int maxIterations);
}

/// <summary>
/// Progress information for a live migration catch-up iteration.
/// </summary>
public sealed record LiveMigrationProgress
{
    /// <summary>
    /// Gets the current catch-up iteration number (1-based).
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Gets the current version of the source stream.
    /// </summary>
    public required int SourceVersion { get; init; }

    /// <summary>
    /// Gets the current version of the target stream.
    /// </summary>
    public required int TargetVersion { get; init; }

    /// <summary>
    /// Gets the number of events the target is behind the source.
    /// </summary>
    public int EventsBehind => Math.Max(0, SourceVersion - TargetVersion);

    /// <summary>
    /// Gets the number of events copied in this iteration.
    /// </summary>
    public required int EventsCopiedThisIteration { get; init; }

    /// <summary>
    /// Gets the total number of events copied across all iterations.
    /// </summary>
    public required long TotalEventsCopied { get; init; }

    /// <summary>
    /// Gets the elapsed time since the migration started.
    /// </summary>
    public required TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the source and target are currently in sync.
    /// </summary>
    public bool IsSynced => EventsBehind == 0;
}

/// <summary>
/// Strategy to use when a live migration cannot converge
/// because the source stream is receiving events faster than the migration can copy them.
/// </summary>
public enum ConvergenceFailureStrategy
{
    /// <summary>
    /// Keep trying until the close timeout is reached.
    /// The migration will fail if it cannot converge within the timeout.
    /// </summary>
    KeepTrying = 0,

    /// <summary>
    /// Fail the migration immediately if convergence is not possible.
    /// This is useful when you want to handle convergence failures manually.
    /// </summary>
    Fail = 1
}
