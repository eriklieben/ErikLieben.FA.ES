using TaskFlow.Domain.Messaging;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Projections;

namespace TaskFlow.Api.Messaging;

/// <summary>
/// Coordinates projection updates by batching events and delegating to individual projection handlers.
/// Batches updates over a short window for efficiency and notifies clients via SignalR.
/// </summary>
public class DirectProjectionUpdateHandler : IProjectionEventHandler<ProjectionUpdateRequested>, IDisposable
{
    private readonly IEnumerable<IProjectionHandler> _projectionHandlers;
    private readonly ILogger<DirectProjectionUpdateHandler> _logger;
    private readonly IHubContext<TaskFlowHub> _hubContext;

    private readonly List<ProjectionUpdateRequested> _pendingEvents = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private readonly SemaphoreSlim _flushLock = new(1, 1); // Ensures only one FlushAsync runs at a time
    private readonly TimeSpan _batchWindow = TimeSpan.FromSeconds(2); // Flush after 2 seconds of inactivity (debounce pattern)
    private Timer? _flushTimer;
    private bool _disposed;

    public DirectProjectionUpdateHandler(
        IEnumerable<IProjectionHandler> projectionHandlers,
        ILogger<DirectProjectionUpdateHandler> logger,
        IHubContext<TaskFlowHub> hubContext)
    {
        _projectionHandlers = projectionHandlers;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task HandleAsync(ProjectionUpdateRequested @event, CancellationToken cancellationToken = default)
    {
        await _batchLock.WaitAsync(cancellationToken);
        try
        {
            var isFirstEvent = _pendingEvents.Count == 0;

            _pendingEvents.Add(@event);

            _logger.LogDebug(
                "Batched version token {VersionToken} for {ObjectName}/{StreamId} projection update ({EventCount} events)",
                @event.VersionToken.Value,
                @event.ObjectName,
                @event.StreamIdentifier,
                @event.EventCount);

            // If this is the first event in a batch, notify that projections are awaiting
            if (isFirstEvent)
            {
                _ = Task.Run(async () => await NotifyProjectionStatus("awaiting"));
            }

            // Reset the timer - flush after 2 seconds of inactivity (debounce pattern)
            // This collects version tokens while events are streaming, then flushes when activity stops
            _flushTimer?.Dispose();
            _flushTimer = new Timer(
                async _ => await FlushAsync(),
                null,
                _batchWindow,
                Timeout.InfiniteTimeSpan);
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private async Task FlushAsync()
    {
        // Prevent concurrent flush operations
        if (!await _flushLock.WaitAsync(0))
        {
            // Another flush is already running, skip this one
            return;
        }

        try
        {
            List<ProjectionUpdateRequested> batch;

            await _batchLock.WaitAsync();
            try
            {
                if (_pendingEvents.Count == 0)
                {
                    return;
                }

                batch = _pendingEvents.ToList();
                _pendingEvents.Clear();
            }
            finally
            {
                _batchLock.Release();
            }

            try
            {
                // Notify that we're now actively projecting
                await NotifyProjectionStatus("projecting");

                _logger.LogInformation(
                    "Processing {TotalEvents} events across {HandlerCount} projection handlers",
                    batch.Count,
                    _projectionHandlers.Count());

                // Process each projection handler in parallel
                // Each handler filters its own relevant events and updates its projection
                await Task.WhenAll(_projectionHandlers.Select(handler =>
                    Task.Run(async () => await handler.HandleBatchAsync(batch))));

                _logger.LogInformation(
                    "Successfully updated all projections with {EventCount} events",
                    batch.Count);

                // Notify clients that projections are now idle
                await NotifyProjectionStatus("idle");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to update projections with batch of {EventCount} events",
                    batch.Count);

                // Re-add events back for retry
                await _batchLock.WaitAsync();
                try
                {
                    _pendingEvents.InsertRange(0, batch);
                }
                finally
                {
                    _batchLock.Release();
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task NotifyProjectionStatus(string status)
    {
        try
        {
            // Get status from all projection handlers
            var statuses = await Task.WhenAll(_projectionHandlers.Select(h => h.GetStatusAsync()));

            var projections = statuses.Select(s => new
            {
                name = s.Name,
                status = status,
                lastUpdate = s.LastModified ?? DateTimeOffset.UtcNow,
                checkpoint = s.CheckpointCount,
                checkpointFingerprint = s.CheckpointFingerprint,
                isPersisted = s.IsPersisted,
                lastGenerationDurationMs = s.LastGenerationDurationMs
            }).ToArray();

            await _hubContext.Clients.All.SendAsync("ProjectionUpdated", new
            {
                projections = projections,
                timestamp = DateTimeOffset.UtcNow
            });

            _logger.LogDebug("SignalR notification sent with status: {Status}", status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR status notification");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Stop timer
        _flushTimer?.Dispose();

        // Flush any remaining tokens before disposing
        FlushAsync().GetAwaiter().GetResult();

        _batchLock.Dispose();
        _flushLock.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
