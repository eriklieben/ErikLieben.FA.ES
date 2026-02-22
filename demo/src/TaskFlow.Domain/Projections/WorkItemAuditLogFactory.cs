using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.CosmosDb;
using ErikLieben.FA.ES.CosmosDb.Configuration;
using Microsoft.Azure.Cosmos;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Factory for creating and saving WorkItemAuditLog projections.
/// </summary>
public class WorkItemAuditLogFactory : CosmosDbMultiDocumentProjectionFactory<WorkItemAuditLog>
{
    private readonly IObjectDocumentFactory _objectDocumentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;

    public WorkItemAuditLogFactory(
        CosmosClient cosmosClient,
        EventStreamCosmosDbSettings settings,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory)
        : base(cosmosClient, settings, "workitemauditlog", "/partitionKey")
    {
        _objectDocumentFactory = objectDocumentFactory;
        _eventStreamFactory = eventStreamFactory;
    }

    protected override WorkItemAuditLog New()
    {
        return new WorkItemAuditLog(_objectDocumentFactory, _eventStreamFactory);
    }

    protected override WorkItemAuditLog? LoadFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        // Multi-document projections don't load state from a single JSON document
        // They just track checkpoint state
        return new WorkItemAuditLog(documentFactory, eventStreamFactory);
    }

    /// <summary>
    /// Override to properly serialize AuditLogEntry using the correct JSON context.
    /// </summary>
    protected override string SerializeDocument(object document)
    {
        if (document is AuditLogEntry entry)
        {
            return JsonSerializer.Serialize(entry, AuditLogEntryJsonContext.Default.AuditLogEntry);
        }
        return JsonSerializer.Serialize(document);
    }

    /// <summary>
    /// Override to extract partition key from AuditLogEntry without reflection.
    /// </summary>
    protected override string ExtractPartitionKey(object document)
    {
        if (document is AuditLogEntry entry)
        {
            return entry.PartitionKey;
        }
        return base.ExtractPartitionKey(document);
    }

    /// <summary>
    /// Override to create document wrapper for AuditLogEntry without reflection.
    /// </summary>
    protected override Dictionary<string, object> CreateDocumentWrapper(object document, string serializedJson)
    {
        if (document is AuditLogEntry entry)
        {
            // Deserialize and create wrapper with proper id and partitionKey
            var wrapper = JsonSerializer.Deserialize<Dictionary<string, object>>(serializedJson) ?? new();
            wrapper["id"] = entry.Id;
            wrapper["partitionKey"] = entry.PartitionKey;
            return wrapper;
        }
        return base.CreateDocumentWrapper(document, serializedJson);
    }
}

/// <summary>
/// JSON serialization context for AuditLogEntry.
/// </summary>
[JsonSerializable(typeof(AuditLogEntry))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AuditLogEntryJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Interface for WorkItemAuditLog factory.
/// </summary>
public interface IWorkItemAuditLogFactory
{
    Task<WorkItemAuditLog> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorkItemAuditLog projection, CancellationToken cancellationToken = default);
}
