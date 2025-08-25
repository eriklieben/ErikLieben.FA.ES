namespace ErikLieben.FA.ES;

public interface ILeasedSession
{
    IEvent<PayloadType> Append<PayloadType>(
        PayloadType payload,
        ActionMetadata? actionMetadata = null,
        string? overrideEventType = null,
        string? externalSequencer = null,
        Dictionary<string, string>? metadata = null) where PayloadType : class;

    List<JsonEvent> Buffer { get; }

    Task CommitAsync();

    Task<bool> IsTerminatedASync(string streamIdentifier);

    Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null);
}
