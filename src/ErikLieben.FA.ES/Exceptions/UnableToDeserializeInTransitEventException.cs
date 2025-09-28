using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when an in-transit event cannot be deserialized.
/// Error Code: ELFAES-VAL-0001
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - Attempting to deserialize an event from a null or invalid payload.
///
/// Common causes:
/// - The serialized event value is null or empty.
/// - Mismatched or missing JsonTypeInfo for the event type.
///
/// Recommended actions:
/// - Validate inputs before deserialization.
/// - Ensure JsonTypeInfo is provided and matches the expected event type.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-val-0001.md
/// </remarks>
public class UnableToDeserializeInTransitEventException : EsException
{
    private const string Code = "ELFAES-VAL-0001";

    public UnableToDeserializeInTransitEventException()
        : base(Code, "Unable to deserialize to event, value is 'null'")
    {
    }

    public UnableToDeserializeInTransitEventException(string message)
        : base(Code, message)
    {
    }

    public UnableToDeserializeInTransitEventException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }
}
