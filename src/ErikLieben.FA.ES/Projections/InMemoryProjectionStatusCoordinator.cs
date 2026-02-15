using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// In-memory implementation of <see cref="IProjectionStatusCoordinator"/>.
/// Suitable for single-instance applications and testing.
/// For distributed scenarios, use a storage-backed implementation.
/// </summary>
public class InMemoryProjectionStatusCoordinator : IProjectionStatusCoordinator
{
    private readonly ConcurrentDictionary<string, ProjectionStatusInfo> _statuses = new();
    private readonly ConcurrentDictionary<string, RebuildToken> _activeRebuilds = new();
    private readonly ILogger<InMemoryProjectionStatusCoordinator>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryProjectionStatusCoordinator"/> class.
    /// </summary>
    /// <param name="options">The projection options (reserved for future use).</param>
    /// <param name="logger">Optional logger.</param>
    public InMemoryProjectionStatusCoordinator(
        IOptions<ProjectionOptions>? options = null,
        ILogger<InMemoryProjectionStatusCoordinator>? logger = null)
    {
        _logger = logger;
    }

    private static string GetKey(string projectionName, string objectId) =>
        $"{projectionName}:{objectId}";

    /// <inheritdoc />
    public Task<RebuildToken> StartRebuildAsync(
        string projectionName,
        string objectId,
        RebuildStrategy strategy,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectionName);
        ArgumentNullException.ThrowIfNull(objectId);

        var key = GetKey(projectionName, objectId);
        var token = RebuildToken.Create(projectionName, objectId, strategy, timeout);

        var rebuildInfo = RebuildInfo.Start(strategy);
        var statusInfo = new ProjectionStatusInfo(
            projectionName,
            objectId,
            ProjectionStatus.Rebuilding,
            DateTimeOffset.UtcNow,
            0,
            rebuildInfo);

        _statuses.AddOrUpdate(key, statusInfo, (_, _) => statusInfo);
        _activeRebuilds[key] = token;

        _logger?.LogInformation(
            "Started rebuild for {ProjectionName}:{ObjectId} with strategy {Strategy}, expires at {ExpiresAt}",
            projectionName, objectId, strategy, token.ExpiresAt);

        return Task.FromResult(token);
    }

    /// <inheritdoc />
    public Task StartCatchUpAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ValidateToken(token);

        var key = GetKey(token.ProjectionName, token.ObjectId);
        if (_statuses.TryGetValue(key, out var current))
        {
            var updated = current with
            {
                Status = ProjectionStatus.CatchingUp,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = current.RebuildInfo?.WithProgress()
            };
            _statuses[key] = updated;

            _logger?.LogInformation(
                "Started catch-up for {ProjectionName}:{ObjectId}",
                token.ProjectionName, token.ObjectId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkReadyAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ValidateToken(token);

        var key = GetKey(token.ProjectionName, token.ObjectId);
        if (_statuses.TryGetValue(key, out var current))
        {
            var updated = current with
            {
                Status = ProjectionStatus.Ready,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = current.RebuildInfo?.WithCompletion()
            };
            _statuses[key] = updated;

            _logger?.LogInformation(
                "Marked {ProjectionName}:{ObjectId} as ready",
                token.ProjectionName, token.ObjectId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteRebuildAsync(RebuildToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ValidateToken(token);

        var key = GetKey(token.ProjectionName, token.ObjectId);
        if (_statuses.TryGetValue(key, out var current))
        {
            var updated = current with
            {
                Status = ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = current.RebuildInfo?.WithCompletion()
            };
            _statuses[key] = updated;
        }

        _activeRebuilds.TryRemove(key, out _);

        _logger?.LogInformation(
            "Completed rebuild for {ProjectionName}:{ObjectId}",
            token.ProjectionName, token.ObjectId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CancelRebuildAsync(
        RebuildToken token,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var key = GetKey(token.ProjectionName, token.ObjectId);
        if (_statuses.TryGetValue(key, out var current))
        {
            var newStatus = error != null ? ProjectionStatus.Failed : ProjectionStatus.Active;
            var rebuildInfo = error != null
                ? current.RebuildInfo?.WithError(error)
                : current.RebuildInfo?.WithCompletion();

            var updated = current with
            {
                Status = newStatus,
                StatusChangedAt = DateTimeOffset.UtcNow,
                RebuildInfo = rebuildInfo
            };
            _statuses[key] = updated;
        }

        _activeRebuilds.TryRemove(key, out _);

        _logger?.LogWarning(
            "Cancelled rebuild for {ProjectionName}:{ObjectId}. Error: {Error}",
            token.ProjectionName, token.ObjectId, error ?? "none");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ProjectionStatusInfo?> GetStatusAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(projectionName, objectId);
        _statuses.TryGetValue(key, out var status);
        return Task.FromResult(status);
    }

    /// <inheritdoc />
    public Task<IEnumerable<ProjectionStatusInfo>> GetByStatusAsync(
        ProjectionStatus status,
        CancellationToken cancellationToken = default)
    {
        var results = _statuses.Values
            .Where(s => s.Status == status)
            .ToList();
        return Task.FromResult<IEnumerable<ProjectionStatusInfo>>(results);
    }

    /// <inheritdoc />
    public Task<int> RecoverStuckRebuildsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var recovered = 0;

        foreach (var kvp in _activeRebuilds)
        {
            if (kvp.Value.IsExpired)
            {
                var key = kvp.Key;
                if (_statuses.TryGetValue(key, out var current) &&
                    current.Status.IsRebuilding())
                {
                    var updated = current with
                    {
                        Status = ProjectionStatus.Failed,
                        StatusChangedAt = now,
                        RebuildInfo = current.RebuildInfo?.WithError("Rebuild timed out")
                    };
                    _statuses[key] = updated;
                    _activeRebuilds.TryRemove(key, out _);
                    recovered++;

                    _logger?.LogWarning(
                        "Recovered stuck rebuild for {ProjectionName}:{ObjectId}",
                        kvp.Value.ProjectionName, kvp.Value.ObjectId);
                }
            }
        }

        return Task.FromResult(recovered);
    }

    /// <inheritdoc />
    public Task DisableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(projectionName, objectId);
        var statusInfo = new ProjectionStatusInfo(
            projectionName,
            objectId,
            ProjectionStatus.Disabled,
            DateTimeOffset.UtcNow,
            0);

        _statuses.AddOrUpdate(key, statusInfo, (_, current) => current with
        {
            Status = ProjectionStatus.Disabled,
            StatusChangedAt = DateTimeOffset.UtcNow
        });

        _logger?.LogInformation(
            "Disabled projection {ProjectionName}:{ObjectId}",
            projectionName, objectId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnableAsync(
        string projectionName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(projectionName, objectId);
        if (_statuses.TryGetValue(key, out var current))
        {
            var updated = current with
            {
                Status = ProjectionStatus.Active,
                StatusChangedAt = DateTimeOffset.UtcNow
            };
            _statuses[key] = updated;

            _logger?.LogInformation(
                "Enabled projection {ProjectionName}:{ObjectId}",
                projectionName, objectId);
        }

        return Task.CompletedTask;
    }

    private void ValidateToken(RebuildToken token)
    {
        var key = GetKey(token.ProjectionName, token.ObjectId);
        if (!_activeRebuilds.TryGetValue(key, out var activeToken) ||
            activeToken.Token != token.Token)
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
}
