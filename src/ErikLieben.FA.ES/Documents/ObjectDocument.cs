using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

public abstract class ObjectDocument : IObjectDocument
{
    protected ObjectDocument(
        string objectId,
        string objectName,
        StreamInformation active,
        IEnumerable<TerminatedStream> terminatedStreams,
        string? schemaVersion = null,
        string? hash = null,
        string? prevHash = null)
    {
        ArgumentNullException.ThrowIfNull(objectId);
        ArgumentNullException.ThrowIfNull(objectName);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(terminatedStreams);

        this.objectId = objectId;
        ObjectName = objectName;
        Active = active;
        TerminatedStreams = terminatedStreams.ToList();
        SchemaVersion = schemaVersion;
        Hash = hash;
        PrevHash = prevHash;
    }

    private string objectId;

    [JsonPropertyName("active")]
    public StreamInformation Active { get; private set; }

    [JsonPropertyName("objectId")]
    public string ObjectId { get { return objectId; } }

    [JsonPropertyName("objectName")]
    public string ObjectName { get; private set; }
    [JsonPropertyName("terminatedStreams")]
    public List<TerminatedStream> TerminatedStreams { get; private set; } = [];
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; private set; }

    [JsonIgnore]
    public string? Hash { get; protected set; }

    [JsonIgnore]
    public string? PrevHash { get; protected set; }


    public void SetHash(string? hash, string? prevHash = null)
    {
        this.Hash = hash;
        this.PrevHash = prevHash;
    }
}
