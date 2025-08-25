namespace ErikLieben.FA.ES;

public interface IEventWithData : IEvent
{
    object? Data { get; }
}