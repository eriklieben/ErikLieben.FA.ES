namespace ErikLieben.FA.ES.EventStream;

public class EventStreamSettings : IEventStreamSettings
{
    public bool ManualFolding { get; set; }
    public bool UseExternalSequencer { get; set; }
}
