using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Represents the materialized document of an object whose state is derived from an event stream.
/// </summary>
/// <remarks>
/// The document tracks the active stream information, terminated streams, optional schema version,
/// and content hashes used for optimistic concurrency when persisted.
/// </remarks>
public interface IObjectDocument
{
    /// <summary>
    /// Gets metadata about the active stream used to read and append events for this document.
    /// </summary>
    StreamInformation Active { get; }

    /// <summary>
    /// Gets the identifier of the object.
    /// </summary>
    string ObjectId { get; }

    /// <summary>
    /// Gets the logical name/type of the object.
    /// </summary>
    string ObjectName { get; }

    /// <summary>
    /// Gets the list of streams that have been terminated for this object.
    /// </summary>
    List<TerminatedStream> TerminatedStreams { get; }

    /// <summary>
    /// Gets the schema version of the materialized document format; may be null.
    /// </summary>
    string? SchemaVersion { get; }

    /// <summary>
    /// Gets the current hash of the materialized document content used for optimistic concurrency; may be null.
    /// </summary>
    [JsonIgnore]
    string? Hash { get; }

    /// <summary>
    /// Gets the previous hash value of the materialized document used for optimistic concurrency; may be null.
    /// </summary>
    [JsonIgnore]
    string? PrevHash { get; }

    /// <summary>
    /// Sets the current and optionally previous hash values for optimistic concurrency checks.
    /// </summary>
    /// <param name="hash">The new content hash value; may be null.</param>
    /// <param name="prevHash">The previous content hash value; null to keep unchanged.</param>
    void SetHash(string? hash, string? prevHash = null);
}

/// <summary>
/// Extends <see cref="IObjectDocument"/> with operations that require access to factories or stores.
/// </summary>
public interface IObjectDocumentWithMethods : IObjectDocument
{
    /// <summary>
    /// Associates a tag with this document using the configured tag store.
    /// </summary>
    /// <param name="tag">The tag value to associate.</param>
    /// <param name="tagType">The tag type determining which tag store is used; default is <see cref="TagTypes.DocumentTag"/>.</param>
    public Task SetTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag);

    /// <summary>
    /// Removes a tag from this document using the configured tag store.
    /// </summary>
    /// <param name="tag">The tag value to remove.</param>
    /// <param name="tagType">The tag type determining which tag store is used; default is <see cref="TagTypes.DocumentTag"/>.</param>
    public Task RemoveTagAsync(string tag, TagTypes tagType = TagTypes.DocumentTag);
}

/// <summary>
/// Specifies the category of tag when associating tags with documents and streams.
/// </summary>
[Flags]
public enum TagTypes
{
    /// <summary>
    /// A tag that is associated with an object document.
    /// </summary>
    DocumentTag = 1,
    /// <summary>
    /// A tag that is associated with an event stream.
    /// </summary>
    StreamTag = 2,
}
