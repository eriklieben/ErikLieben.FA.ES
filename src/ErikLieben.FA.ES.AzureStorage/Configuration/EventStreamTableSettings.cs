#pragma warning disable S107 // Methods should not have too many parameters - settings record requires all configuration properties

namespace ErikLieben.FA.ES.AzureStorage.Configuration;

/// <summary>
/// Represents configuration settings for Table Storage-backed event streams and related stores.
/// </summary>
public record EventStreamTableSettings
{
    /// <summary>
    /// Gets the default data store key used for event streams (e.g., "table").
    /// </summary>
    public string DefaultDataStore { get; init; }

    /// <summary>
    /// Gets the default document store key used for object documents.
    /// </summary>
    public string DefaultDocumentStore { get; init; }

    /// <summary>
    /// Gets the default snapshot store key used for snapshots.
    /// </summary>
    public string DefaultSnapShotStore { get; init; }

    /// <summary>
    /// Gets the default tag store key used for document and stream tags.
    /// </summary>
    public string DefaultDocumentTagStore { get; init; }

    /// <summary>
    /// Gets a value indicating whether tables are automatically created when missing.
    /// </summary>
    public bool AutoCreateTable { get; init; }

    /// <summary>
    /// Gets a value indicating whether event stream chunking is enabled.
    /// </summary>
    public bool EnableStreamChunks { get; init; }

    /// <summary>
    /// Gets the default number of events per chunk when chunking is enabled.
    /// </summary>
    public int DefaultChunkSize { get; init; }

    /// <summary>
    /// Gets a value indicating whether large event payload chunking is enabled.
    /// When enabled, event payloads exceeding <see cref="PayloadChunkThresholdBytes"/> will be
    /// automatically compressed and split across multiple table rows.
    /// </summary>
    public bool EnableLargePayloadChunking { get; init; }

    /// <summary>
    /// Gets the payload size threshold in bytes that triggers chunking.
    /// Payloads larger than this will be compressed and chunked. Default is 60KB (61440 bytes).
    /// </summary>
    public int PayloadChunkThresholdBytes { get; init; }

    /// <summary>
    /// Gets a value indicating whether to compress large payloads before chunking.
    /// Compression is applied before size checking, so payloads that compress below the threshold
    /// will be stored in a single row. Default is true.
    /// </summary>
    public bool CompressLargePayloads { get; init; }

    /// <summary>
    /// Gets the default table name used to store materialized object documents.
    /// </summary>
    public string DefaultDocumentTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store event streams.
    /// </summary>
    public string DefaultEventTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store snapshots.
    /// </summary>
    public string DefaultSnapshotTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store document tags.
    /// </summary>
    public string DefaultDocumentTagTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store stream tags.
    /// </summary>
    public string DefaultStreamTagTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store stream chunk metadata.
    /// </summary>
    public string DefaultStreamChunkTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store document snapshot metadata.
    /// </summary>
    public string DefaultDocumentSnapShotTableName { get; init; }

    /// <summary>
    /// Gets the default table name used to store terminated stream metadata.
    /// </summary>
    public string DefaultTerminatedStreamTableName { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamTableSettings"/> record.
    /// </summary>
    /// <param name="defaultDataStore">The default data store key used for event streams.</param>
    /// <param name="defaultDocumentStore">The default document store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="defaultSnapShotStore">The default snapshot store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="defaultDocumentTagStore">The default document tag store key; when null, falls back to <paramref name="defaultDataStore"/>.</param>
    /// <param name="autoCreateTable">True to create tables automatically when missing.</param>
    /// <param name="enableStreamChunks">True to enable chunked event streams.</param>
    /// <param name="defaultChunkSize">The default number of events per chunk.</param>
    /// <param name="defaultDocumentTableName">The default table name used to store object documents.</param>
    /// <param name="defaultEventTableName">The default table name used to store event streams.</param>
    /// <param name="defaultSnapshotTableName">The default table name used to store snapshots.</param>
    /// <param name="defaultDocumentTagTableName">The default table name used to store document tags.</param>
    /// <param name="defaultStreamTagTableName">The default table name used to store stream tags.</param>
    /// <param name="defaultStreamChunkTableName">The default table name used to store stream chunk metadata.</param>
    /// <param name="defaultDocumentSnapShotTableName">The default table name used to store document snapshot metadata.</param>
    /// <param name="defaultTerminatedStreamTableName">The default table name used to store terminated stream metadata.</param>
    /// <param name="enableLargePayloadChunking">True to enable automatic chunking of large event payloads.</param>
    /// <param name="payloadChunkThresholdBytes">The payload size threshold in bytes that triggers chunking. Default is 60KB.</param>
    /// <param name="compressLargePayloads">True to compress large payloads before chunking. Default is true.</param>
    public EventStreamTableSettings(
        string defaultDataStore,
        string? defaultDocumentStore = null,
        string? defaultSnapShotStore = null,
        string? defaultDocumentTagStore = null,
        bool autoCreateTable = true,
        bool enableStreamChunks = false,
        int defaultChunkSize = 1000,
        string defaultDocumentTableName = "objectdocumentstore",
        string defaultEventTableName = "eventstream",
        string defaultSnapshotTableName = "snapshots",
        string defaultDocumentTagTableName = "documenttags",
        string defaultStreamTagTableName = "streamtags",
        string defaultStreamChunkTableName = "streamchunks",
        string defaultDocumentSnapShotTableName = "documentsnapshots",
        string defaultTerminatedStreamTableName = "terminatedstreams",
        bool enableLargePayloadChunking = false,
        int payloadChunkThresholdBytes = 60 * 1024,
        bool compressLargePayloads = true)
    {
        ArgumentNullException.ThrowIfNull(defaultDataStore);

        DefaultDataStore = defaultDataStore;
        DefaultDocumentStore = defaultDocumentStore ?? DefaultDataStore;
        DefaultSnapShotStore = defaultSnapShotStore ?? DefaultDataStore;
        DefaultDocumentTagStore = defaultDocumentTagStore ?? DefaultDataStore;
        DefaultDocumentTableName = defaultDocumentTableName;
        DefaultEventTableName = defaultEventTableName;
        DefaultSnapshotTableName = defaultSnapshotTableName;
        DefaultDocumentTagTableName = defaultDocumentTagTableName;
        DefaultStreamTagTableName = defaultStreamTagTableName;
        DefaultStreamChunkTableName = defaultStreamChunkTableName;
        DefaultDocumentSnapShotTableName = defaultDocumentSnapShotTableName;
        DefaultTerminatedStreamTableName = defaultTerminatedStreamTableName;
        AutoCreateTable = autoCreateTable;
        EnableStreamChunks = enableStreamChunks;
        DefaultChunkSize = defaultChunkSize;
        EnableLargePayloadChunking = enableLargePayloadChunking;
        PayloadChunkThresholdBytes = payloadChunkThresholdBytes;
        CompressLargePayloads = compressLargePayloads;
    }
}
