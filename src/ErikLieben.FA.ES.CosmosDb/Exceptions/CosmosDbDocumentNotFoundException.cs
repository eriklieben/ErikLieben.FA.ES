namespace ErikLieben.FA.ES.CosmosDb.Exceptions;

/// <summary>
/// Exception thrown when a requested document is not found in CosmosDB.
/// </summary>
public class CosmosDbDocumentNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDocumentNotFoundException"/> class.
    /// </summary>
    public CosmosDbDocumentNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDocumentNotFoundException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CosmosDbDocumentNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbDocumentNotFoundException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CosmosDbDocumentNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
