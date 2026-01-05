namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

/// <summary>
/// Implementation of <see cref="ILiveMigrationOptions"/> for configuring live migration behavior.
/// </summary>
public class LiveMigrationOptions : ILiveMigrationOptions
{
    /// <summary>
    /// Gets the maximum time to spend attempting to close the source stream.
    /// </summary>
    public TimeSpan CloseTimeout { get; private set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the delay between catch-up iterations.
    /// </summary>
    public TimeSpan CatchUpDelay { get; private set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the progress callback invoked after each catch-up iteration.
    /// </summary>
    public Action<LiveMigrationProgress>? ProgressCallback { get; private set; }

    /// <summary>
    /// Gets the strategy to use when convergence fails.
    /// </summary>
    public ConvergenceFailureStrategy FailureStrategy { get; private set; } = ConvergenceFailureStrategy.KeepTrying;

    /// <summary>
    /// Gets the maximum number of catch-up iterations (0 = unlimited).
    /// </summary>
    public int MaxIterations { get; private set; } = 0;

    /// <summary>
    /// Gets the async callback invoked for each event copied during catch-up iterations.
    /// </summary>
    public Func<LiveMigrationEventProgress, Task>? EventCopiedCallback { get; private set; }

    /// <summary>
    /// Gets the async callback invoked immediately before each event is appended.
    /// When set, events are appended one at a time instead of batched.
    /// </summary>
    public Func<LiveMigrationEventProgress, Task>? BeforeAppendCallback { get; private set; }

    /// <inheritdoc/>
    public ILiveMigrationOptions WithCloseTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }

        CloseTimeout = timeout;
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions WithCatchUpDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        }

        CatchUpDelay = delay;
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions OnCatchUpProgress(Action<LiveMigrationProgress> callback)
    {
        ProgressCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions OnConvergenceFailure(ConvergenceFailureStrategy strategy)
    {
        FailureStrategy = strategy;
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions WithMaxIterations(int maxIterations)
    {
        if (maxIterations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "Max iterations cannot be negative.");
        }

        MaxIterations = maxIterations;
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions OnEventCopied(Func<LiveMigrationEventProgress, Task> callback)
    {
        EventCopiedCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    /// <inheritdoc/>
    public ILiveMigrationOptions OnBeforeAppend(Func<LiveMigrationEventProgress, Task> callback)
    {
        BeforeAppendCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }
}
