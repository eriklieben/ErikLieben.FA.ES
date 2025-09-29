using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a document tag factory implementation cannot be found or resolved.
/// Error Code: ELFAES-CFG-0005
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The requested document tag factory type is not registered.
/// - The configuration points to a non-existent or invalid tag factory.
///
/// Common causes:
/// - Missing DI registration for the document tag factory.
/// - Incorrect configuration values or type names.
///
/// Recommended actions:
/// - Register the required tag factory implementation in DI.
/// - Verify configuration keys and type mappings.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-cfg-0005.md
/// </remarks>
public class UnableToFindDocumentTagFactoryException : EsException
{
    private const string Code = "ELFAES-CFG-0005";

    /// <summary>
    /// Initializes a new instance of the <see cref="UnableToFindDocumentTagFactoryException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnableToFindDocumentTagFactoryException(string message)
        : base(Code, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnableToFindDocumentTagFactoryException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnableToFindDocumentTagFactoryException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }
}
