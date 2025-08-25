namespace ErikLieben.FA.ES.Documents;

public class StreamInformation : IStreamInformation
{
    public string StreamIdentifier { get; set; } = string.Empty;
    public string StreamType { get; set; } = string.Empty;
    public string DocumentTagType { get; set; } = string.Empty;

    public int CurrentStreamVersion { get; set; } = -1;

    public string StreamConnectionName { get; set; } = string.Empty;
    public string DocumentTagConnectionName { get; set; } = string.Empty;
    public string StreamTagConnectionName { get; set; } = string.Empty;
    public string SnapShotConnectionName { get; set; } = string.Empty;

    public StreamChunkSettings? ChunkSettings { get; set; } = new();

    public List<StreamChunk> StreamChunks { get; set; } = [];

    public List<StreamSnapShot> SnapShots { get; set; } = [];

    public bool ChunkingEnabled()
    {
        return ChunkSettings is { EnableChunks: true };
    }

    public bool HasSnapShots()
    {
        return SnapShots.Count != 0;
    }
}
