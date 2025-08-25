using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

[JsonDerivedType(typeof(StreamInformation), typeDiscriminator: nameof(StreamInformation))]
public interface IStreamInformation
{
    StreamChunkSettings? ChunkSettings { get; set; }
    
    string DocumentTagConnectionName { get; set; }
    
    string DocumentTagType { get; set; }
    
    string SnapShotConnectionName { get; set; }

    int CurrentStreamVersion { get; set; }

    List<StreamSnapShot> SnapShots { get; set; }
    
    List<StreamChunk> StreamChunks { get; set; }

    string StreamConnectionName { get; set; }

    string StreamIdentifier { get; set; }
    
    string StreamTagConnectionName { get; set; }
    
    string StreamType { get; set; }

    bool ChunkingEnabled();
    bool HasSnapShots();
}