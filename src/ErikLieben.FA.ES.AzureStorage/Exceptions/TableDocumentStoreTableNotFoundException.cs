using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when a required Azure Table Storage table is not found.
/// Error Code: ELFAES-EXT-0011
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The expected table does not exist in the storage account.
///
/// Common causes:
/// - Table was not created during deployment.
/// - Incorrect table name configuration.
///
/// Recommended actions:
/// - Create the required table in your storage account.
/// - Enable AutoCreateTable in EventStreamTableSettings.
/// - Verify your table name configuration.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-ext-0011.md
/// </remarks>
public class TableDocumentStoreTableNotFoundException : EsException
{
    private const string Code = "ELFAES-EXT-0011";

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentStoreTableNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TableDocumentStoreTableNotFoundException(string message) : base(Code, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableDocumentStoreTableNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TableDocumentStoreTableNotFoundException(string message, Exception innerException) : base(Code, message, innerException)
    {
    }
}
