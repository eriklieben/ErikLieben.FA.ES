using System;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when the requested blob document could not be found.
/// Error Code: ELFAES-FILE-0001
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - A blob corresponding to the requested document identifier does not exist.
///
/// Common causes:
/// - Incorrect blob name or path.
/// - The document was deleted or never uploaded.
///
/// Recommended actions:
/// - Verify the identifier and container configuration.
/// - Ensure the blob exists and the application has access.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-file-0001.md
/// </remarks>
[Serializable]
public class BlobDocumentNotFoundException : EsException
{
    private const string Code = "ELFAES-FILE-0001";

    public BlobDocumentNotFoundException(string message)
        : base(Code, message)
    {
    }

    public BlobDocumentNotFoundException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }

    protected BlobDocumentNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
