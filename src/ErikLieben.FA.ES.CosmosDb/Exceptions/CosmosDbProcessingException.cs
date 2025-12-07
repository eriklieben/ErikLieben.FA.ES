namespace ErikLieben.FA.ES.CosmosDb.Exceptions;

/// <summary>
/// Exception thrown when a CosmosDB operation fails during processing.
/// </summary>
public class CosmosDbProcessingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbProcessingException"/> class.
    /// </summary>
    public CosmosDbProcessingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbProcessingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CosmosDbProcessingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbProcessingException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CosmosDbProcessingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
