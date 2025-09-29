using System;
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
public class BlobDocumentNotFoundException : EsException
{
    private const string Code = "ELFAES-FILE-0001";

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobDocumentNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BlobDocumentNotFoundException(string message)
        : base(Code, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobDocumentNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BlobDocumentNotFoundException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }
}
