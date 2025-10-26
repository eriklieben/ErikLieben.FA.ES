using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Provides a base implementation for object documents that store stream metadata and concurrency hashes.
/// </summary>
public abstract class ObjectDocument : IObjectDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDocument"/> class.
    /// </summary>
    /// <param name="objectId">The identifier of the object.</param>
    /// <param name="objectName">The logical name/type of the object.</param>
    /// <param name="active">The active stream information.</param>
    /// <param name="terminatedStreams">The list of terminated streams for the object.</param>
    /// <param name="schemaVersion">The schema version of the document; may be null.</param>
    /// <param name="hash">The current hash used for optimistic concurrency; may be null.</param>
    /// <param name="prevHash">The previous hash used for optimistic concurrency; may be null.</param>
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

    /// <summary>
    /// Gets the active stream information that controls how events are read and appended.
    /// </summary>
    [JsonPropertyName("active")]
    public StreamInformation Active { get; private set; }

    /// <summary>
    /// Gets the identifier of the object.
    /// </summary>
    [JsonPropertyName("objectId")]
    public string ObjectId { get { return objectId; } }

    /// <summary>
    /// Gets the logical name/type of the object.
    /// </summary>
    [JsonPropertyName("objectName")]
    public string ObjectName { get; private set; }

    /// <summary>
    /// Gets the list of terminated streams for the object.
    /// </summary>
    [JsonPropertyName("terminatedStreams")]
    public List<TerminatedStream> TerminatedStreams { get; private set; }

    /// <summary>
    /// Gets the schema version of the materialized document; may be null.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; private set; }

    /// <summary>
    /// Gets the current hash of the materialized document content used for optimistic concurrency; may be null.
    /// </summary>
    [JsonIgnore]
    public string? Hash { get; protected set; }

    /// <summary>
    /// Gets the previous hash value used for optimistic concurrency; may be null.
    /// </summary>
    [JsonIgnore]
    public string? PrevHash { get; protected set; }

    /// <summary>
    /// Sets the current and previous hash values used for optimistic concurrency checks.
    /// </summary>
    /// <param name="hash">The new content hash value; may be null.</param>
    /// <param name="prevHash">The previous content hash value; null to keep unchanged.</param>
    public void SetHash(string? hash, string? prevHash = null)
    {
        this.Hash = hash;
        this.PrevHash = prevHash;
    }
}
