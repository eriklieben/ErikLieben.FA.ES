using System.Globalization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Provides a base class for object metadata that tracks event stream positioning and versioning information.
/// </summary>
public class ObjectMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectMetadata"/> class.
    /// </summary>
    protected ObjectMetadata()
    {

    }
}

/// <summary>
/// Represents metadata for an object that includes version, stream identification, and positioning within the event stream.
/// </summary>
/// <typeparam name="T">The type of the object identifier.</typeparam>
public class ObjectMetadata<T> : ObjectMetadata
{
    /// <summary>
    /// Gets the combined version string that includes stream identifier and version number.
    /// </summary>
    public string? Version { get; private init; }

    /// <summary>
    /// Gets the identifier of the event stream this object belongs to.
    /// </summary>
    public string? StreamId { get; private init; }

    /// <summary>
    /// Gets the version number of the event within its stream.
    /// </summary>
    public int VersionInStream { get; private init; }

    /// <summary>
    /// Gets the unique identifier for this object.
    /// </summary>
    public T? Id { get; private init; }

    /// <summary>
    /// Creates object metadata from an event document, event, and identifier.
    /// </summary>
    /// <param name="document">The object document containing stream information.</param>
    /// <param name="event">The event from which to extract version information.</param>
    /// <param name="id">The unique identifier for the object.</param>
    /// <returns>A new <see cref="ObjectMetadata{T}"/> instance populated with the provided information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="event"/> is null.</exception>
    public static ObjectMetadata<T> From(IObjectDocument document, IEvent @event, T id)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return new ObjectMetadata<T>
        {
            Id = id,
            StreamId = document.Active.StreamIdentifier,
            VersionInStream = @event.EventVersion,
            Version = $"{document.Active.StreamIdentifier}:{@event.EventVersion:d20}"
        };
    }

    /// <summary>
    /// Converts this metadata to a version token for the specified object name.
    /// </summary>
    /// <param name="objectName">The name of the object type.</param>
    /// <returns>A <see cref="VersionToken"/> representing this object's version.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="objectName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when StreamId is null or whitespace, or when Id is null.</exception>
    public VersionToken ToVersionToken(string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        if (string.IsNullOrWhiteSpace(StreamId))
        {
            throw new InvalidOperationException("StreamId is null or whitespace");
        }

        if (EqualityComparer<T>.Default.Equals(Id, default))
        {
            throw new InvalidOperationException("Id is null");
        }

        return new VersionToken(objectName, Id!.ToString()!, StreamId, VersionInStream);
    }

    /// <summary>
    /// Converts this metadata to a causation ID string for traceability.
    /// The causation ID is the string representation of the version token.
    /// </summary>
    /// <param name="objectName">The name of the object type.</param>
    /// <returns>A string that can be used as CausationId in ActionMetadata.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="objectName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when StreamId is null or whitespace, or when Id is null.</exception>
    public string ToCausationId(string objectName)
    {
        return ToVersionToken(objectName).Value;
    }

}
