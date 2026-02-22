using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Extension of the generated SprintDashboardFactory to implement ISprintDashboardFactory.
/// </summary>
public partial class SprintDashboardFactory : ISprintDashboardFactory
{
    private readonly IObjectDocumentFactory _objectDocumentFactory = objectDocumentFactory;
    private readonly IEventStreamFactory _eventStreamFactory = eventStreamFactory;

    /// <inheritdoc />
    public async Task<SprintDashboard> GetAsync(CancellationToken cancellationToken = default)
    {
        return await GetOrCreateAsync(_objectDocumentFactory, _eventStreamFactory, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(SprintDashboard dashboard, CancellationToken cancellationToken = default)
    {
        await SaveAsync(dashboard, null, cancellationToken);
    }
}
