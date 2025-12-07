namespace ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Represents the result of migration verification.
/// </summary>
public interface IVerificationResult
{
    /// <summary>
    /// Gets a value indicating whether verification passed.
    /// </summary>
    bool Passed { get; }

    /// <summary>
    /// Gets individual validation results.
    /// </summary>
    IReadOnlyList<ValidationResult> ValidationResults { get; }

    /// <summary>
    /// Gets a summary message of the verification.
    /// </summary>
    string Summary { get; }

    /// <summary>
    /// Gets verification warnings (non-blocking issues).
    /// </summary>
    IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gets verification errors (blocking issues).
    /// </summary>
    IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// Represents the result of a single validation check.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> record.
    /// </summary>
    public ValidationResult(string name, bool passed, string message)
    {
        Name = name;
        Passed = passed;
        Message = message;
    }

    /// <summary>
    /// Gets the name of the validation check.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// Gets a descriptive message about the validation result.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets or sets additional details about the validation.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
