using Azure.Data.Tables;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Factory for creating and managing projections stored in Azure Table Storage.
/// Supports chunked checkpoint storage with historical retention.
/// </summary>
/// <typeparam name="T">The projection type that inherits from <see cref="TableProjection"/>.</typeparam>
public abstract class TableProjectionFactory<T> : IProjectionFactory<T>, IProjectionFactory where T : TableProjection
{
    /// <summary>
    /// Maximum size for each checkpoint chunk (60KB to leave room for other properties).
    /// </summary>
    private const int MaxChunkSizeBytes = 60 * 1024;

    /// <summary>
    /// Partition key for all checkpoint data.
    /// </summary>
    private const string CheckpointPartitionKey = "checkpoint";

    /// <summary>
    /// Suffix for the current checkpoint pointer row.
    /// </summary>
    private const string CurrentPointerSuffix = "_current";

    /// <summary>
    /// Legacy partition key for projection data (backwards compatibility).
    /// </summary>
    private const string LegacyProjectionPartitionKey = "projection";

    private readonly IAzureClientFactory<TableServiceClient> _tableServiceClientFactory;
    private readonly string _connectionName;
    private readonly string _tableName;
    private readonly bool _autoCreateTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjectionFactory{T}"/> class.
    /// </summary>
    /// <param name="tableServiceClientFactory">The factory for creating Azure Table Service clients.</param>
    /// <param name="connectionName">The name of the Azure client connection.</param>
    /// <param name="tableName">The name of the table where projection data is stored.</param>
    /// <param name="autoCreateTable">Whether to create the table automatically if it doesn't exist.</param>
    protected TableProjectionFactory(
        IAzureClientFactory<TableServiceClient> tableServiceClientFactory,
        string connectionName,
        string tableName,
        bool autoCreateTable = true)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClientFactory);
        ArgumentNullException.ThrowIfNull(connectionName);
        ArgumentNullException.ThrowIfNull(tableName);

        _tableServiceClientFactory = tableServiceClientFactory;
        _connectionName = connectionName;
        _tableName = tableName;
        _autoCreateTable = autoCreateTable;
    }

    /// <summary>
    /// Creates a new instance of the projection.
    /// </summary>
    /// <returns>A new projection instance.</returns>
    protected abstract T New();

    /// <summary>
    /// Loads a projection from JSON using the generated LoadFromJson method.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <returns>The loaded projection instance, or null if deserialization fails.</returns>
    protected abstract T? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory);

    /// <summary>
    /// Gets the table service client for the configured connection.
    /// </summary>
    /// <returns>The <see cref="TableServiceClient"/>.</returns>
    protected TableServiceClient GetTableServiceClient()
    {
        return _tableServiceClientFactory.CreateClient(_connectionName);
    }

    /// <summary>
    /// Gets the table client for the configured table.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The table client.</returns>
    protected async Task<TableClient> GetTableClientAsync(CancellationToken cancellationToken = default)
    {
        var tableServiceClient = GetTableServiceClient();
        var tableClient = tableServiceClient.GetTableClient(_tableName);

        if (_autoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync(cancellationToken);
        }

        return tableClient;
    }

    /// <summary>
    /// Gets the table client for the checkpoint metadata table.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint table client.</returns>
    protected async Task<TableClient> GetCheckpointTableClientAsync(CancellationToken cancellationToken = default)
    {
        var tableServiceClient = GetTableServiceClient();
        var checkpointTableName = $"{_tableName}checkpoints";
        var tableClient = tableServiceClient.GetTableClient(checkpointTableName);

        if (_autoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync(cancellationToken);
        }

        return tableClient;
    }

    /// <inheritdoc />
    public virtual async Task<T> GetOrCreateAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        // For table projections, blobName is used as the checkpoint identifier
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        // Try to load using new chunked format first
        var json = await TryLoadChunkedCheckpointAsync(checkpointTableClient, projectionName, cancellationToken);

        // If not found, try backwards compatibility with old format
        if (json == null)
        {
            json = await TryLoadLegacyCheckpointAsync(checkpointTableClient, projectionName, cancellationToken);
        }

        if (!string.IsNullOrEmpty(json))
        {
            var projection = LoadFromJson(json, documentFactory, eventStreamFactory);
            if (projection != null)
            {
                return projection;
            }
        }

        return New();
    }

    /// <summary>
    /// Tries to load a checkpoint using the new chunked format.
    /// </summary>
    private async Task<string?> TryLoadChunkedCheckpointAsync(
        TableClient tableClient,
        string projectionName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the current pointer to find the active fingerprint
            var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";
            var pointerResponse = await tableClient.GetEntityAsync<TableEntity>(
                CheckpointPartitionKey,
                pointerRowKey,
                cancellationToken: cancellationToken);

            if (pointerResponse?.Value == null)
            {
                return null;
            }

            var fingerprint = pointerResponse.Value.GetString("Fingerprint");
            if (string.IsNullOrEmpty(fingerprint))
            {
                return null;
            }

            // Load all chunks for this fingerprint
            return await LoadCheckpointByFingerprintAsync(tableClient, fingerprint, cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Loads checkpoint data by fingerprint, reassembling chunks if necessary.
    /// </summary>
    /// <param name="tableClient">The table client.</param>
    /// <param name="fingerprint">The checkpoint fingerprint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decompressed JSON, or null if not found.</returns>
    public async Task<string?> LoadCheckpointByFingerprintAsync(
        TableClient tableClient,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        // Query all chunks for this fingerprint using range query
        var chunks = new List<(int Index, byte[] Data)>();
        var filterStart = $"{fingerprint}_";
        var filterEnd = $"{fingerprint}`"; // '`' is the next ASCII character after '_'

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{CheckpointPartitionKey}' and RowKey ge '{filterStart}' and RowKey lt '{filterEnd}'",
            cancellationToken: cancellationToken))
        {
            // Extract chunk index from RowKey: "{fingerprint}_{index}"
            var rowKey = entity.RowKey;
            var indexPart = rowKey.Substring(fingerprint.Length + 1);
            if (int.TryParse(indexPart, out var chunkIndex)
                && entity.TryGetValue("Data", out var dataValue) && dataValue is byte[] data)
            {
                chunks.Add((chunkIndex, data));
            }
        }

        if (chunks.Count == 0)
        {
            return null;
        }

        // Sort by chunk index and concatenate
        chunks.Sort((a, b) => a.Index.CompareTo(b.Index));
        var totalSize = chunks.Sum(c => c.Data.Length);
        var combinedData = new byte[totalSize];
        var offset = 0;
        foreach (var (_, data) in chunks)
        {
            Array.Copy(data, 0, combinedData, offset, data.Length);
            offset += data.Length;
        }

        // Decompress
        return DecompressJson(combinedData);
    }

    /// <summary>
    /// Tries to load a checkpoint using the legacy single-row format (backwards compatibility).
    /// </summary>
    private static async Task<string?> TryLoadLegacyCheckpointAsync(
        TableClient tableClient,
        string checkpointId,
        CancellationToken cancellationToken)
    {
        TableEntity? entity;
        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(
                LegacyProjectionPartitionKey,
                checkpointId,
                cancellationToken: cancellationToken);

            entity = response?.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        if (entity == null)
        {
            return null;
        }

        return ExtractLegacyCheckpointJson(entity);
    }

    private static string? ExtractLegacyCheckpointJson(TableEntity entity)
    {
        if (IsCompressedLegacyCheckpoint(entity))
        {
            if (entity.TryGetValue("CheckpointData", out var dataValue) && dataValue is byte[] compressedData)
            {
                return DecompressJson(compressedData);
            }

            return null;
        }

        return entity.GetString("CheckpointJson");
    }

    private static bool IsCompressedLegacyCheckpoint(TableEntity entity)
    {
        return entity.TryGetValue("IsCompressed", out var isCompressed)
            && isCompressed is bool compressed
            && compressed;
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(
        T projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var pendingOperations = projection.GetPendingOperations();

        if (pendingOperations.Count == 0)
        {
            await SaveCheckpointAsync(projection, blobName, cancellationToken);
            return;
        }

        // Group operations by partition key for transactional batching
        var groupedOperations = pendingOperations.GroupBy(op => op.PartitionKey);

        foreach (var group in groupedOperations)
        {
            var batch = new List<TableTransactionAction>();

            foreach (var operation in group)
            {
                switch (operation.Type)
                {
                    case TableOperationType.Upsert:
                        if (operation.Entity != null)
                        {
                            batch.Add(new TableTransactionAction(
                                TableTransactionActionType.UpsertReplace,
                                operation.Entity));
                        }
                        break;

                    case TableOperationType.Delete:
                        batch.Add(new TableTransactionAction(
                            TableTransactionActionType.Delete,
                            new TableEntity(operation.PartitionKey, operation.RowKey) { ETag = Azure.ETag.All }));
                        break;
                }
            }

            // Azure Table Storage allows max 100 operations per batch
            foreach (var chunk in batch.Chunk(100))
            {
                await tableClient.SubmitTransactionAsync(chunk, cancellationToken);
            }
        }

        projection.ClearPendingOperations();

        // Save checkpoint metadata
        await SaveCheckpointAsync(projection, blobName, cancellationToken);
    }

    /// <summary>
    /// Saves the checkpoint metadata for the projection using chunked storage with historical retention.
    /// </summary>
    /// <param name="projection">The projection to save checkpoint for.</param>
    /// <param name="checkpointId">Optional checkpoint identifier (projection name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task SaveCheckpointAsync(
        T projection,
        string? checkpointId = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = checkpointId ?? typeof(T).Name;
        var fingerprint = projection.CheckpointFingerprint;

        if (string.IsNullOrEmpty(fingerprint))
        {
            // No checkpoint data to save
            return;
        }

        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        // Serialize and compress the checkpoint
        var json = projection.ToJson();
        var compressedData = CompressJson(json);

        // Split into chunks
        var chunks = ChunkData(compressedData);
        var now = DateTimeOffset.UtcNow;

        // Save all chunks (historical - never deleted)
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkEntity = new TableEntity(CheckpointPartitionKey, $"{fingerprint}_{i}")
            {
                ["Data"] = chunks[i],
                ["TotalChunks"] = chunks.Count,
                ["ChunkIndex"] = i,
                ["CreatedAt"] = now,
                ["ProjectionName"] = projectionName
            };

            await checkpointTableClient.UpsertEntityAsync(chunkEntity, TableUpdateMode.Replace, cancellationToken);
        }

        // Update the current pointer
        var pointerEntity = new TableEntity(CheckpointPartitionKey, $"{projectionName}{CurrentPointerSuffix}")
        {
            ["Fingerprint"] = fingerprint,
            ["LastUpdated"] = now
        };

        await checkpointTableClient.UpsertEntityAsync(pointerEntity, TableUpdateMode.Replace, cancellationToken);
    }

    /// <summary>
    /// Splits compressed data into chunks that fit within Azure Table Storage property limits.
    /// </summary>
    /// <param name="compressedData">The compressed data to chunk.</param>
    /// <returns>A list of byte array chunks.</returns>
    private static List<byte[]> ChunkData(byte[] compressedData)
    {
        var chunks = new List<byte[]>();
        var offset = 0;

        while (offset < compressedData.Length)
        {
            var chunkSize = Math.Min(MaxChunkSizeBytes, compressedData.Length - offset);
            var chunk = new byte[chunkSize];
            Array.Copy(compressedData, offset, chunk, 0, chunkSize);
            chunks.Add(chunk);
            offset += chunkSize;
        }

        return chunks;
    }

    /// <summary>
    /// Compresses a JSON string using GZip compression.
    /// </summary>
    /// <param name="json">The JSON string to compress.</param>
    /// <returns>The compressed data as a byte array.</returns>
    private static byte[] CompressJson(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses GZip compressed data to a JSON string.
    /// </summary>
    /// <param name="compressedData">The compressed data.</param>
    /// <returns>The decompressed JSON string.</returns>
    private static string DecompressJson(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        try
        {
            // Try new format first (pointer row)
            var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                CheckpointPartitionKey,
                pointerRowKey,
                cancellationToken: cancellationToken);

            if (response?.Value != null)
            {
                return true;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Try legacy format
        }

        try
        {
            // Try legacy format
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                LegacyProjectionPartitionKey,
                projectionName,
                cancellationToken: cancellationToken);
            return response?.Value != null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public virtual async Task<DateTimeOffset?> GetLastModifiedAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        try
        {
            // Try new format first (pointer row)
            var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                CheckpointPartitionKey,
                pointerRowKey,
                cancellationToken: cancellationToken);

            if (response?.Value != null && response.Value.TryGetValue("LastUpdated", out var lastUpdated))
            {
                return (DateTimeOffset?)lastUpdated;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Try legacy format
        }

        try
        {
            // Try legacy format
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                LegacyProjectionPartitionKey,
                projectionName,
                cancellationToken: cancellationToken);

            if (response?.Value != null && response.Value.TryGetValue("LastUpdated", out var lastUpdated))
            {
                return (DateTimeOffset?)lastUpdated;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Doesn't exist
        }

        return null;
    }

    /// <summary>
    /// Gets a list of all historical checkpoint fingerprints for this projection.
    /// </summary>
    /// <param name="blobName">Optional projection name override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of historical checkpoint information.</returns>
    public virtual async Task<IReadOnlyList<HistoricalCheckpointInfo>> GetHistoricalCheckpointsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);
        var results = new List<HistoricalCheckpointInfo>();

        // Query all chunk rows ending with "_0" (first chunk of each fingerprint) for this projection
        await foreach (var entity in checkpointTableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{CheckpointPartitionKey}' and ChunkIndex eq 0 and ProjectionName eq '{projectionName}'",
            cancellationToken: cancellationToken))
        {
            var rowKey = entity.RowKey;
            // Extract fingerprint from RowKey: "{fingerprint}_0"
            var fingerprint = rowKey.Substring(0, rowKey.Length - 2); // Remove "_0" suffix

            DateTimeOffset? createdAt = null;
            if (entity.TryGetValue("CreatedAt", out var createdAtValue))
            {
                createdAt = (DateTimeOffset?)createdAtValue;
            }

            var totalChunks = 1;
            if (entity.TryGetValue("TotalChunks", out var chunksValue) && chunksValue is int chunks)
            {
                totalChunks = chunks;
            }

            results.Add(new HistoricalCheckpointInfo(fingerprint, createdAt, totalChunks));
        }

        // Sort by creation date descending (newest first)
        results.Sort((a, b) => (b.CreatedAt ?? DateTimeOffset.MinValue).CompareTo(a.CreatedAt ?? DateTimeOffset.MinValue));

        return results;
    }

    /// <summary>
    /// Deletes the current checkpoint pointer. Historical checkpoint data is retained.
    /// </summary>
    /// <param name="blobName">Optional checkpoint identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// This only deletes the pointer to the current checkpoint. Historical checkpoint
    /// data (chunk rows) are retained for audit purposes. To delete all historical data,
    /// use <see cref="DeleteAllHistoricalCheckpointsAsync"/>.
    /// </remarks>
    public virtual async Task DeleteAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        // Delete new format pointer
        try
        {
            var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";
            await checkpointTableClient.DeleteEntityAsync(
                CheckpointPartitionKey,
                pointerRowKey,
                Azure.ETag.All,
                cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Pointer doesn't exist
        }

        // Also try to delete legacy format for backwards compatibility
        try
        {
            await checkpointTableClient.DeleteEntityAsync(
                LegacyProjectionPartitionKey,
                projectionName,
                Azure.ETag.All,
                cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Legacy checkpoint doesn't exist
        }
    }

    /// <summary>
    /// Deletes all historical checkpoint data for this projection.
    /// </summary>
    /// <param name="blobName">Optional projection name override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// WARNING: This permanently deletes all historical checkpoint data. This cannot be undone.
    /// </remarks>
    public virtual async Task DeleteAllHistoricalCheckpointsAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        // Get all historical checkpoints
        var historicalCheckpoints = await GetHistoricalCheckpointsAsync(projectionName, cancellationToken);

        // Delete all chunks for each fingerprint
        foreach (var checkpoint in historicalCheckpoints)
        {
            for (var i = 0; i < checkpoint.TotalChunks; i++)
            {
                try
                {
                    await checkpointTableClient.DeleteEntityAsync(
                        CheckpointPartitionKey,
                        $"{checkpoint.Fingerprint}_{i}",
                        Azure.ETag.All,
                        cancellationToken);
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    // Already deleted
                }
            }
        }

        // Delete the pointer
        await DeleteAsync(projectionName, cancellationToken);
    }

    /// <summary>
    /// Loads a projection from a specific historical checkpoint fingerprint.
    /// </summary>
    /// <param name="documentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="fingerprint">The checkpoint fingerprint to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded projection, or null if the fingerprint was not found.</returns>
    public virtual async Task<T?> LoadFromHistoricalCheckpointAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);
        var json = await LoadCheckpointByFingerprintAsync(checkpointTableClient, fingerprint, cancellationToken);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return LoadFromJson(json, documentFactory, eventStreamFactory);
    }

    /// <inheritdoc />
    public Type ProjectionType => typeof(T);

    /// <inheritdoc />
    public async Task<Projection> GetOrCreateProjectionAsync(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        return await GetOrCreateAsync(documentFactory, eventStreamFactory, blobName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveProjectionAsync(
        Projection projection,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        if (projection is not T typedProjection)
        {
            throw new ArgumentException($"Projection must be of type {typeof(T).Name}", nameof(projection));
        }

        await SaveAsync(typedProjection, blobName, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task SetStatusAsync(
        ProjectionStatus status,
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";

        try
        {
            // Try to get existing pointer row
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                CheckpointPartitionKey,
                pointerRowKey,
                cancellationToken: cancellationToken);

            if (response?.Value != null)
            {
                // Update existing row with new status
                var entity = response.Value;
                entity["Status"] = (int)status;
                entity["StatusUpdatedAt"] = DateTimeOffset.UtcNow;

                await checkpointTableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                return;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Create new pointer row with status
        }

        // Create new pointer row with just the status
        var newEntity = new TableEntity(CheckpointPartitionKey, pointerRowKey)
        {
            ["Status"] = (int)status,
            ["StatusUpdatedAt"] = DateTimeOffset.UtcNow
        };

        await checkpointTableClient.UpsertEntityAsync(newEntity, TableUpdateMode.Replace, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<ProjectionStatus> GetStatusAsync(
        string? blobName = null,
        CancellationToken cancellationToken = default)
    {
        var projectionName = blobName ?? typeof(T).Name;
        var checkpointTableClient = await GetCheckpointTableClientAsync(cancellationToken);

        var pointerRowKey = $"{projectionName}{CurrentPointerSuffix}";

        try
        {
            var response = await checkpointTableClient.GetEntityAsync<TableEntity>(
                CheckpointPartitionKey,
                pointerRowKey,
                cancellationToken: cancellationToken);

            if (response?.Value != null && response.Value.TryGetValue("Status", out var statusValue)
                && statusValue is int statusInt)
            {
                return (ProjectionStatus)statusInt;
            }
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Pointer doesn't exist
        }

        return ProjectionStatus.Active;
    }
}

/// <summary>
/// Information about a historical checkpoint.
/// </summary>
/// <param name="Fingerprint">The SHA-256 fingerprint of the checkpoint state.</param>
/// <param name="CreatedAt">When the checkpoint was created.</param>
/// <param name="TotalChunks">The number of chunks used to store this checkpoint.</param>
public record HistoricalCheckpointInfo(
    string Fingerprint,
    DateTimeOffset? CreatedAt,
    int TotalChunks);
