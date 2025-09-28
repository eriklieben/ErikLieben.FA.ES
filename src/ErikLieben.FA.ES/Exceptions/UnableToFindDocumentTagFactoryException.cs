using System;
using System.Runtime.Serialization;

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
[Serializable]
public class UnableToFindDocumentTagFactoryException : EsException
{
    private const string Code = "ELFAES-CFG-0005";

    public UnableToFindDocumentTagFactoryException(string message)
        : base(Code, message)
    {
    }

    public UnableToFindDocumentTagFactoryException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }

    protected UnableToFindDocumentTagFactoryException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
