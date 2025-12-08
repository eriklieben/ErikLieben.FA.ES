namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict occurs during event stream operations.
/// This typically happens when another process has written events to the stream
/// between reading and writing.
/// </summary>
public class OptimisticConcurrencyException : EsException
{
    private const string ErrorCodeValue = "ES_CONCURRENCY_CONFLICT";

    /// <summary>
    /// Gets the identifier of the stream where the conflict occurred.
    /// </summary>
    public string StreamIdentifier { get; }

    /// <summary>
    /// Gets the expected version that was used in the write attempt.
    /// </summary>
    public int? ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual version found in the stream.
    /// </summary>
    public int? ActualVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticConcurrencyException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream.</param>
    /// <param name="message">The error message.</param>
    public OptimisticConcurrencyException(string streamIdentifier, string message)
        : base(ErrorCodeValue, message)
    {
        StreamIdentifier = streamIdentifier;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticConcurrencyException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream.</param>
    /// <param name="expectedVersion">The expected version.</param>
    /// <param name="actualVersion">The actual version found.</param>
    public OptimisticConcurrencyException(string streamIdentifier, int expectedVersion, int actualVersion)
        : base(ErrorCodeValue, $"Optimistic concurrency conflict on stream '{streamIdentifier}'. Expected version {expectedVersion}, but found {actualVersion}.")
    {
        StreamIdentifier = streamIdentifier;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimisticConcurrencyException"/> class.
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public OptimisticConcurrencyException(string streamIdentifier, string message, Exception innerException)
        : base(ErrorCodeValue, message, innerException)
    {
        StreamIdentifier = streamIdentifier;
    }
}
