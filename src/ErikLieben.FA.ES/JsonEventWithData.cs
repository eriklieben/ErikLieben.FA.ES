namespace ErikLieben.FA.ES;

public record JsonEventWithData : JsonEvent, IEventWithData
{
    public object? Data { get; set; }
}
