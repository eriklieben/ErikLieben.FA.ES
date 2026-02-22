namespace ErikLieben.FA.ES.EventStreamManagement.Verification;

using ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Builder for configuring verification checks.
/// </summary>
public interface IVerificationBuilder
{
    /// <summary>
    /// Verifies that event counts match between source and target streams.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder CompareEventCounts();

    /// <summary>
    /// Verifies checksums match between source and target streams.
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder CompareChecksums();

    /// <summary>
    /// Validates that transformations were applied correctly by spot-checking samples.
    /// </summary>
    /// <param name="sampleSize">Number of events to sample (default 100).</param>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder ValidateTransformations(int sampleSize = 100);

    /// <summary>
    /// Verifies stream integrity (no corruption, proper sequencing).
    /// </summary>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder VerifyStreamIntegrity();

    /// <summary>
    /// Adds a custom validation function.
    /// </summary>
    /// <param name="name">The name of the validation.</param>
    /// <param name="validator">The validation function.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder CustomValidation(
        string name,
        Func<VerificationContext, Task<ValidationResult>> validator);

    /// <summary>
    /// Sets whether verification failures should block the migration.
    /// </summary>
    /// <param name="failFast">If true, migration stops on first verification failure.</param>
    /// <returns>This builder for fluent chaining.</returns>
    IVerificationBuilder FailFast(bool failFast = true);
}

/// <summary>
/// Context provided to custom validation functions.
/// </summary>
public class VerificationContext
{
    /// <summary>
    /// Gets the source stream identifier.
    /// </summary>
    public required string SourceStreamIdentifier { get; init; }

    /// <summary>
    /// Gets the target stream identifier.
    /// </summary>
    public required string TargetStreamIdentifier { get; init; }

    /// <summary>
    /// Gets the event transformer used during migration, if any.
    /// </summary>
    public IEventTransformer? Transformer { get; init; }

    /// <summary>
    /// Gets the migration statistics.
    /// </summary>
    public required Core.MigrationStatistics Statistics { get; init; }
}
