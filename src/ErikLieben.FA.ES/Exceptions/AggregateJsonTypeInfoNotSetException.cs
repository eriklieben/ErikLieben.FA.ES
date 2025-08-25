namespace ErikLieben.FA.ES.Exceptions;

public class AggregateJsonTypeInfoNotSetException : Exception
{
    public AggregateJsonTypeInfoNotSetException()
        : base("Aggregate JsonInfo type should be set to deserialize the aggregate type")
    {
        
    }
}
