namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when attempting to append events to a closed event stream.
/// A stream is closed when it contains an EventStream.Closed event, typically after migration.
/// </summary>
/// <remarks>
/// When this exception is thrown, the caller should:
/// 1. Check if <see cref="HasContinuation"/> is true
/// 2. If true, the continuation stream information is available in this exception
/// 3. If false, reload the object document to get the current active stream
/// 4. Retry the operation on the continuation/active stream
///
/// When automatic retry is enabled (default), the library handles this transparently.
/// </remarks>
public class EventStreamClosedException : EsException
{
    private const string ErrorCodeValue = "ES_STREAM_CLOSED";

    /// <summary>
    /// Gets the identifier of the closed stream.
    /// </summary>
    public string StreamIdentifier { get; }

    /// <summary>
    /// Gets the identifier of the continuation stream, if available.
    /// </summary>
    public string? ContinuationStreamId { get; }

    /// <summary>
    /// Gets the type of the continuation stream (blob, table, cosmos), if available.
    /// </summary>
    public string? ContinuationStreamType { get; }

    /// <summary>
    /// Gets the data store connection name for the continuation stream, if available.
    /// </summary>
    public string? ContinuationDataStore { get; }

    /// <summary>
    /// Gets the document store connection name for the continuation stream, if available.
    /// </summary>
    public string? ContinuationDocumentStore { get; }

    /// <summary>
    /// Gets the reason for stream closure, if available.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets a value indicating whether this exception contains continuation stream information.
    /// When true, the caller can use the continuation properties to retry on the new stream.
    /// When false, the caller should reload the object document to get the active stream.
    /// </summary>
    public bool HasContinuation => !string.IsNullOrEmpty(ContinuationStreamId);

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
    /// Initializes a new instance of the <see cref="EventStreamClosedException"/> class
    /// with continuation stream information.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the closed stream.</param>
    /// <param name="continuationStreamId">The identifier of the continuation stream.</param>
    /// <param name="continuationStreamType">The type of the continuation stream.</param>
    /// <param name="continuationDataStore">The data store for the continuation stream.</param>
    /// <param name="continuationDocumentStore">The document store for the continuation stream.</param>
    /// <param name="reason">The reason for stream closure.</param>
    public EventStreamClosedException(
        string streamIdentifier,
        string continuationStreamId,
        string continuationStreamType,
        string continuationDataStore,
        string continuationDocumentStore,
        string? reason = null)
        : base(ErrorCodeValue, $"Stream '{streamIdentifier}' is closed. Continuation stream: '{continuationStreamId}'")
    {
        StreamIdentifier = streamIdentifier;
        ContinuationStreamId = continuationStreamId;
        ContinuationStreamType = continuationStreamType;
        ContinuationDataStore = continuationDataStore;
        ContinuationDocumentStore = continuationDocumentStore;
        Reason = reason;
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
