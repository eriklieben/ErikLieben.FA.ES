using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when a document entity is not found in Azure Table Storage.
/// Error Code: ELFAES-EXT-0012
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The expected document entity does not exist in the table.
///
/// Common causes:
/// - Document was never created.
/// - Document was deleted.
/// - Incorrect object ID or name.
///
/// Recommended actions:
/// - Verify the object ID and name are correct.
/// - Use CreateAsync to create the document first.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-ext-0012.md
/// </remarks>
public class TableDocumentNotFoundException : EsException
{
    private const string Code = "ELFAES-EXT-0012";

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TableDocumentNotFoundException(string message) : base(Code, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TableDocumentNotFoundException(string message, Exception innerException) : base(Code, message, innerException)
    {
    }
}
