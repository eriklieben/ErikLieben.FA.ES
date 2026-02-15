using System.Net;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// CosmosDB-backed implementation of <see cref="IProjectionStatusCoordinator"/>.
/// Suitable for distributed scenarios where multiple instances need coordinated projection status management.
/// </summary>
/// <remarks>
/// <para>
/// Documents are stored with partition key <c>/projectionName</c> for efficient queries
/// scoped to a single projection type. The document ID format is <c>{projectionName}_{objectId}</c>.
/// </para>
/// <para>
/// Optimistic concurrency is enforced via ETags on all status-changing operations to prevent
/// conflicting updates from multiple instances.
/// </para>
/// </remarks>
public class CosmosDbProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private readonly ILogger<CosmosDbProjectionStatusCoordinator>? _logger;
    private Container? _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbProjectionStatusCoordinator"/> class.
    /// </summary>
    /// <param name="cosmosClient">The CosmosDB client instance.</param>
    /// <param name="databaseName">The database name containing the status container.</param>
    /// <param name="containerName">The container name for projection status documents. Defaults to "projection-status".</param>
    /// <param name="logger">Optional logger.</param>
    public CosmosDbProjectionStatusCoordinator(
        CosmosClient cosmosClient,
        string databaseName,
        string? containerName = null,
        ILogger<CosmosDbProjectionStatusCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(databaseName);

        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _containerName = containerName ?? "projection-status";
        _logger = logger;
    }

    private static string GetDocumentId(string projectionName, string objectId) =>
        $"{projectionName}_{objectId}";

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

        var container = await GetContainerAsync();
        var documentId = GetDocumentId(projectionName, objectId);
        var partitionKey = new PartitionKey(projectionName);
        var token = RebuildToken.Create(projectionName, objectId, strategy, timeout);
        var now = DateTimeOffset.UtcNow;

        var rebuildInfo = RebuildInfo.Start(strategy);

        var document = new CosmosStatusDocument
        {
            Id = documentId,
            ProjectionName = projectionName,
            ObjectId = objectId,
            Status = (int)ProjectionStatus.Rebuilding,
            StatusChangedAt = now,
            SchemaVersion = 0,
            RebuildTokenJson = JsonConvert.SerializeObject(token),
            RebuildInfoJson = JsonConvert.SerializeObject(rebuildInfo)
        };

        await container.UpsertItemAsync(
            document,
            partitionKey,
            cancellationToken: cancellationToken);

        _logger?.LogInformation(
            "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
            projectionName, objectId, strategy, token.ExpiresAt);

        return token;
    }

    /// <inheritdoc />
    public async Task StartCatchUpAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var container = await GetContainerAsync();
        var documentId = GetDocumentId(token.ProjectionName, token.ObjectId);
        var partitionKey = new PartitionKey(token.ProjectionName);

        var (document, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return;
        }

        ValidateToken(token, document);

        var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);

        document.Status = (int)ProjectionStatus.CatchingUp;
        document.StatusChangedAt = DateTimeOffset.UtcNow;
        document.RebuildInfoJson = JsonConvert.SerializeObject(rebuildInfo?.WithProgress());

        await container.ReplaceItemAsync(
            document,
            documentId,
            partitionKey,
            new ItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);

        _logger?.LogInformation(
            "Started catch-up for {ProjectionName}:{ObjectId}",
            token.ProjectionName, token.ObjectId);
    }

    /// <inheritdoc />
    public async Task MarkReadyAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var container = await GetContainerAsync();
        var documentId = GetDocumentId(token.ProjectionName, token.ObjectId);
        var partitionKey = new PartitionKey(token.ProjectionName);

        var (document, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return;
        }

        ValidateToken(token, document);

        var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);

        document.Status = (int)ProjectionStatus.Ready;
        document.StatusChangedAt = DateTimeOffset.UtcNow;
        document.RebuildInfoJson = JsonConvert.SerializeObject(rebuildInfo?.WithCompletion());

        await container.ReplaceItemAsync(
            document,
            documentId,
            partitionKey,
            new ItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);

        _logger?.LogInformation(
            "Marked {ProjectionName}:{ObjectId} as ready",
            token.ProjectionName, token.ObjectId);
    }

    /// <inheritdoc />
    public async Task CompleteRebuildAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var container = await GetContainerAsync();
        var documentId = GetDocumentId(token.ProjectionName, token.ObjectId);
        var partitionKey = new PartitionKey(token.ProjectionName);

        var (document, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return;
        }

        ValidateToken(token, document);

        var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);

        document.Status = (int)ProjectionStatus.Active;
        document.StatusChangedAt = DateTimeOffset.UtcNow;
        document.RebuildTokenJson = null;
        document.RebuildInfoJson = JsonConvert.SerializeObject(rebuildInfo?.WithCompletion());

        await container.ReplaceItemAsync(
            document,
            documentId,
            partitionKey,
            new ItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);

        _logger?.LogInformation(
            "Completed rebuild for {ProjectionName}:{ObjectId}",
            token.ProjectionName, token.ObjectId);
    }

    /// <inheritdoc />
    public async Task CancelRebuildAsync(
        RebuildToken token,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var container = await GetContainerAsync();
        var documentId = GetDocumentId(token.ProjectionName, token.ObjectId);
        var partitionKey = new PartitionKey(token.ProjectionName);

        var (document, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return;
        }

        var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);
        var newStatus = error != null ? ProjectionStatus.Failed : ProjectionStatus.Active;
        var updatedRebuildInfo = error != null
            ? rebuildInfo?.WithError(error)
            : rebuildInfo?.WithCompletion();

        document.Status = (int)newStatus;
        document.StatusChangedAt = DateTimeOffset.UtcNow;
        document.RebuildTokenJson = null;
        document.RebuildInfoJson = JsonConvert.SerializeObject(updatedRebuildInfo);

        await container.ReplaceItemAsync(
            document,
            documentId,
            partitionKey,
            new ItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);

        _logger?.LogWarning(
            "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
            token.ProjectionName, token.ObjectId, error ?? "none");
    }

    /// <inheritdoc />
    public async Task<ProjectionStatusInfo?> GetStatusAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync();
        var documentId = GetDocumentId(projectionName, objectId);
        var partitionKey = new PartitionKey(projectionName);

        var (document, _) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return null;
        }

        return ToProjectionStatusInfo(document);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(
        ProjectionStatus status,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync();

        var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
            .WithParameter("@status", (int)status);

        var results = new List<ProjectionStatusInfo>();

        using var iterator = container.GetItemQueryIterator<CosmosStatusDocument>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var document in response)
            {
                results.Add(ToProjectionStatusInfo(document));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync();
        var now = DateTimeOffset.UtcNow;
        var recovered = 0;

        // Query for all documents that are in a rebuilding state (Rebuilding or CatchingUp)
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.status IN (@rebuilding, @catchingUp)")
            .WithParameter("@rebuilding", (int)ProjectionStatus.Rebuilding)
            .WithParameter("@catchingUp", (int)ProjectionStatus.CatchingUp);

        using var iterator = container.GetItemQueryIterator<CosmosStatusDocument>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var document in response)
            {
                var rebuildToken = DeserializeRebuildToken(document.RebuildTokenJson);
                if (rebuildToken == null || !rebuildToken.IsExpired)
                {
                    continue;
                }

                var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);

                document.Status = (int)ProjectionStatus.Failed;
                document.StatusChangedAt = now;
                document.RebuildTokenJson = null;
                document.RebuildInfoJson = JsonConvert.SerializeObject(
                    rebuildInfo?.WithError("Rebuild timed out"));

                try
                {
                    var partitionKey = new PartitionKey(document.ProjectionName);
                    await container.ReplaceItemAsync(
                        document,
                        document.Id,
                        partitionKey,
                        new ItemRequestOptions { IfMatchEtag = document.ETag },
                        cancellationToken);

                    recovered++;

                    _logger?.LogWarning(
                        "Recovered stuck rebuild for {ProjectionName}:{ObjectId}",
                        document.ProjectionName, document.ObjectId);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // Document was modified concurrently, skip it
                    _logger?.LogDebug(
                        ex,
                        "Skipped recovery for {ProjectionName}:{ObjectId} due to concurrent modification",
                        document.ProjectionName, document.ObjectId);
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
        var container = await GetContainerAsync();
        var documentId = GetDocumentId(projectionName, objectId);
        var partitionKey = new PartitionKey(projectionName);
        var now = DateTimeOffset.UtcNow;

        try
        {
            var (existing, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
            if (existing != null)
            {
                existing.Status = (int)ProjectionStatus.Disabled;
                existing.StatusChangedAt = now;

                await container.ReplaceItemAsync(
                    existing,
                    documentId,
                    partitionKey,
                    new ItemRequestOptions { IfMatchEtag = etag },
                    cancellationToken);
            }
            else
            {
                var document = new CosmosStatusDocument
                {
                    Id = documentId,
                    ProjectionName = projectionName,
                    ObjectId = objectId,
                    Status = (int)ProjectionStatus.Disabled,
                    StatusChangedAt = now,
                    SchemaVersion = 0
                };

                await container.CreateItemAsync(document, partitionKey, cancellationToken: cancellationToken);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Document was created concurrently, try update
            var (existing, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
            if (existing != null)
            {
                existing.Status = (int)ProjectionStatus.Disabled;
                existing.StatusChangedAt = now;

                await container.ReplaceItemAsync(
                    existing,
                    documentId,
                    partitionKey,
                    new ItemRequestOptions { IfMatchEtag = etag },
                    cancellationToken);
            }
        }

        _logger?.LogInformation(
            "Disabled projection {ProjectionName}:{ObjectId}",
            projectionName, objectId);
    }

    /// <inheritdoc />
    public async Task EnableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync();
        var documentId = GetDocumentId(projectionName, objectId);
        var partitionKey = new PartitionKey(projectionName);

        var (document, etag) = await ReadDocumentAsync(container, documentId, partitionKey, cancellationToken);
        if (document == null)
        {
            return;
        }

        document.Status = (int)ProjectionStatus.Active;
        document.StatusChangedAt = DateTimeOffset.UtcNow;

        await container.ReplaceItemAsync(
            document,
            documentId,
            partitionKey,
            new ItemRequestOptions { IfMatchEtag = etag },
            cancellationToken);

        _logger?.LogInformation(
            "Enabled projection {ProjectionName}:{ObjectId}",
            projectionName, objectId);
    }

    private async Task<Container> GetContainerAsync()
    {
        if (_container != null)
        {
            return _container;
        }

        var database = _cosmosClient.GetDatabase(_databaseName);
        _container = database.GetContainer(_containerName);
        return _container;
    }

    private static async Task<(CosmosStatusDocument? Document, string? ETag)> ReadDocumentAsync(
        Container container,
        string documentId,
        PartitionKey partitionKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<CosmosStatusDocument>(
                documentId,
                partitionKey,
                cancellationToken: cancellationToken);

            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }
    }

    private static void ValidateToken(RebuildToken token, CosmosStatusDocument document)
    {
        var storedToken = DeserializeRebuildToken(document.RebuildTokenJson);

        if (storedToken == null || storedToken.Token != token.Token)
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

    private static RebuildToken? DeserializeRebuildToken(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<RebuildToken>(json);
    }

    private static RebuildInfo? DeserializeRebuildInfo(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<RebuildInfo>(json);
    }

    private static ProjectionStatusInfo ToProjectionStatusInfo(CosmosStatusDocument document)
    {
        var rebuildInfo = DeserializeRebuildInfo(document.RebuildInfoJson);

        return new ProjectionStatusInfo(
            document.ProjectionName,
            document.ObjectId,
            (ProjectionStatus)document.Status,
            document.StatusChangedAt,
            document.SchemaVersion,
            rebuildInfo);
    }
}

/// <summary>
/// CosmosDB document model for storing projection status information.
/// Uses Newtonsoft.Json attributes for serialization compatibility.
/// </summary>
/// <remarks>
/// Partition key: <c>/projectionName</c>.
/// Document ID format: <c>{projectionName}_{objectId}</c>.
/// </remarks>
internal class CosmosStatusDocument
{
    /// <summary>
    /// Unique document identifier. Format: {projectionName}_{objectId}.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The projection type name. Used as partition key.
    /// </summary>
    [JsonProperty("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// The object identifier.
    /// </summary>
    [JsonProperty("objectId")]
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// The projection status as an integer (maps to <see cref="ProjectionStatus"/>).
    /// </summary>
    [JsonProperty("status")]
    public int Status { get; set; }

    /// <summary>
    /// When the status was last changed.
    /// </summary>
    [JsonProperty("statusChangedAt")]
    public DateTimeOffset? StatusChangedAt { get; set; }

    /// <summary>
    /// The stored schema version.
    /// </summary>
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Serialized <see cref="RebuildToken"/> JSON. Null when no rebuild is active.
    /// </summary>
    [JsonProperty("rebuildToken")]
    public string? RebuildTokenJson { get; set; }

    /// <summary>
    /// Serialized <see cref="RebuildInfo"/> JSON. Contains rebuild metadata.
    /// </summary>
    [JsonProperty("rebuildInfo")]
    public string? RebuildInfoJson { get; set; }

    /// <summary>
    /// CosmosDB ETag for optimistic concurrency.
    /// </summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
