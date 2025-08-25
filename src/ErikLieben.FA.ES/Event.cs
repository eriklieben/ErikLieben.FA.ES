namespace ErikLieben.FA.ES;

public record Event<PayloadType>() : JsonEventWithData, IEvent<PayloadType> where PayloadType : class
{
    PayloadType IEvent<PayloadType>.Data()
    {
        if (Data is not PayloadType data)
        {
            throw new InvalidCastException();
        }
        return data;
    }
}
