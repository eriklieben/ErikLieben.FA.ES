namespace TaskFlow.Domain.Projections;

/// <summary>
/// Factory interface for creating and loading SprintDashboard projections.
/// </summary>
public interface ISprintDashboardFactory
{
    /// <summary>
    /// Loads the SprintDashboard projection from storage, or creates a new instance if it doesn't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded or newly created SprintDashboard instance.</returns>
    Task<SprintDashboard> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the SprintDashboard projection to storage.
    /// </summary>
    /// <param name="dashboard">The SprintDashboard to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(SprintDashboard dashboard, CancellationToken cancellationToken = default);
}
