using System.Globalization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

public class ObjectMetadata
{
    protected ObjectMetadata()
    {

    }
}

public class ObjectMetadata<T> : ObjectMetadata
{
    public string? Version { get; private init; }

    public string? StreamId { get; private init; }

    public int VersionInStream { get; private init; }

    public T? Id { get; private init; }

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

    public VersionToken ToVersionToken(string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        if (string.IsNullOrWhiteSpace(StreamId))
        {
            throw new InvalidOperationException("StreamId is null or whitespace");
        }

        if (Id == null)
        {
            throw new InvalidOperationException("Id is null");
        }

        return new VersionToken(objectName, Id.ToString()!, StreamId, VersionInStream);
    }

}
