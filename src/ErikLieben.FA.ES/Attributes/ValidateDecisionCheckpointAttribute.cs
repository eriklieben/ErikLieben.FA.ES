namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a command method to validate decision checkpoint before execution.
/// When applied, the CLI will generate a wrapper method that validates the
/// DecisionContext parameter before calling the original method.
/// </summary>
/// <remarks>
/// This attribute is used for idempotency patterns where you want to ensure
/// that a command is only applied if the aggregate hasn't changed since the
/// user viewed the projection that informed their decision.
/// </remarks>
/// <example>
/// <code>
/// [Aggregate]
/// public partial class Order : Aggregate
/// {
///     [ValidateDecisionCheckpoint]
///     public async Task Approve(string approvedBy, DecisionContext decisionContext)
///     {
///         // This method will have a generated wrapper that validates
///         // the decisionContext before calling this method
///         await Stream.Session(context =>
///             Fold(context.Append(new OrderApproved(approvedBy, DateTime.UtcNow))));
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public class ValidateDecisionCheckpointAttribute : Attribute
{
    /// <summary>
    /// The default parameter name for the decision context.
    /// </summary>
    public const string DefaultParameterName = "decisionContext";

    /// <summary>
    /// The default maximum age for a decision in seconds.
    /// </summary>
    public const int DefaultMaxDecisionAgeSeconds = 300;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateDecisionCheckpointAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter containing the DecisionContext.</param>
    public ValidateDecisionCheckpointAttribute(string parameterName = DefaultParameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            throw new ArgumentException("Parameter name cannot be null or whitespace.", nameof(parameterName));
        }

        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets the name of the parameter containing the DecisionContext.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets or sets the maximum age of a decision in seconds before it's considered stale.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int MaxDecisionAgeSeconds { get; set; } = DefaultMaxDecisionAgeSeconds;
}
