using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Projections;
using TaskFlow.Domain.Events.Release;
using TaskFlow.Domain.ValueObjects.Release;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Projection that provides dashboard metrics and summaries for all releases.
/// Demonstrates projecting events from S3-backed aggregates.
/// </summary>
[ProjectionWithExternalCheckpoint]
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class ReleaseDashboard : Projection
{
    /// <summary>
    /// Dictionary of all releases indexed by their ID
    /// </summary>
    public Dictionary<string, ReleaseSummary> Releases { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ReleaseCreated @event, string releaseId)
    {
        Releases[releaseId] = new ReleaseSummary
        {
            ReleaseId = releaseId,
            Name = @event.Name,
            Version = @event.Version,
            ProjectId = @event.ProjectId,
            Status = ReleaseStatus.Draft,
            CreatedBy = @event.CreatedBy,
            CreatedAt = @event.CreatedAt
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ReleaseStaged @event, string releaseId)
    {
        if (Releases.TryGetValue(releaseId, out var release))
        {
            release.Status = ReleaseStatus.Staged;
            release.StagedBy = @event.StagedBy;
            release.StagedAt = @event.StagedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ReleaseDeployed @event, string releaseId)
    {
        if (Releases.TryGetValue(releaseId, out var release))
        {
            release.Status = ReleaseStatus.Deployed;
            release.DeployedBy = @event.DeployedBy;
            release.DeployedAt = @event.DeployedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ReleaseCompleted @event, string releaseId)
    {
        if (Releases.TryGetValue(releaseId, out var release))
        {
            release.Status = ReleaseStatus.Completed;
            release.CompletedBy = @event.CompletedBy;
            release.CompletedAt = @event.CompletedAt;
        }
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ReleaseRolledBack @event, string releaseId)
    {
        if (Releases.TryGetValue(releaseId, out var release))
        {
            release.Status = ReleaseStatus.RolledBack;
            release.RolledBackBy = @event.RolledBackBy;
            release.RolledBackAt = @event.RolledBackAt;
            release.RollbackReason = @event.Reason;
        }
    }

    /// <summary>
    /// Get all releases as a list
    /// </summary>
    public IEnumerable<ReleaseSummary> GetAllReleases()
    {
        return Releases.Values.OrderByDescending(r => r.CreatedAt);
    }

    /// <summary>
    /// Get releases by project
    /// </summary>
    public IEnumerable<ReleaseSummary> GetReleasesByProject(string projectId)
    {
        return Releases.Values
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt);
    }

    /// <summary>
    /// Get releases by status
    /// </summary>
    public IEnumerable<ReleaseSummary> GetReleasesByStatus(ReleaseStatus status)
    {
        return Releases.Values
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt);
    }

    /// <summary>
    /// Get deployed releases
    /// </summary>
    public IEnumerable<ReleaseSummary> GetDeployedReleases()
    {
        return Releases.Values
            .Where(r => r.Status == ReleaseStatus.Deployed)
            .OrderByDescending(r => r.DeployedAt);
    }

    /// <summary>
    /// Get release by ID
    /// </summary>
    public ReleaseSummary? GetReleaseById(string releaseId)
    {
        return Releases.TryGetValue(releaseId, out var release) ? release : null;
    }

    /// <summary>
    /// Get release statistics
    /// </summary>
    public ReleaseStatistics GetStatistics()
    {
        var releases = Releases.Values.ToList();
        return new ReleaseStatistics
        {
            TotalReleases = releases.Count,
            DraftCount = releases.Count(r => r.Status == ReleaseStatus.Draft),
            StagedCount = releases.Count(r => r.Status == ReleaseStatus.Staged),
            DeployedCount = releases.Count(r => r.Status == ReleaseStatus.Deployed),
            CompletedCount = releases.Count(r => r.Status == ReleaseStatus.Completed),
            RolledBackCount = releases.Count(r => r.Status == ReleaseStatus.RolledBack)
        };
    }
}

/// <summary>
/// Summary information for a release
/// </summary>
public class ReleaseSummary
{
    public string ReleaseId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Draft;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? StagedBy { get; set; }
    public DateTime? StagedAt { get; set; }
    public string? DeployedBy { get; set; }
    public DateTime? DeployedAt { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RolledBackBy { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? RollbackReason { get; set; }
}

/// <summary>
/// Release statistics across all releases
/// </summary>
public class ReleaseStatistics
{
    public int TotalReleases { get; set; }
    public int DraftCount { get; set; }
    public int StagedCount { get; set; }
    public int DeployedCount { get; set; }
    public int CompletedCount { get; set; }
    public int RolledBackCount { get; set; }

    /// <summary>
    /// Completion rate as a percentage
    /// </summary>
    public double CompletionRate => TotalReleases > 0
        ? (double)CompletedCount / TotalReleases * 100
        : 0;

    /// <summary>
    /// Rollback rate as a percentage
    /// </summary>
    public double RollbackRate => TotalReleases > 0
        ? (double)RolledBackCount / TotalReleases * 100
        : 0;
}
