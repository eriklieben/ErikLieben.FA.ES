using ErikLieben.FA.ES;
using Microsoft.Azure.Cosmos;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Services;

/// <summary>
/// Service for accessing projection data
/// Provides a clean interface for querying read models
/// </summary>
public interface IProjectionService
{
    /// <summary>
    /// Get the ActiveWorkItems projection
    /// </summary>
    ActiveWorkItems GetActiveWorkItems();

    /// <summary>
    /// Get the ProjectDashboard projection
    /// </summary>
    ProjectDashboard GetProjectDashboard();

    /// <summary>
    /// Get the UserProfiles projection
    /// </summary>
    UserProfiles GetUserProfiles();

    /// <summary>
    /// Get the EventUpcastingDemonstration projection
    /// </summary>
    EventUpcastingDemonstration GetEventUpcastingDemonstration();

    /// <summary>
    /// Get the ProjectKanbanBoard projection
    /// </summary>
    ProjectKanbanBoard GetProjectKanbanBoard();

    /// <summary>
    /// Get the EpicSummary projection
    /// </summary>
    EpicSummary GetEpicSummary();

    /// <summary>
    /// Get the SprintDashboard projection (returns null if CosmosDB not enabled)
    /// </summary>
    SprintDashboard? GetSprintDashboard();

    /// <summary>
    /// Get the CosmosClient for direct CosmosDB access (returns null if CosmosDB not enabled)
    /// </summary>
    CosmosClient? GetCosmosClient();

    /// <summary>
    /// Get the ObjectDocumentFactory for document access
    /// </summary>
    IObjectDocumentFactory? GetObjectDocumentFactory();

    /// <summary>
    /// Get the EventStreamFactory for event stream access
    /// </summary>
    IEventStreamFactory? GetEventStreamFactory();
}

/// <summary>
/// Implementation of projection service
/// </summary>
public class ProjectionService : IProjectionService
{
    private readonly ActiveWorkItems _activeWorkItems;
    private readonly ProjectDashboard _projectDashboard;
    private readonly UserProfiles _userProfiles;
    private readonly EventUpcastingDemonstration _eventUpcastingDemonstration;
    private readonly ProjectKanbanBoard _projectKanbanBoard;
    private readonly EpicSummary _epicSummary;
    private readonly SprintDashboard? _sprintDashboard;
    private readonly CosmosClient? _cosmosClient;
    private readonly IObjectDocumentFactory? _objectDocumentFactory;
    private readonly IEventStreamFactory? _eventStreamFactory;

    public ProjectionService(
        ActiveWorkItems activeWorkItems,
        ProjectDashboard projectDashboard,
        UserProfiles userProfiles,
        EventUpcastingDemonstration eventUpcastingDemonstration,
        ProjectKanbanBoard projectKanbanBoard,
        EpicSummary epicSummary,
        SprintDashboard? sprintDashboard = null,
        CosmosClient? cosmosClient = null,
        IObjectDocumentFactory? objectDocumentFactory = null,
        IEventStreamFactory? eventStreamFactory = null)
    {
        _activeWorkItems = activeWorkItems;
        _projectDashboard = projectDashboard;
        _userProfiles = userProfiles;
        _eventUpcastingDemonstration = eventUpcastingDemonstration;
        _projectKanbanBoard = projectKanbanBoard;
        _epicSummary = epicSummary;
        _sprintDashboard = sprintDashboard;
        _cosmosClient = cosmosClient;
        _objectDocumentFactory = objectDocumentFactory;
        _eventStreamFactory = eventStreamFactory;
    }

    public ActiveWorkItems GetActiveWorkItems()
    {
        return _activeWorkItems;
    }

    public ProjectDashboard GetProjectDashboard()
    {
        return _projectDashboard;
    }

    public UserProfiles GetUserProfiles()
    {
        return _userProfiles;
    }

    public EventUpcastingDemonstration GetEventUpcastingDemonstration()
    {
        return _eventUpcastingDemonstration;
    }

    public ProjectKanbanBoard GetProjectKanbanBoard()
    {
        return _projectKanbanBoard;
    }

    public EpicSummary GetEpicSummary()
    {
        return _epicSummary;
    }

    public SprintDashboard? GetSprintDashboard()
    {
        return _sprintDashboard;
    }

    public CosmosClient? GetCosmosClient()
    {
        return _cosmosClient;
    }

    public IObjectDocumentFactory? GetObjectDocumentFactory()
    {
        return _objectDocumentFactory;
    }

    public IEventStreamFactory? GetEventStreamFactory()
    {
        return _eventStreamFactory;
    }
}
