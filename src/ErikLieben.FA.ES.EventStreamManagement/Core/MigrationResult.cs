namespace ErikLieben.FA.ES.EventStreamManagement.Core;

using ErikLieben.FA.ES.EventStreamManagement.Progress;
using ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Implementation of migration result.
/// </summary>
public class MigrationResult : IMigrationResult
{
    /// <inheritdoc/>
    public Guid MigrationId { get; init; }

    /// <inheritdoc/>
    public bool Success { get; init; }

    /// <inheritdoc/>
    public MigrationStatus Status { get; init; }

    /// <inheritdoc/>
    public string? ErrorMessage { get; init; }

    /// <inheritdoc/>
    public Exception? Exception { get; init; }

    /// <inheritdoc/>
    public required IMigrationProgress Progress { get; init; }

    /// <inheritdoc/>
    public IVerificationResult? VerificationResult { get; init; }

    /// <inheritdoc/>
    public IMigrationPlan? Plan { get; init; }

    /// <inheritdoc/>
    public TimeSpan Duration { get; init; }

    /// <inheritdoc/>
    public MigrationStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Creates a successful migration result.
    /// </summary>
    public static MigrationResult CreateSuccess(
        Guid migrationId,
        IMigrationProgress progress,
        MigrationStatistics statistics,
        IVerificationResult? verificationResult = null)
    {
        return new MigrationResult
        {
            MigrationId = migrationId,
            Success = true,
            Status = MigrationStatus.Completed,
            Progress = progress,
            Statistics = statistics,
            VerificationResult = verificationResult,
            Duration = progress.Elapsed
        };
    }

    /// <summary>
    /// Creates a failed migration result.
    /// </summary>
    public static MigrationResult CreateFailure(
        Guid migrationId,
        IMigrationProgress progress,
        Exception exception,
        MigrationStatistics statistics)
    {
        return new MigrationResult
        {
            MigrationId = migrationId,
            Success = false,
            Status = MigrationStatus.Failed,
            ErrorMessage = exception.Message,
            Exception = exception,
            Progress = progress,
            Statistics = statistics,
            Duration = progress.Elapsed
        };
    }

    /// <summary>
    /// Creates a dry-run result with a plan.
    /// </summary>
    public static MigrationResult CreateDryRun(
        Guid migrationId,
        IMigrationProgress progress,
        IMigrationPlan plan)
    {
        return new MigrationResult
        {
            MigrationId = migrationId,
            Success = plan.IsFeasible,
            Status = MigrationStatus.Completed,
            Progress = progress,
            Plan = plan,
            Statistics = new MigrationStatistics(),
            Duration = progress.Elapsed
        };
    }
}
