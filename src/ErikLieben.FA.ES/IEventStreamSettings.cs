namespace ErikLieben.FA.ES;

public interface IEventStreamSettings
{
    public bool ManualFolding { get; set; }

    public bool UseExternalSequencer { get; set; }
}