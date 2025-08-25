namespace ErikLieben.FA.ES.Exceptions;

public class UnableToCreateEventStreamForStreamTypeException(string streamType, string fallbackStreamType)
    : Exception($"Unable to create EventStream of the type {streamType} or {fallbackStreamType}. Is your configuration correct?")
{
}
