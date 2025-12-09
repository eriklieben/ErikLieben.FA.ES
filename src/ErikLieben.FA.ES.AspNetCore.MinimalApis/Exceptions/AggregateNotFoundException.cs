namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

/// <summary>
/// Exception thrown when an aggregate cannot be found in the event store
/// and <see cref="EventStreamAttribute.CreateIfNotExists"/> is <c>false</c>.
/// </summary>
public sealed class AggregateNotFoundException : Exception
{
    /// <summary>
    /// Gets the type of the aggregate that was not found.
    /// </summary>
    public Type? AggregateType { get; }

    /// <summary>
    /// Gets the object identifier that was searched for.
    /// </summary>
    public string? ObjectId { get; }

    /// <summary>
    /// Gets the object type/name used for document storage.
    /// </summary>
    public string? ObjectType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateNotFoundException"/> class.
    /// </summary>
    public AggregateNotFoundException()
        : base("The requested aggregate was not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateNotFoundException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AggregateNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateNotFoundException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public AggregateNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateNotFoundException"/> class
    /// with aggregate details.
    /// </summary>
    /// <param name="aggregateType">The type of the aggregate that was not found.</param>
    /// <param name="objectId">The object identifier that was searched for.</param>
    /// <param name="objectType">The object type/name used for document storage.</param>
    public AggregateNotFoundException(Type aggregateType, string objectId, string objectType)
        : base($"Aggregate of type '{aggregateType.Name}' with object ID '{objectId}' (object type: '{objectType}') was not found.")
    {
        AggregateType = aggregateType;
        ObjectId = objectId;
        ObjectType = objectType;
    }
}
