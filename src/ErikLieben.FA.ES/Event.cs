namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a strongly-typed event with a specific payload type.
/// </summary>
/// <typeparam name="PayloadType">The type of the event payload.</typeparam>
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
