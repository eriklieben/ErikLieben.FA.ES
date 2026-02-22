using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Validation;

/// <summary>
/// Extension methods for validating decision checkpoints on aggregates.
/// </summary>
public static class AggregateValidationExtensions
{
    /// <summary>
    /// Validates a decision checkpoint against the aggregate's current stream state.
    /// </summary>
    /// <param name="aggregate">The aggregate to validate against.</param>
    /// <param name="context">The decision context containing checkpoint information.</param>
    /// <returns>A validation result indicating success or the nature of the mismatch.</returns>
    /// <remarks>
    /// If the context is null or empty, validation is skipped and the result is considered valid.
    /// </remarks>
    public static CheckpointValidationResult ValidateCheckpoint(
        this Aggregate aggregate,
        DecisionContext? context)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        if (context is null || context.IsEmpty)
        {
            return CheckpointValidationResult.NoCheckpointProvided();
        }

        return CheckpointValidation.Validate(
            aggregate.EventStream,
            context.CheckpointFingerprint,
            context.StreamVersions);
    }
}
