namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.EventStreamManagement.Backup;
using ErikLieben.FA.ES.EventStreamManagement.BookClosing;
using ErikLieben.FA.ES.EventStreamManagement.Coordination;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;
using ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Fluent builder for configuring and executing stream migrations.
/// </summary>
public interface IMigrationBuilder
{
    /// <summary>
    /// Specifies the target stream identifier for the migration.
    /// </summary>
    /// <param name="streamIdentifier">The new stream identifier.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder CopyToNewStream(string streamIdentifier);

    /// <summary>
    /// Adds an event transformer to apply during migration.
    /// </summary>
    /// <param name="transformer">The transformer to use.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithTransformation(IEventTransformer transformer);

    /// <summary>
    /// Configures a transformation pipeline with multiple transformers.
    /// </summary>
    /// <param name="configure">Action to configure the pipeline.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithPipeline(Action<ITransformationPipelineBuilder> configure);

    /// <summary>
    /// Configures distributed lock settings for coordination across multiple instances.
    /// </summary>
    /// <param name="configure">Action to configure distributed lock options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithDistributedLock(Action<IDistributedLockOptions> configure);

    /// <summary>
    /// Configures backup before migration starts.
    /// </summary>
    /// <param name="configure">Action to configure backup options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithBackup(Action<IBackupBuilder> configure);

    /// <summary>
    /// Configures book closing for the old stream after migration.
    /// </summary>
    /// <param name="configure">Action to configure book closing options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithBookClosing(Action<IBookClosingBuilder> configure);

    /// <summary>
    /// Configures verification checks to run after migration.
    /// </summary>
    /// <param name="configure">Action to configure verification options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithVerification(Action<IVerificationBuilder> configure);

    /// <summary>
    /// Enables dry-run mode (simulation only, no actual migration).
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder DryRun();

    /// <summary>
    /// Configures progress reporting and monitoring.
    /// </summary>
    /// <param name="configure">Action to configure progress options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithProgress(Action<IProgressBuilder> configure);

    /// <summary>
    /// Enables support for pausing and resuming the migration.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithPauseSupport();

    /// <summary>
    /// Enables support for rolling back the migration on failure.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder WithRollbackSupport();

    /// <summary>
    /// Executes the migration with the configured settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the migration operation.</returns>
    Task<IMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a migration using a previously generated plan from a dry run.
    /// </summary>
    /// <param name="plan">The migration plan to execute.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IMigrationBuilder FromDryRunPlan(IMigrationPlan plan);

    /// <summary>
    /// Enables live migration mode where the source stream remains active during migration.
    /// Events are copied while new writes continue to the source, then the source is
    /// atomically closed using optimistic concurrency.
    /// </summary>
    /// <param name="configure">Optional action to configure live migration options.</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <remarks>
    /// Live migration uses a catch-up loop that:
    /// 1. Copies events from source to target
    /// 2. Verifies sync between source and target
    /// 3. Attempts to close source with optimistic concurrency
    /// 4. If new events arrived, repeats from step 1
    /// 5. On successful close, updates ObjectDocument to point to target
    /// </remarks>
    IMigrationBuilder WithLiveMigration(Action<ILiveMigrationOptions>? configure = null);

    /// <summary>
    /// Executes a live migration with the configured settings.
    /// This is separate from ExecuteAsync to clearly indicate live migration mode.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the live migration operation.</returns>
    Task<LiveMigrationResult> ExecuteLiveMigrationAsync(CancellationToken cancellationToken = default);
}
