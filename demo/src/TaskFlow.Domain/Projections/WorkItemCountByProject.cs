using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Postgres;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.WorkItem;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Counts work items per project. Persisted as a single JSONB row in PostgreSQL —
/// the simplest demo of the [PostgresJsonbProjection] attribute and the
/// table-per-projection storage model.
/// </summary>
[ProjectionVersion(1)]
[PostgresJsonbProjection]
public partial class WorkItemCountByProject : Projection
{
    public Dictionary<string, int> CountByProject { get; } = new();

    private void When(WorkItemPlanned @event)
    {
        if (!CountByProject.TryAdd(@event.ProjectId, 1))
        {
            CountByProject[@event.ProjectId] += 1;
        }
    }

    public int GetCount(string projectId)
        => CountByProject.TryGetValue(projectId, out var count) ? count : 0;
}
