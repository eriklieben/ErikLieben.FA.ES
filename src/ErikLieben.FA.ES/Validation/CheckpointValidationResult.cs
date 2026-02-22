namespace ErikLieben.FA.ES.Validation;

/// <summary>
/// Represents the result of validating a decision checkpoint against the current stream state.
/// </summary>
/// <param name="IsValid">True if the checkpoint is valid or no checkpoint was provided.</param>
/// <param name="StreamId">The stream identifier that failed validation, if any.</param>
/// <param name="ExpectedVersion">The expected version from the checkpoint, if validation failed.</param>
/// <param name="ActualVersion">The actual current version, if validation failed.</param>
/// <param name="Message">A human-readable message describing the validation result.</param>
public record CheckpointValidationResult(
    bool IsValid,
    string? StreamId,
    int? ExpectedVersion,
    int? ActualVersion,
    string? Message)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static CheckpointValidationResult Valid()
        => new(true, null, null, null, null);

    /// <summary>
    /// Creates a validation result indicating no checkpoint was provided.
    /// This is considered valid (validation is skipped).
    /// </summary>
    public static CheckpointValidationResult NoCheckpointProvided()
        => new(true, null, null, null, "No checkpoint provided");

    /// <summary>
    /// Creates a validation result indicating a version mismatch.
    /// </summary>
    /// <param name="streamId">The stream identifier that failed validation.</param>
    /// <param name="expected">The expected version from the checkpoint.</param>
    /// <param name="actual">The actual current version of the stream.</param>
    public static CheckpointValidationResult VersionMismatch(string streamId, int expected, int actual)
        => new(false, streamId, expected, actual,
            $"Stream {streamId} version mismatch: expected v{expected}, actual v{actual}");
}
