using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.AzureStorage.Configuration;

/// <summary>
/// Represents configuration settings for Append Blob Storage-backed event streams and related stores.
/// </summary>
public record EventStreamAppendBlobSettings
{
    /// <summary>
    /// Gets the default data store key used for event streams (e.g., "appendblob").
    /// </summary>
    [Required]
    public string DefaultDataStore { get; init; }

    /// <summary>
    /// Gets the default document store key used for object documents.
    /// </summary>
    [Required]
    public string DefaultDocumentStore { get; init; }

    /// <summary>
    /// Gets the default snapshot store key used for snapshots.
    /// </summary>
    [Required]
    public string DefaultSnapShotStore { get; init; }

    /// <summary>
    /// Gets the default tag store key used for document and stream tags.
    /// </summary>
    [Required]
    public string DefaultDocumentTagStore { get; init; }

    /// <summary>
    /// Gets a value indicating whether containers are automatically created when missing.
    /// </summary>
    public bool AutoCreateContainer { get; init; }

    /// <summary>
    /// Gets a value indicating whether event stream chunking is enabled.
    /// </summary>
    public bool EnableStreamChunks { get; init; }

    /// <summary>
    /// Gets the default number of events per chunk when chunking is enabled.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Chunk size must be at least 1")]
    public int DefaultChunkSize { get; init; }

    /// <summary>
    /// Gets the default container name used to store materialized object documents.
    /// </summary>
    [Required]
    public string DefaultDocumentContainerName { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamAppendBlobSettings"/> record.
    /// </summary>
    /// <param name="defaultDataStore">The default data store key used for event streams.</param>
    /// <param name="defaultDocumentStore">The default document store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="defaultSnapShotStore">The default snapshot store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="defaultDocumentTagStore">The default document tag store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="autoCreateContainer">True to create containers automatically when missing.</param>
    /// <param name="enableStreamChunks">True to enable chunked event streams.</param>
    /// <param name="defaultChunkSize">The default number of events per chunk.</param>
    /// <param name="defaultDocumentContainerName">The default container name used to store object documents.</param>
    public EventStreamAppendBlobSettings(
        string defaultDataStore,
        string defaultDocumentStore = null!,
        string defaultSnapShotStore = null!,
        string defaultDocumentTagStore = null!,
        bool autoCreateContainer = true,
        bool enableStreamChunks = false,
        int defaultChunkSize = 1000,
        string defaultDocumentContainerName = "object-document-store")
    {
        ArgumentNullException.ThrowIfNull(defaultDataStore);

        DefaultDataStore = defaultDataStore;
        DefaultDocumentStore = defaultDocumentStore ?? DefaultDataStore;
        DefaultSnapShotStore = defaultSnapShotStore ?? DefaultDataStore;
        DefaultDocumentTagStore = defaultDocumentTagStore ?? DefaultDataStore;
        DefaultDocumentContainerName = defaultDocumentContainerName;
        AutoCreateContainer = autoCreateContainer;
        EnableStreamChunks = enableStreamChunks;
        DefaultChunkSize = defaultChunkSize;
    }
}
