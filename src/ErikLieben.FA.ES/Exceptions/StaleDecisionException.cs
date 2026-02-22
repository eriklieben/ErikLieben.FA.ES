using ErikLieben.FA.ES.Validation;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a command is attempted with a stale decision checkpoint.
/// This indicates that the aggregate has changed since the user viewed the projection
/// that informed their decision.
/// </summary>
/// <remarks>
/// This exception should typically result in a 409 Conflict HTTP response,
/// prompting the user to refresh their view and retry the action.
/// </remarks>
public class StaleDecisionException : EsException
{
    /// <summary>
    /// The error code for stale decision exceptions.
    /// </summary>
    public const string StaleDecisionErrorCode = "ELFAES-STALE-0001";

    /// <summary>
    /// Initializes a new instance of the <see cref="StaleDecisionException"/> class
    /// with the validation result that caused the exception.
    /// </summary>
    /// <param name="validationResult">The validation result indicating the version mismatch.</param>
    public StaleDecisionException(CheckpointValidationResult validationResult)
        : base(StaleDecisionErrorCode, validationResult.Message ?? "Decision is based on stale data")
    {
        ValidationResult = validationResult;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StaleDecisionException"/> class
    /// with specific version mismatch details.
    /// </summary>
    /// <param name="streamId">The stream identifier that has changed.</param>
    /// <param name="expectedVersion">The version the decision was based on.</param>
    /// <param name="actualVersion">The current version of the stream.</param>
    public StaleDecisionException(string streamId, int expectedVersion, int actualVersion)
        : base(StaleDecisionErrorCode,
            $"State changed: {streamId} was v{expectedVersion}, now v{actualVersion}. Please refresh.")
    {
        ValidationResult = CheckpointValidationResult.VersionMismatch(streamId, expectedVersion, actualVersion);
    }

    /// <summary>
    /// Gets the validation result that caused this exception.
    /// </summary>
    public CheckpointValidationResult ValidationResult { get; }

    /// <summary>
    /// Gets the stream identifier that failed validation, if available.
    /// </summary>
    public string? StreamId => ValidationResult.StreamId;

    /// <summary>
    /// Gets the expected version from the checkpoint, if available.
    /// </summary>
    public int? ExpectedVersion => ValidationResult.ExpectedVersion;

    /// <summary>
    /// Gets the actual current version, if available.
    /// </summary>
    public int? ActualVersion => ValidationResult.ActualVersion;
}
