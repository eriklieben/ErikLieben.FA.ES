using Azure.Data.Tables;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage.Table;
using Microsoft.Extensions.Azure;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Factory for creating and saving WorkItemReportingIndex projections.
/// </summary>
public class WorkItemReportingIndexFactory : TableProjectionFactory<WorkItemReportingIndex>
{
    private readonly IObjectDocumentFactory _objectDocumentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;

    public WorkItemReportingIndexFactory(
        IAzureClientFactory<TableServiceClient> tableServiceClientFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory,
        string connectionName = "tables",
        string tableName = "workitemindex")
        : base(tableServiceClientFactory, connectionName, tableName)
    {
        _objectDocumentFactory = objectDocumentFactory;
        _eventStreamFactory = eventStreamFactory;
    }

    protected override WorkItemReportingIndex New()
    {
        return new WorkItemReportingIndex(_objectDocumentFactory, _eventStreamFactory);
    }

    protected override WorkItemReportingIndex? LoadFromJson(
        string json,
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        // Table projections don't load state from a single JSON document
        // They just track checkpoint state
        return new WorkItemReportingIndex(documentFactory, eventStreamFactory);
    }
}

/// <summary>
/// Interface for WorkItemReportingIndex factory.
/// </summary>
public interface IWorkItemReportingIndexFactory
{
    Task<WorkItemReportingIndex> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorkItemReportingIndex projection, CancellationToken cancellationToken = default);
}
