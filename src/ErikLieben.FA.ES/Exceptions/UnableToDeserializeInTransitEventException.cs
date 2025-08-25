namespace ErikLieben.FA.ES.Exceptions;

public class UnableToDeserializeInTransitEventException : Exception
{
    public UnableToDeserializeInTransitEventException()
        : base("Unable to deserialize to event, value is 'null'")
    {
        
    }
}
