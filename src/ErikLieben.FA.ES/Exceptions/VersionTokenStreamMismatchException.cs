using System;
using System.Runtime.Serialization;

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
[Serializable]
public class VersionTokenStreamMismatchException : EsException
{
    private const string Code = "ELFAES-VAL-0004";

    public string LeftObjectIdentifier { get; }
    public string RightObjectIdentifier { get; }

    public VersionTokenStreamMismatchException(string leftObjectIdentifier, string rightObjectIdentifier)
        : base(Code, $"Version token stream mismatch: '{leftObjectIdentifier}' vs '{rightObjectIdentifier}'.")
    {
        LeftObjectIdentifier = leftObjectIdentifier;
        RightObjectIdentifier = rightObjectIdentifier;
    }

    public VersionTokenStreamMismatchException(string leftObjectIdentifier, string rightObjectIdentifier, Exception innerException)
        : base(Code, $"Version token stream mismatch: '{leftObjectIdentifier}' vs '{rightObjectIdentifier}'.", innerException)
    {
        LeftObjectIdentifier = leftObjectIdentifier;
        RightObjectIdentifier = rightObjectIdentifier;
    }

    protected VersionTokenStreamMismatchException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        LeftObjectIdentifier = info.GetString(nameof(LeftObjectIdentifier))!;
        RightObjectIdentifier = info.GetString(nameof(RightObjectIdentifier))!;
    }

    /// <inheritdoc />
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(LeftObjectIdentifier), LeftObjectIdentifier);
        info.AddValue(nameof(RightObjectIdentifier), RightObjectIdentifier);
    }
}
