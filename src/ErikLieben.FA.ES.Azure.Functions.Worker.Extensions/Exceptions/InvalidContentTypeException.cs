using System;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;

/// <summary>
/// Exception thrown when an invalid content-type is provided to an Azure Functions Worker binding.
/// Error Code: ELFAES-VAL-0003
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The provided request/content type does not match the supported type(s).
///
/// Common causes:
/// - Client sends an unexpected Content-Type header.
/// - Binding or attribute expects a different content type.
///
/// Recommended actions:
/// - Ensure the request Content-Type matches the expected value(s).
/// - Update binding configuration or client request accordingly.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-val-0003.md
/// </remarks>
[Serializable]
internal class InvalidContentTypeException : EsException
{
    private const string Code = "ELFAES-VAL-0003";

    /// <summary>
    /// Initializes a new instance of the exception with a formatted message including the actual and expected content types.
    /// </summary>
    /// <param name="actualContentType">The content type that is being provided.</param>
    /// <param name="expectedContentType">The content type(s) that is supported.</param>
    public InvalidContentTypeException(string actualContentType, string expectedContentType)
        : base(Code, $"Unexpected content-type '{actualContentType}'. Only '{expectedContentType}' is supported.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the exception with a formatted message and an inner exception.
    /// </summary>
    /// <param name="actualContentType">The content type that is being provided.</param>
    /// <param name="expectedContentType">The content type(s) that is supported.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidContentTypeException(string actualContentType, string expectedContentType, Exception innerException)
        : base(Code, $"Unexpected content-type '{actualContentType}'. Only '{expectedContentType}' is supported.", innerException)
    {
    }

    protected InvalidContentTypeException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
