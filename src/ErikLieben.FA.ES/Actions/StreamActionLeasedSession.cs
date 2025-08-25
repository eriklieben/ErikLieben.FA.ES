namespace ErikLieben.FA.ES.Actions;

public class StreamActionLeasedSession(ILeasedSession session) : ILeasedSession
{
    public IEvent<PayloadType> Append<PayloadType>(PayloadType payload, ActionMetadata? actionMetadata = null, string? overrideEventType = null,
        string? externalSequencer = null, Dictionary<string, string>? metadata = null) where PayloadType : class
    {
        // Pre append

        return session.Append(payload, actionMetadata, overrideEventType, externalSequencer, metadata);

        // Post append
    }

    public List<JsonEvent> Buffer => session.Buffer;

    public Task CommitAsync()
    {
        // Pre commit
        // Buffer.ForEach();

        return session.CommitAsync();

        // Post commit
        // Buffer.ForEach();
    }

    public Task<bool> IsTerminatedASync(string streamIdentifier)
    {
        return session.IsTerminatedASync(streamIdentifier);
    }

    public Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null)
    {
        return session.ReadAsync(startVersion, untilVersion);
    }
}