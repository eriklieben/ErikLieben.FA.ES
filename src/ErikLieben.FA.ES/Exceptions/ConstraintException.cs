using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a business constraint is violated.
/// Error Code: ELFAES-BIZ-0001
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - A new event violates an existing constraint on a stream.
/// - A new constraint conflicts with the current stream state.
///
/// Common causes:
/// - Incorrect precondition checks before appending events.
/// - Concurrency or state validation issues.
///
/// Recommended actions:
/// - Validate constraints before committing changes.
/// - Inspect the <see cref="Constraint"/> for details and resolve conflicts.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-biz-0001.md
/// </remarks>
public class ConstraintException : EsException
{
    private const string Code = "ELFAES-BIZ-0001";

    /// <summary>
    /// Gets the constraint that was violated.
    /// </summary>
    /// <value>The constraint that triggered this exception.</value>
    public Constraint Constraint { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstraintException"/> class with a specified error message and the violated constraint.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="constraint">The constraint that was violated.</param>
    public ConstraintException(string message, Constraint constraint)
        : base(Code, message)
    {
        Constraint = constraint;
    }
}
