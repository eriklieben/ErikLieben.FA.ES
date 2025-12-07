namespace ErikLieben.FA.ES.EventStreamManagement.Verification;

using ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Builder for configuring verification operations.
/// </summary>
public class VerificationBuilder : IVerificationBuilder
{
    private readonly VerificationConfiguration config = new();

    /// <inheritdoc/>
    public IVerificationBuilder CompareEventCounts()
    {
        config.CompareEventCounts = true;
        return this;
    }

    /// <inheritdoc/>
    public IVerificationBuilder CompareChecksums()
    {
        config.CompareChecksums = true;
        return this;
    }

    /// <inheritdoc/>
    public IVerificationBuilder ValidateTransformations(int sampleSize = 100)
    {
        config.ValidateTransformations = true;
        config.TransformationSampleSize = sampleSize;
        return this;
    }

    /// <inheritdoc/>
    public IVerificationBuilder VerifyStreamIntegrity()
    {
        config.VerifyStreamIntegrity = true;
        return this;
    }

    /// <inheritdoc/>
    public IVerificationBuilder CustomValidation(
        string name,
        Func<VerificationContext, Task<ValidationResult>> validator)
    {
        config.CustomValidations.Add((name, validator));
        return this;
    }

    /// <inheritdoc/>
    public IVerificationBuilder FailFast(bool failFast = true)
    {
        config.FailFast = failFast;
        return this;
    }

    /// <summary>
    /// Builds the verification configuration.
    /// </summary>
    internal VerificationConfiguration Build() => config;
}
