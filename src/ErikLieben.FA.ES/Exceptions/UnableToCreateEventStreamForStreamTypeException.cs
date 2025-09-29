using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when the configured EventStream type cannot be created.
/// Error Code: ELFAES-CFG-0003
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The configured EventStream type is not registered or cannot be resolved.
/// - The fallback EventStream type is also unavailable or invalid.
///
/// Common causes:
/// - Missing DI registration or incorrect type mapping.
/// - Typos in configuration values for stream types.
///
/// Recommended actions:
/// - Verify DI registrations and configuration keys for stream types.
/// - Ensure both the primary and fallback types are public and constructible.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-cfg-0003.md
/// </remarks>
public class UnableToCreateEventStreamForStreamTypeException : EsException
{
    private const string Code = "ELFAES-CFG-0003";

    /// <summary>
    /// Gets the configured stream type that failed to resolve or create.
    /// </summary>
    /// <value>The configured EventStream type name.</value>
    public string StreamType { get; }

    /// <summary>
    /// Gets the fallback stream type that also failed to resolve or create.
    /// </summary>
    /// <value>The fallback EventStream type name.</value>
    public string FallbackStreamType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnableToCreateEventStreamForStreamTypeException"/> class.
    /// </summary>
    /// <param name="streamType">The configured stream type that failed to resolve or create.</param>
    /// <param name="fallbackStreamType">The fallback stream type that also failed to resolve or create.</param>
    public UnableToCreateEventStreamForStreamTypeException(string streamType, string fallbackStreamType)
        : base(Code, $"Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?")
    {
        StreamType = streamType;
        FallbackStreamType = fallbackStreamType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnableToCreateEventStreamForStreamTypeException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="streamType">The configured stream type that failed to resolve or create.</param>
    /// <param name="fallbackStreamType">The fallback stream type that also failed to resolve or create.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnableToCreateEventStreamForStreamTypeException(string streamType, string fallbackStreamType, Exception innerException)
        : base(Code, $"Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?", innerException)
    {
        StreamType = streamType;
        FallbackStreamType = fallbackStreamType;
    }
}
