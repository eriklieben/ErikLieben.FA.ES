namespace ErikLieben.FA.ES.Testing.PropertyBased;

/// <summary>
/// Configuration options for property-based tests.
/// </summary>
public class PropertyTestOptions
{
    /// <summary>
    /// Gets or sets the number of iterations to run. Default is 100.
    /// </summary>
    public int Iterations { get; set; } = 100;

    /// <summary>
    /// Gets or sets the random seed for reproducibility. If null, a random seed is used.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Gets or sets whether to stop on the first failure. Default is true.
    /// </summary>
    public bool StopOnFailure { get; set; } = true;
}

/// <summary>
/// Contains the result of a property-based test run.
/// </summary>
public class PropertyTestResult
{
    /// <summary>
    /// Gets or sets the random seed used for the test.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Gets or sets the number of iterations executed.
    /// </summary>
    public int IterationsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the number of iterations that passed.
    /// </summary>
    public int IterationsPassed { get; set; }

    /// <summary>
    /// Gets or sets the number of iterations that failed.
    /// </summary>
    public int IterationsFailed { get; set; }

    /// <summary>
    /// Gets or sets whether the test was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the failure message if the test failed.
    /// </summary>
    public string? FailureMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused the failure, if any.
    /// </summary>
    public Exception? FailureException { get; set; }
}
