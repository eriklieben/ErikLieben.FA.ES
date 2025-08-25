using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

public interface IProjectionWhenParameterValueFactory<out TValue, in TEventType> 
    : IProjectionWhenParameterValueFactory where TEventType : class
{
    public TValue Create(IObjectDocument document, IEvent<TEventType> @event);
}


public interface IProjectionWhenParameterValueFactory<out TValue> 
    : IProjectionWhenParameterValueFactory 
{
    public TValue Create(IObjectDocument document, IEvent @event);
}

public interface IProjectionWhenParameterValueFactory
{
}


