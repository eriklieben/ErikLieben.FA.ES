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

/// <summary>
/// Concrete implementation of verification results.
/// </summary>
public class MigrationVerificationResult : IVerificationResult
{
    private readonly List<ValidationResult> validationResults = [];
    private readonly List<string> warnings = [];
    private readonly List<string> errors = [];

    /// <inheritdoc/>
    public bool Passed => errors.Count == 0 && validationResults.All(r => r.Passed);

    /// <inheritdoc/>
    public IReadOnlyList<ValidationResult> ValidationResults => validationResults.AsReadOnly();

    /// <inheritdoc/>
    public string Summary
    {
        get
        {
            var passedCount = validationResults.Count(r => r.Passed);
            var failedCount = validationResults.Count(r => !r.Passed);
            return Passed
                ? $"Verification passed: {passedCount} checks succeeded"
                : $"Verification failed: {passedCount} passed, {failedCount} failed";
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Warnings => warnings.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<string> Errors => errors.AsReadOnly();

    /// <summary>
    /// Adds a validation result.
    /// </summary>
    public void AddResult(ValidationResult result)
    {
        validationResults.Add(result);
        if (!result.Passed)
        {
            errors.Add(result.Message);
        }
    }

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    public void AddWarning(string message) => warnings.Add(message);

    /// <summary>
    /// Adds an error message.
    /// </summary>
    public void AddError(string message) => errors.Add(message);
}
