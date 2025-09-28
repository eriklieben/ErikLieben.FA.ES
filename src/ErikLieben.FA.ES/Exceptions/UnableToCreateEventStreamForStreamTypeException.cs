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

    public string StreamType { get; }
    public string FallbackStreamType { get; }

    public UnableToCreateEventStreamForStreamTypeException(string streamType, string fallbackStreamType)
        : base(Code, $"Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?")
    {
        StreamType = streamType;
        FallbackStreamType = fallbackStreamType;
    }

    public UnableToCreateEventStreamForStreamTypeException(string streamType, string fallbackStreamType, Exception innerException)
        : base(Code, $"Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?", innerException)
    {
        StreamType = streamType;
        FallbackStreamType = fallbackStreamType;
    }
}
