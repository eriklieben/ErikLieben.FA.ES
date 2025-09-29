using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when the JSON TypeInfo for the aggregate type has not been configured.
/// Error Code: ELFAES-CFG-0001
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - Attempting to deserialize an aggregate without providing the required JsonTypeInfo.
///
/// Common causes:
/// - Missing configuration of JsonSerializerContext for aggregate types.
/// - Incorrect DI setup that omits JsonTypeInfo registration.
///
/// Recommended actions:
/// - Ensure the aggregate JsonTypeInfo is registered in the JsonSerializerContext.
/// - Verify your configuration/DI registration for serialization setup.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-cfg-0001.md
/// </remarks>
public class AggregateJsonTypeInfoNotSetException : EsException
{
    private const string Code = "ELFAES-CFG-0001";

    public AggregateJsonTypeInfoNotSetException()
        : base(Code, "Aggregate JsonInfo type should be set to deserialize the aggregate type")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateJsonTypeInfoNotSetException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the configuration error.</param>
    public AggregateJsonTypeInfoNotSetException(string message)
        : base(Code, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateJsonTypeInfoNotSetException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the configuration error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AggregateJsonTypeInfoNotSetException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }
}
