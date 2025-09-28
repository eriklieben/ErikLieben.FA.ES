using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when a document factory implementation cannot be found or resolved.
/// Error Code: ELFAES-CFG-0004
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The requested document factory type is not registered.
/// - The configuration points to a non-existent or invalid factory.
///
/// Common causes:
/// - Missing DI registration for the document factory.
/// - Incorrect configuration values or type names.
///
/// Recommended actions:
/// - Register the required factory implementation in DI.
/// - Verify configuration keys and type mappings.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-cfg-0004.md
/// </remarks>
public class UnableToFindDocumentFactoryException : EsException
{
    private const string Code = "ELFAES-CFG-0004";

    public UnableToFindDocumentFactoryException(string message)
        : base(Code, message)
    {
    }

    public UnableToFindDocumentFactoryException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }
}
