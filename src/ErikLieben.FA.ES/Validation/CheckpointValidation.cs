namespace ErikLieben.FA.ES.Validation;

/// <summary>
/// Provides methods for validating decision checkpoints against current stream state.
/// Used to detect stale decisions in event-sourced applications.
/// </summary>
public static class CheckpointValidation
{
    /// <summary>
    /// Validates that the stream version matches the expected version from a checkpoint.
    /// </summary>
    /// <param name="stream">The event stream to validate against.</param>
    /// <param name="checkpointFingerprint">Optional checkpoint fingerprint for simple validation.</param>
    /// <param name="checkpoint">Optional checkpoint mapping object identifiers to version identifiers.</param>
    /// <returns>A validation result indicating success or the nature of the mismatch.</returns>
    /// <remarks>
    /// If neither checkpointFingerprint nor checkpoint is provided, validation is skipped
    /// and the result is considered valid (NoCheckpointProvided).
    ///
    /// If the stream identifier is not found in the checkpoint dictionary, validation passes.
    /// This allows for new streams created after the projection was built.
    /// </remarks>
    public static CheckpointValidationResult Validate(
        IEventStream stream,
        string? checkpointFingerprint = null,
        Checkpoint? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if ((checkpoint == null || checkpoint.Count == 0) && checkpointFingerprint == null)
        {
            return CheckpointValidationResult.NoCheckpointProvided();
        }

        if (checkpoint != null && checkpoint.Count > 0)
        {
            var streamKey = stream.StreamIdentifier;

            // Find the checkpoint entry where the VersionIdentifier's StreamIdentifier matches
            foreach (var entry in checkpoint)
            {
                if (entry.Value.StreamIdentifier == streamKey)
                {
                    // Parse the version from the 20-digit padded string
                    var expectedVersion = int.Parse(entry.Value.VersionString);
                    if (stream.CurrentVersion != expectedVersion)
                    {
                        return CheckpointValidationResult.VersionMismatch(
                            streamKey, expectedVersion, stream.CurrentVersion);
                    }

                    // Found and validated - return success
                    return CheckpointValidationResult.Valid();
                }
            }
            // Stream not in checkpoint = new aggregate since projection was built
            // This is valid - proceed
        }

        // If only fingerprint is provided, we can't do detailed validation
        // but we trust the caller has already done appropriate checks
        return CheckpointValidationResult.Valid();
    }
}
