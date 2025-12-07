namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when attempting to append events to a closed event stream.
/// A stream is closed when it contains an EventStream.Closed event, typically after migration.
/// </summary>
/// <remarks>
/// When this exception is thrown, the caller should:
/// 1. Reload the object document to get the current active stream
/// 2. Retry the operation on the active stream (which may be a continuation stream)
/// </remarks>
public class EventStreamClosedException : EsException
{
    private const string ErrorCodeValue = "ES_STREAM_CLOSED";

    /// <summary>
    /// Gets the identifier of the closed stream.
    /// </summary>
    public string StreamIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamClosedException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the closed stream.</param>
    /// <param name="message">The error message.</param>
    public EventStreamClosedException(string streamIdentifier, string message)
        : base(ErrorCodeValue, message)
    {
        StreamIdentifier = streamIdentifier;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamClosedException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the closed stream.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public EventStreamClosedException(string streamIdentifier, string message, Exception innerException)
        : base(ErrorCodeValue, message, innerException)
    {
        StreamIdentifier = streamIdentifier;
    }
}
