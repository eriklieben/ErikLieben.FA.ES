using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when two version tokens being compared refer to different streams.
/// Error Code: ELFAES-VAL-0004
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - Comparing version tokens that belong to different object identifiers.
///
/// Common causes:
/// - Mixing tokens from different streams inadvertently.
/// - Incorrectly constructed comparison inputs.
///
/// Recommended actions:
/// - Ensure both version tokens refer to the same stream/object identifier before comparing.
/// - Validate inputs prior to calling comparison methods.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-val-0004.md
/// </remarks>
public class VersionTokenStreamMismatchException : EsException
{
    private const string Code = "ELFAES-VAL-0004";

    /// <summary>
    /// Gets the object identifier associated with the left version token.
    /// </summary>
    public string LeftObjectIdentifier { get; }

    /// <summary>
    /// Gets the object identifier associated with the right version token.
    /// </summary>
    public string RightObjectIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionTokenStreamMismatchException"/> class with the specified object identifiers.
    /// </summary>
    /// <param name="leftObjectIdentifier">The object identifier of the left version token.</param>
    /// <param name="rightObjectIdentifier">The object identifier of the right version token.</param>
    public VersionTokenStreamMismatchException(string leftObjectIdentifier, string rightObjectIdentifier)
        : base(Code, $"Version token stream mismatch: '{leftObjectIdentifier}' vs '{rightObjectIdentifier}'.")
    {
        LeftObjectIdentifier = leftObjectIdentifier;
        RightObjectIdentifier = rightObjectIdentifier;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionTokenStreamMismatchException"/> class with the specified object identifiers and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="leftObjectIdentifier">The object identifier of the left version token.</param>
    /// <param name="rightObjectIdentifier">The object identifier of the right version token.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VersionTokenStreamMismatchException(string leftObjectIdentifier, string rightObjectIdentifier, Exception innerException)
        : base(Code, $"Version token stream mismatch: '{leftObjectIdentifier}' vs '{rightObjectIdentifier}'.", innerException)
    {
        LeftObjectIdentifier = leftObjectIdentifier;
        RightObjectIdentifier = rightObjectIdentifier;
    }
}
