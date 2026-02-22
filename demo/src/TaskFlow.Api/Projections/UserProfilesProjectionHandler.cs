using System.Diagnostics;
using TaskFlow.Api.Services;
using TaskFlow.Domain.Messaging;
using TaskFlow.Domain.Projections;

namespace TaskFlow.Api.Projections;

/// <summary>
/// Handles UserProfiles projection updates
/// Filters and processes only userProfile events
/// </summary>
public class UserProfilesProjectionHandler(
    IProjectionService projectionService,
    UserProfilesFactory factory,
    ILogger<UserProfilesProjectionHandler> logger)
    : IProjectionHandler
{
    private long? _lastGenerationDurationMs;

    public string ProjectionName => "UserProfiles";

    public async Task HandleBatchAsync(IEnumerable<ProjectionUpdateRequested> events, CancellationToken cancellationToken = default)
    {
        var userProfileEvents = events.Where(e => e.ObjectName == "userprofile").ToList();

        if (userProfileEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "Processing {EventCount} userProfile events for {ProjectionName}",
            userProfileEvents.Count,
            ProjectionName);

        var stopwatch = Stopwatch.StartNew();

        var projection = projectionService.GetUserProfiles();

        foreach (var evt in userProfileEvents)
        {
            await projection.UpdateToVersion(evt.VersionToken);
        }

        await factory.SaveAsync(projection, cancellationToken: cancellationToken);

        stopwatch.Stop();
        _lastGenerationDurationMs = stopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "Successfully updated {ProjectionName} with {EventCount} events in {DurationMs}ms",
            ProjectionName,
            userProfileEvents.Count,
            _lastGenerationDurationMs);
    }

    public async Task<ProjectionStatus> GetStatusAsync()
    {
        var projection = projectionService.GetUserProfiles();
        var lastModified = await factory.GetLastModifiedAsync();

        return new ProjectionStatus
        {
            Name = ProjectionName,
            CheckpointCount = projection.Checkpoint.Count,
            CheckpointFingerprint = projection.CheckpointFingerprint,
            LastModified = lastModified,
            IsPersisted = lastModified.HasValue,
            LastGenerationDurationMs = _lastGenerationDurationMs
        };
    }
}
