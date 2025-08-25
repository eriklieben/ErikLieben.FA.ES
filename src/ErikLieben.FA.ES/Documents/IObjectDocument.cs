using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

public interface IObjectDocument
{
    StreamInformation Active { get; }

    string ObjectId { get; }
    
    string ObjectName { get; }

    List<TerminatedStream> TerminatedStreams { get; }

    string? SchemaVersion { get; }

    [JsonIgnore]
    string? Hash { get; }

    [JsonIgnore]
    string? PrevHash { get; }

    void SetHash(string? hash, string? prevHash = null);
}

public interface IObjectDocumentWithMethods : IObjectDocument
{
    public Task SetTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag);
}

[Flags]
public enum TagTypes
{
    DocumentTag = 1,
    StreamTag = 2,
}