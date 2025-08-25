namespace ErikLieben.FA.ES.Upcasting;

public interface IEventUpcaster
{
    public bool CanUpcast(IEvent @event);

    public IEnumerable<IEvent> UpCast(IEvent @event);
}