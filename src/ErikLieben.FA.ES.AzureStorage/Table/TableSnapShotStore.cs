using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure;
using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Table.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Provides an Azure Table Storage-backed implementation of <see cref="ISnapShotStore"/> for persisting and retrieving aggregate snapshots.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="TableServiceClient"/> instances.</param>
/// <param name="settings">The Table settings controlling table creation and defaults.</param>
public class TableSnapShotStore(
    IAzureClientFactory<TableServiceClient> clientFactory,
    EventStreamTableSettings settings)
    : ISnapShotStore
{
    /// <summary>
    /// Persists a snapshot of the aggregate to Table Storage using the supplied JSON type info.
    /// </summary>
    /// <param name="object">The aggregate instance to snapshot.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info describing the aggregate type.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot is taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.Set");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationUpsert);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
            activity.SetTag("faes.snapshot.version", version);
            activity.SetTag("faes.snapshot.name", name);
        }

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var rowKey = string.IsNullOrWhiteSpace(name)
            ? $"{version:d20}"
            : $"{version:d20}_{name}";

        var data = JsonSerializer.Serialize(@object, jsonTypeInfo);

        var entity = new TableSnapshotEntity
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            StreamIdentifier = document.Active.StreamIdentifier,
            Version = version,
            Name = name,
            Data = data,
            AggregateType = @object.GetType().FullName ?? @object.GetType().Name
        };

        try
        {
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
            FaesMetrics.RecordSnapshotCreated(document.ObjectName ?? "unknown");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new TableDocumentStoreTableNotFoundException(
                $"The table '{settings.DefaultSnapshotTableName}' was not found. " +
                "Create the table in your deployment or enable AutoCreateTable in settings.", ex);
        }
    }

    /// <summary>
    /// Retrieves a snapshot of the aggregate at the specified version using the supplied JSON type info.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
            activity.SetTag("faes.snapshot.version", version);
            activity.SetTag("faes.snapshot.name", name);
        }

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var rowKey = string.IsNullOrWhiteSpace(name)
            ? $"{version:d20}"
            : $"{version:d20}_{name}";

        try
        {
            var response = await tableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            if (!response.HasValue || response.Value == null)
            {
                activity?.SetTag("faes.snapshot.found", false);
                return null;
            }

            activity?.SetTag("faes.snapshot.found", true);
            return JsonSerializer.Deserialize(response.Value.Data, jsonTypeInfo);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            activity?.SetTag("faes.snapshot.found", false);
            return null;
        }
    }

    /// <summary>
    /// Retrieves a snapshot as an untyped object at the specified version using the supplied JSON type info.
    /// </summary>
    /// <param name="jsonTypeInfo">The source-generated JSON type info representing the runtime type of the snapshot.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.Get");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
            activity.SetTag("faes.snapshot.version", version);
            activity.SetTag("faes.snapshot.name", name);
        }

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var rowKey = string.IsNullOrWhiteSpace(name)
            ? $"{version:d20}"
            : $"{version:d20}_{name}";

        try
        {
            var response = await tableClient.GetEntityIfExistsAsync<TableSnapshotEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            if (!response.HasValue || response.Value == null)
            {
                activity?.SetTag("faes.snapshot.found", false);
                return null;
            }

            activity?.SetTag("faes.snapshot.found", true);
            return JsonSerializer.Deserialize(response.Value.Data, jsonTypeInfo);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            activity?.SetTag("faes.snapshot.found", false);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.List");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationQuery);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
        }

        var tableClient = await GetTableClientAsync(document);
        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";

        var snapshots = new List<SnapshotMetadata>();

        await foreach (var entity in tableClient.QueryAsync<TableSnapshotEntity>(
            e => e.PartitionKey == partitionKey,
            cancellationToken: cancellationToken))
        {
            var metadata = new SnapshotMetadata(
                entity.Version,
                entity.Timestamp ?? DateTimeOffset.MinValue,
                entity.Name,
                entity.Data?.Length);
            snapshots.Add(metadata);
        }

        activity?.SetTag("faes.snapshot.count", snapshots.Count);
        return snapshots.OrderByDescending(s => s.Version).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        IObjectDocument document,
        int version,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.Delete");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
            activity.SetTag("faes.snapshot.version", version);
            activity.SetTag("faes.snapshot.name", name);
        }

        var tableClient = await GetTableClientAsync(document);

        var partitionKey = $"{document.ObjectName.ToLowerInvariant()}_{document.Active.StreamIdentifier}";
        var rowKey = string.IsNullOrWhiteSpace(name)
            ? $"{version:d20}"
            : $"{version:d20}_{name}";

        try
        {
            await tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
            activity?.SetTag("faes.success", true);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            activity?.SetTag("faes.success", false);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableSnapShotStore.DeleteMany");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.Active.StreamIdentifier);
        }

        var deleted = 0;
        foreach (var version in versions)
        {
            if (await DeleteAsync(document, version, cancellationToken: cancellationToken))
            {
                deleted++;
            }
        }

        activity?.SetTag("faes.snapshot.deleted_count", deleted);
        return deleted;
    }

    private async Task<TableClient> GetTableClientAsync(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.SnapShotStore)
            ? objectDocument.Active.SnapShotStore
            : objectDocument.Active.SnapShotConnectionName;
#pragma warning restore CS0618

        var serviceClient = clientFactory.CreateClient(connectionName);
        var tableClient = serviceClient.GetTableClient(settings.DefaultSnapshotTableName);

        if (settings.AutoCreateTable)
        {
            await tableClient.CreateIfNotExistsAsync();
        }

        return tableClient;
    }
}
