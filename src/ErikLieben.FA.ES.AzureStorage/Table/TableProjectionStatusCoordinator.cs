using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// AOT-compatible JSON serializer context for projection status serialization.
/// </summary>
[JsonSerializable(typeof(RebuildInfo))]
[JsonSerializable(typeof(RebuildToken))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal partial class ProjectionStatusJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Azure Table Storage entity representing a projection's status.
/// </summary>
/// <remarks>
/// PartitionKey: {ProjectionName}
/// RowKey: {ObjectId}
/// </remarks>
public class ProjectionStatusEntity : ITableEntity
{
    /// <summary>
    /// Gets or sets the partition key (ProjectionName).
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the row key (ObjectId).
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the entity.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the ETag for optimistic concurrency.
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Gets or sets the projection status as an integer.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets when the status was last changed.
    /// </summary>
    public DateTimeOffset? StatusChangedAt { get; set; }

    /// <summary>
    /// Gets or sets the stored schema version.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the serialized rebuild token (JSON).
    /// </summary>
    public string? RebuildToken { get; set; }

    /// <summary>
    /// Gets or sets the rebuild strategy as an integer.
    /// </summary>
    public int? RebuildStrategy { get; set; }

    /// <summary>
    /// Gets or sets when the rebuild started.
    /// </summary>
    public DateTimeOffset? RebuildStartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the rebuild token expires.
    /// </summary>
    public DateTimeOffset? RebuildExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the error message, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the serialized rebuild info (JSON).
    /// </summary>
    public string? RebuildInfoJson { get; set; }
}

/// <summary>
/// Azure Table Storage implementation of <see cref="IProjectionStatusCoordinator"/>.
/// Provides durable, distributed projection status coordination backed by Azure Table Storage.
/// </summary>
public class TableProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly string _tableName;
    private readonly ILogger<TableProjectionStatusCoordinator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjectionStatusCoordinator"/> class.
    /// </summary>
    /// <param name="tableServiceClient">The Azure Table Service client.</param>
    /// <param name="tableName">The table name to use. Defaults to "ProjectionStatus".</param>
    /// <param name="logger">Optional logger.</param>
    public TableProjectionStatusCoordinator(
        TableServiceClient tableServiceClient,
        string tableName = "ProjectionStatus",
        ILogger<TableProjectionStatusCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(tableServiceClient);

        _tableServiceClient = tableServiceClient;
        _tableName = tableName;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RebuildToken> StartRebuildAsync(
        string projectionName,
        string objectId,
        RebuildStrategy strategy,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectionName);
        ArgumentNullException.ThrowIfNull(objectId);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var token = RebuildToken.Create(projectionName, objectId, strategy, timeout);
        var rebuildInfo = RebuildInfo.Start(strategy);

        var entity = new ProjectionStatusEntity
        {
            PartitionKey = projectionName,
            RowKey = objectId,
            Status = (int)ProjectionStatus.Rebuilding,
            StatusChangedAt = DateTimeOffset.UtcNow,
            SchemaVersion = 0,
            RebuildToken = JsonSerializer.Serialize(token, ProjectionStatusJsonContext.Default.RebuildToken),
            RebuildStrategy = (int)strategy,
            RebuildStartedAt = token.StartedAt,
            RebuildExpiresAt = token.ExpiresAt,
            RebuildInfoJson = JsonSerializer.Serialize(rebuildInfo, ProjectionStatusJsonContext.Default.RebuildInfo)
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
                projectionName, objectId, strategy, token.ExpiresAt);
        }

        return token;
    }

    /// <inheritdoc />
    public async Task StartCatchUpAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, token.ProjectionName, token.ObjectId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"No status entry found for {token.ProjectionName}:{token.ObjectId}");
        }

        ValidateToken(token, entity);

        var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);
        entity.Status = (int)ProjectionStatus.CatchingUp;
        entity.StatusChangedAt = DateTimeOffset.UtcNow;
        entity.RebuildInfoJson = rebuildInfo is not null
            ? JsonSerializer.Serialize(rebuildInfo.WithProgress(), ProjectionStatusJsonContext.Default.RebuildInfo)
            : null;

        await UpdateEntityAsync(tableClient, entity, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Started catch-up for {ProjectionName}:{ObjectId}",
                token.ProjectionName, token.ObjectId);
        }
    }

    /// <inheritdoc />
    public async Task MarkReadyAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, token.ProjectionName, token.ObjectId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"No status entry found for {token.ProjectionName}:{token.ObjectId}");
        }

        ValidateToken(token, entity);

        var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);
        entity.Status = (int)ProjectionStatus.Ready;
        entity.StatusChangedAt = DateTimeOffset.UtcNow;
        entity.RebuildInfoJson = rebuildInfo is not null
            ? JsonSerializer.Serialize(rebuildInfo.WithCompletion(), ProjectionStatusJsonContext.Default.RebuildInfo)
            : null;

        await UpdateEntityAsync(tableClient, entity, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Marked {ProjectionName}:{ObjectId} as ready",
                token.ProjectionName, token.ObjectId);
        }
    }

    /// <inheritdoc />
    public async Task CompleteRebuildAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, token.ProjectionName, token.ObjectId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"No status entry found for {token.ProjectionName}:{token.ObjectId}");
        }

        ValidateToken(token, entity);

        var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);
        entity.Status = (int)ProjectionStatus.Active;
        entity.StatusChangedAt = DateTimeOffset.UtcNow;
        entity.RebuildToken = null;
        entity.RebuildStrategy = null;
        entity.RebuildStartedAt = null;
        entity.RebuildExpiresAt = null;
        entity.RebuildInfoJson = rebuildInfo is not null
            ? JsonSerializer.Serialize(rebuildInfo.WithCompletion(), ProjectionStatusJsonContext.Default.RebuildInfo)
            : null;

        await UpdateEntityAsync(tableClient, entity, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Completed rebuild for {ProjectionName}:{ObjectId}",
                token.ProjectionName, token.ObjectId);
        }
    }

    /// <inheritdoc />
    public async Task CancelRebuildAsync(
        RebuildToken token,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, token.ProjectionName, token.ObjectId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"No status entry found for {token.ProjectionName}:{token.ObjectId}");
        }

        var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);
        var newStatus = error is not null ? ProjectionStatus.Failed : ProjectionStatus.Active;
        var updatedRebuildInfo = error is not null
            ? rebuildInfo?.WithError(error)
            : rebuildInfo?.WithCompletion();

        entity.Status = (int)newStatus;
        entity.StatusChangedAt = DateTimeOffset.UtcNow;
        entity.Error = error;
        entity.RebuildToken = null;
        entity.RebuildStrategy = null;
        entity.RebuildStartedAt = null;
        entity.RebuildExpiresAt = null;
        entity.RebuildInfoJson = updatedRebuildInfo is not null
            ? JsonSerializer.Serialize(updatedRebuildInfo, ProjectionStatusJsonContext.Default.RebuildInfo)
            : null;

        await UpdateEntityAsync(tableClient, entity, cancellationToken);

        if (_logger?.IsEnabled(LogLevel.Warning) == true)
        {
            _logger.LogWarning(
                "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
                token.ProjectionName, token.ObjectId, error ?? "none");
        }
    }

    /// <inheritdoc />
    public async Task<ProjectionStatusInfo?> GetStatusAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, projectionName, objectId, cancellationToken);

        return entity is not null ? ToProjectionStatusInfo(entity) : null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(
        ProjectionStatus status,
        CancellationToken cancellationToken = default)
    {
        var tableClient = await GetTableClientAsync(cancellationToken);
        var statusValue = (int)status;
        var filter = $"Status eq {statusValue}";

        var results = new List<ProjectionStatusInfo>();
        await foreach (var entity in tableClient.QueryAsync<ProjectionStatusEntity>(
            filter: filter, cancellationToken: cancellationToken))
        {
            results.Add(ToProjectionStatusInfo(entity));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        var tableClient = await GetTableClientAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var recovered = 0;

        // Query for entities in Rebuilding or CatchingUp status
        var rebuildingFilter = $"Status eq {(int)ProjectionStatus.Rebuilding}";
        var catchingUpFilter = $"Status eq {(int)ProjectionStatus.CatchingUp}";

        var entities = new List<ProjectionStatusEntity>();
        await foreach (var entity in tableClient.QueryAsync<ProjectionStatusEntity>(
            filter: rebuildingFilter, cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        await foreach (var entity in tableClient.QueryAsync<ProjectionStatusEntity>(
            filter: catchingUpFilter, cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        foreach (var entity in entities)
        {
            if (entity.RebuildExpiresAt.HasValue && entity.RebuildExpiresAt.Value <= now)
            {
                var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);

                entity.Status = (int)ProjectionStatus.Failed;
                entity.StatusChangedAt = now;
                entity.Error = "Rebuild timed out";
                entity.RebuildToken = null;
                entity.RebuildStrategy = null;
                entity.RebuildStartedAt = null;
                entity.RebuildExpiresAt = null;
                entity.RebuildInfoJson = rebuildInfo is not null
                    ? JsonSerializer.Serialize(rebuildInfo.WithError("Rebuild timed out"), ProjectionStatusJsonContext.Default.RebuildInfo)
                    : null;

                try
                {
                    await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
                    recovered++;

                    if (_logger?.IsEnabled(LogLevel.Warning) == true)
                    {
                        _logger.LogWarning(
                            "Recovered stuck rebuild for {ProjectionName}:{ObjectId}",
                            entity.PartitionKey, entity.RowKey);
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 412)
                {
                    // Concurrency conflict - another process already recovered this entity
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        _logger.LogDebug(
                            ex,
                            "Skipped recovery for {ProjectionName}:{ObjectId} due to concurrency conflict",
                            entity.PartitionKey, entity.RowKey);
                    }
                }
            }
        }

        return recovered;
    }

    /// <inheritdoc />
    public async Task DisableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, projectionName, objectId, cancellationToken);

        if (entity is not null)
        {
            entity.Status = (int)ProjectionStatus.Disabled;
            entity.StatusChangedAt = DateTimeOffset.UtcNow;
            await UpdateEntityAsync(tableClient, entity, cancellationToken);
        }
        else
        {
            var newEntity = new ProjectionStatusEntity
            {
                PartitionKey = projectionName,
                RowKey = objectId,
                Status = (int)ProjectionStatus.Disabled,
                StatusChangedAt = DateTimeOffset.UtcNow,
                SchemaVersion = 0
            };
            await tableClient.AddEntityAsync(newEntity, cancellationToken);
        }

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(
                "Disabled projection {ProjectionName}:{ObjectId}",
                projectionName, objectId);
        }
    }

    /// <inheritdoc />
    public async Task EnableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var tableClient = await GetTableClientAsync(cancellationToken);
        var entity = await GetEntityAsync(tableClient, projectionName, objectId, cancellationToken);

        if (entity is not null)
        {
            entity.Status = (int)ProjectionStatus.Active;
            entity.StatusChangedAt = DateTimeOffset.UtcNow;
            await UpdateEntityAsync(tableClient, entity, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Enabled projection {ProjectionName}:{ObjectId}",
                    projectionName, objectId);
            }
        }
    }

    private async Task<TableClient> GetTableClientAsync(CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        return tableClient;
    }

    private static async Task<ProjectionStatusEntity?> GetEntityAsync(
        TableClient tableClient,
        string projectionName,
        string objectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await tableClient.GetEntityIfExistsAsync<ProjectionStatusEntity>(
                projectionName, objectId, cancellationToken: cancellationToken);
            return response.HasValue ? response.Value : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static async Task UpdateEntityAsync(
        TableClient tableClient,
        ProjectionStatusEntity entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            throw new InvalidOperationException(
                $"Concurrency conflict when updating projection status for {entity.PartitionKey}:{entity.RowKey}. " +
                "The entity was modified by another process.", ex);
        }
    }

    private static void ValidateToken(RebuildToken token, ProjectionStatusEntity entity)
    {
        var storedToken = DeserializeRebuildToken(entity.RebuildToken);

        if (storedToken is null || storedToken.Token != token.Token)
        {
            throw new InvalidOperationException(
                $"Invalid or expired rebuild token for {token.ProjectionName}:{token.ObjectId}");
        }

        if (token.IsExpired)
        {
            throw new InvalidOperationException(
                $"Rebuild token for {token.ProjectionName}:{token.ObjectId} has expired");
        }
    }

    private static ProjectionStatusInfo ToProjectionStatusInfo(ProjectionStatusEntity entity)
    {
        var rebuildInfo = DeserializeRebuildInfo(entity.RebuildInfoJson);

        return new ProjectionStatusInfo(
            entity.PartitionKey,
            entity.RowKey,
            (ProjectionStatus)entity.Status,
            entity.StatusChangedAt,
            entity.SchemaVersion,
            rebuildInfo);
    }

    private static RebuildInfo? DeserializeRebuildInfo(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize(json, ProjectionStatusJsonContext.Default.RebuildInfo);
    }

    private static RebuildToken? DeserializeRebuildToken(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize(json, ProjectionStatusJsonContext.Default.RebuildToken);
    }
}
