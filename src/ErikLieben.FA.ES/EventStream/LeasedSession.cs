using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.Notifications;
using ErikLieben.FA.ES.Observability;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.EventStream;

/// <summary>
/// Implements a leased session for appending events to an event stream with transactional semantics and action support.
/// </summary>
public class LeasedSession : ILeasedSession
{
    private readonly IDataStore datastore;
    private readonly IObjectDocument document;
    private readonly IObjectDocumentFactory documentstore;
    private readonly IEventStream eventStream;
    private readonly List<IStreamDocumentChunkClosedNotification> docClosedNotificationActions = [];
    private readonly List<IAsyncPostCommitAction> postCommitActions = [];

    /// <summary>
    /// Gets the buffer of events pending commit in this session.
    /// </summary>
    public List<JsonEvent> Buffer { get; private set; } = [];

    private readonly List<IPreAppendAction> preAppendActions = [];
    private readonly List<IPostReadAction> postReadActions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LeasedSession"/> class.
    /// </summary>
    /// <param name="eventStream">The event stream associated with this session.</param>
    /// <param name="document">The object document for the stream.</param>
    /// <param name="datastore">The data store for persisting events.</param>
    /// <param name="documentstore">The document store factory for persisting metadata.</param>
    /// <param name="docClosedNotificationActions">Notifications to execute when stream chunks are closed.</param>
    /// <param name="postCommitActions">Actions to execute after events are committed.</param>
    /// <param name="preAppendActions">Actions to execute before events are appended.</param>
    /// <param name="postReadActions">Actions to execute after events are read.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public LeasedSession(
        IEventStream eventStream,
        IObjectDocument document,
        IDataStore datastore,
        IObjectDocumentFactory documentstore,
        IEnumerable<IStreamDocumentChunkClosedNotification> docClosedNotificationActions,
        IEnumerable<IAsyncPostCommitAction> postCommitActions,
        IEnumerable<IPreAppendAction> preAppendActions,
        IEnumerable<IPostReadAction> postReadActions)
    {
        ArgumentNullException.ThrowIfNull(eventStream);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active);
        ArgumentNullException.ThrowIfNull(datastore);
        ArgumentNullException.ThrowIfNull(documentstore);
        ArgumentNullException.ThrowIfNull(docClosedNotificationActions);
        ArgumentNullException.ThrowIfNull(postCommitActions);

        this.document = document;
        this.datastore = datastore;
        this.documentstore = documentstore;
        this.eventStream = eventStream;
        this.docClosedNotificationActions.AddRange(docClosedNotificationActions);
        this.postCommitActions.AddRange(postCommitActions);
        this.preAppendActions.AddRange(preAppendActions);
        this.postReadActions.AddRange(postReadActions);
    }

    /// <summary>
    /// Appends an event with the specified payload to the session buffer.
    /// </summary>
    /// <typeparam name="TPayloadType">The type of the event payload.</typeparam>
    /// <param name="payload">The event payload.</param>
    /// <param name="actionMetadata">Optional metadata about the action that triggered this event.</param>
    /// <param name="overrideEventType">Optional override for the event type name.</param>
    /// <param name="externalSequencer">Optional external sequencer identifier for event ordering.</param>
    /// <param name="metadata">Optional additional metadata as key-value pairs.</param>
    /// <returns>The created event.</returns>
    /// <exception cref="ArgumentNullException">Thrown when payload is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the event type is not registered.</exception>
    public IEvent<TPayloadType> Append<TPayloadType>(
        TPayloadType payload,
        ActionMetadata? actionMetadata = null,
        string? overrideEventType = null,
        string? externalSequencer = null,
        Dictionary<string, string>? metadata = null) where TPayloadType : class
    {
        using var activity = FaesInstrumentation.Core.StartActivity("Session.Append");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.StreamId, document.Active.StreamIdentifier);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.EventType, typeof(TPayloadType).Name);
        }

        ArgumentNullException.ThrowIfNull(payload);

        int version = document.Active.CurrentStreamVersion += 1;


        if (!eventStream.EventTypeRegistry.TryGetByType(typeof(TPayloadType), out var eventTypeInfo) || eventTypeInfo == null)
        {
            throw new InvalidOperationException($"Event type '{typeof(TPayloadType).Name}' is not registered in the event type registry.");
        }

        var eventName = string.IsNullOrWhiteSpace(overrideEventType)
            ? eventTypeInfo.EventName
            : overrideEventType;

        var @event = new JsonEvent
        {
            EventType = eventName,
            EventVersion = version,
            SchemaVersion = eventTypeInfo.SchemaVersion,
            Payload = JsonSerializer.Serialize(payload, eventTypeInfo.JsonTypeInfo),
            ActionMetadata = actionMetadata ?? new ActionMetadata(),
            ExternalSequencer = externalSequencer,
            Metadata = metadata ?? new Dictionary<string, string>(),
        };

        // PRE-APPEND ACTIONS
        if (preAppendActions.Count != 0)
        {
            foreach (var action in preAppendActions)
            {
                @event = @event with
                {
                    Payload =
                    JsonSerializer.Serialize(action.PreAppend(payload, @event, document)(), eventTypeInfo.JsonTypeInfo),
                };
            }
        }

        Buffer.Add(@event);

        return JsonEvent.ToEvent(@event, payload);
    }

    /// <summary>
    /// Commits all buffered events to the event stream.
    /// Handles stream chunking if enabled and executes post-commit actions.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    /// <exception cref="CommitFailedException">
    /// Thrown when the commit operation fails. The exception contains information
    /// about the commit state to help with recovery, including whether events may
    /// have been partially written to storage.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The commit operation updates document metadata first, then appends events.
    /// This order ensures that the document's ETag provides optimistic concurrency
    /// control, preventing concurrent writers from both succeeding.
    /// </para>
    /// <para>
    /// If a <see cref="CommitFailedException"/> is thrown with
    /// <see cref="CommitFailedException.EventsMayBeWritten"/> set to true, some or
    /// all events may have been persisted even though the commit failed. Callers
    /// should reload the document to determine the actual state before retrying.
    /// </para>
    /// </remarks>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Core.StartActivity("Session.Commit");
        var timer = activity != null ? FaesMetrics.StartTimer() : null;
        var allEvents = Buffer.ToList();
        var originalVersion = document.Active.CurrentStreamVersion - Buffer.Count;

        SetCommitActivityTags(activity);

        // Track commit state for exception context
        var commitState = new CommitState();

        try
        {
            if (!document.Active.ChunkingEnabled())
            {
                await CommitWithoutChunkingAsync(commitState);
            }
            else
            {
                await CommitWithChunkingAsync(commitState);
            }
        }
        catch (Exception ex)
        {
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);
            FaesMetrics.RecordCommit(document.ObjectName, FaesSemanticConventions.StorageProviderBlob, success: false);

            // Restore the version so the caller can retry with the same buffer.
            document.Active.CurrentStreamVersion = originalVersion;

            await HandleCommitFailureAsync(ex, commitState, originalVersion, allEvents.Count, cancellationToken);

            // Document update failed before events were written - safe to retry
            throw new CommitFailedException(
                document.Active.StreamIdentifier,
                originalVersion,
                originalVersion + allEvents.Count,
                eventsMayBeWritten: false,
                $"Commit failed for stream '{document.Active.StreamIdentifier}'. " +
                $"Document update failed before events were written. Safe to retry.",
                ex);
        }

        await ExecutePostCommitActionsAsync(allEvents);
        RecordSuccessMetrics(activity, timer, allEvents.Count);

        Buffer.Clear();
    }

    private void SetCommitActivityTags(System.Diagnostics.Activity? activity)
    {
        if (activity?.IsAllDataRequested != true)
        {
            return;
        }

        activity.SetTag(FaesSemanticConventions.StreamId, document.Active.StreamIdentifier);
        activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
        activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        activity.SetTag(FaesSemanticConventions.EventCount, Buffer.Count);
        activity.SetTag(FaesSemanticConventions.ChunkingEnabled, document.Active.ChunkingEnabled());
    }

    private async Task HandleCommitFailureAsync(Exception ex, CommitState commitState, int originalVersion, int eventCount, CancellationToken cancellationToken)
    {
        if (!commitState.EventsAppendStarted)
        {
            return;
        }

        var cleanupFromVersion = originalVersion + 1;
        var cleanupToVersion = originalVersion + eventCount;

        try
        {
            await AttemptCleanupAfterFailedCommitAsync(ex, cleanupFromVersion, cleanupToVersion, originalVersion, cancellationToken);
        }
        catch (Exception cleanupEx) when (cleanupEx is not CommitFailedException)
        {
            await MarkStreamAsBrokenAsync(ex, cleanupEx, cleanupFromVersion, cleanupToVersion, cancellationToken);

            throw new CommitCleanupFailedException(
                document.Active.StreamIdentifier,
                originalVersion,
                cleanupToVersion,
                cleanupFromVersion,
                cleanupToVersion,
                cleanupEx,
                ex);
        }
    }

    private async Task AttemptCleanupAfterFailedCommitAsync(
        Exception originalEx, int cleanupFromVersion, int cleanupToVersion, int originalVersion, CancellationToken cancellationToken)
    {
        var removed = await ((IDataStoreRecovery)datastore).RemoveEventsForFailedCommitAsync(
            document, cleanupFromVersion, cleanupToVersion);

        // Record rollback in metadata
        document.Active.RollbackHistory ??= [];
        document.Active.RollbackHistory.Add(new RollbackRecord
        {
            RolledBackAt = DateTimeOffset.UtcNow,
            FromVersion = cleanupFromVersion,
            ToVersion = cleanupToVersion,
            EventsRemoved = removed,
            OriginalError = originalEx.Message,
            OriginalExceptionType = originalEx.GetType().FullName
        });

        // Persist rollback history (best effort)
        try { await documentstore.SetAsync(document, cancellationToken: cancellationToken); } catch { /* Log but continue */ }

        throw new CommitFailedException(
            document.Active.StreamIdentifier,
            originalVersion,
            cleanupToVersion,
            eventsMayBeWritten: false,
            $"Commit failed for stream '{document.Active.StreamIdentifier}'. " +
            $"Automatic cleanup removed {removed} partial events. Safe to retry.",
            originalEx);
    }

    private async Task MarkStreamAsBrokenAsync(
        Exception originalEx, Exception cleanupEx, int cleanupFromVersion, int cleanupToVersion, CancellationToken cancellationToken)
    {
        document.Active.IsBroken = true;
        document.Active.BrokenInfo = new BrokenStreamInfo
        {
            BrokenAt = DateTimeOffset.UtcNow,
            OrphanedFromVersion = cleanupFromVersion,
            OrphanedToVersion = cleanupToVersion,
            ErrorMessage = cleanupEx.Message,
            OriginalExceptionType = originalEx.GetType().FullName,
            CleanupExceptionType = cleanupEx.GetType().FullName
        };

        // Try to persist broken state (best effort)
        try { await documentstore.SetAsync(document, cancellationToken: cancellationToken); } catch { /* Log but continue */ }
    }

    private void RecordSuccessMetrics(System.Diagnostics.Activity? activity, System.Diagnostics.Stopwatch? timer, int eventCount)
    {
        if (timer != null)
        {
            var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
            FaesMetrics.RecordCommitDuration(durationMs, document.ObjectName, FaesSemanticConventions.StorageProviderBlob);
        }
        FaesMetrics.RecordEventsAppended(eventCount, document.ObjectName, FaesSemanticConventions.StorageProviderBlob);
        FaesMetrics.RecordEventsPerCommit(eventCount, document.ObjectName);
        FaesMetrics.RecordCommit(document.ObjectName, FaesSemanticConventions.StorageProviderBlob, success: true);
        activity?.SetTag(FaesSemanticConventions.Success, true);
    }

    /// <summary>
    /// Tracks commit operation state for exception handling context.
    /// </summary>
    private sealed class CommitState
    {
        /// <summary>
        /// Gets or sets a value indicating whether event append has started.
        /// This is set to true after document update succeeds, before events are appended.
        /// </summary>
        public bool EventsAppendStarted { get; set; }
    }

    /// <summary>
    /// Commits events without chunking support.
    /// </summary>
    /// <param name="state">State object to track commit progress.</param>
    private async Task CommitWithoutChunkingAsync(CommitState state)
    {
        // IMPORTANT: Update document metadata FIRST, then append events.
        // This order ensures the document's ETag provides optimistic concurrency
        // control - if another writer updated the document, this will fail with
        // a concurrency exception before any events are written.
        await documentstore.SetAsync(document);

        // Document update succeeded - mark that events will be appended
        // This flag is used for exception context if AppendAsync fails
        state.EventsAppendStarted = true;

        await datastore.AppendAsync(document, default, [.. Buffer]);
    }

    /// <summary>
    /// Commits events with chunking support.
    /// </summary>
    /// <param name="state">State object to track commit progress.</param>
    private async Task CommitWithChunkingAsync(CommitState state)
    {
        // Default chunk size: 1000 events per chunk
        // Rationale:
        // - Keeps blob chunks at ~1-5MB (assuming ~1-5KB per event with JSON payload)
        // - Fast read-modify-write operations for append-heavy workloads
        // - Most streams have <1000 events and don't need chunking at all
        // - Balances chunk management overhead vs individual chunk size
        // - Matches defaults in EventStreamBlobSettings and EventStreamTableSettings
        int rowsPerPartition = document.Active.ChunkSettings?.ChunkSize ?? 1000;
        int latestEventIndex = 0;

        while (Buffer.Count > 0)
        {
            int chunkIdentifier = GetCurrentChunkIdentifier();
            var availableSpaceInCurrentPartition = DeterminateAvailableSpaceInChunk(rowsPerPartition, ref latestEventIndex);

            // Ensure we don't try to take more events than available in the buffer
            var eventsToTake = Math.Min(availableSpaceInCurrentPartition, Buffer.Count);
            var eventsToAdd = Buffer.GetRange(0, eventsToTake);
            Buffer.RemoveRange(0, eventsToTake);

            if (eventsToAdd.Count > 0)
            {
                await AppendEventsToCurrentChunkAsync(eventsToAdd, state);
            }

            await CreateChunkIfNeededAsync(chunkIdentifier, eventsToAdd, rowsPerPartition, latestEventIndex);
        }
    }

    private int GetCurrentChunkIdentifier()
    {
        return document.Active.StreamChunks.Count > 0
            ? document.Active.StreamChunks[^1].ChunkIdentifier
            : 0;
    }

    /// <summary>
    /// Appends events to the current chunk.
    /// </summary>
    /// <param name="eventsToAdd">Events to append.</param>
    /// <param name="state">State object to track commit progress.</param>
    private async Task AppendEventsToCurrentChunkAsync(List<JsonEvent> eventsToAdd, CommitState state)
    {
        if (document.Active.StreamChunks.Count == 0)
        {
            await CreateNewChunk(1, eventsToAdd[^1].EventVersion);
        }

        var lastChunk = document.Active.StreamChunks[^1];
        lastChunk.LastEventVersion = eventsToAdd[^1].EventVersion;

        // IMPORTANT: Update document metadata FIRST, then append events.
        // This order ensures the document's ETag provides optimistic concurrency
        // control - if another writer updated the document, this will fail with
        // a concurrency exception before any events are written.
        await documentstore.SetAsync(document);

        // Document update succeeded - mark that events will be appended
        state.EventsAppendStarted = true;

        await datastore.AppendAsync(document, default, [.. eventsToAdd]);
    }

    private async Task CreateChunkIfNeededAsync(
        int chunkIdentifier,
        List<JsonEvent> eventsToAdd,
        int rowsPerPartition,
        int latestEventIndex)
    {
        if (Buffer.Count > 0 || IsCurrentChunkFull(rowsPerPartition, latestEventIndex))
        {
            await CreateNewChunk(chunkIdentifier, eventsToAdd[^1].EventVersion);
        }
    }

    private bool IsCurrentChunkFull(int rowsPerPartition, int latestEventIndex)
    {
        var availableSpaceInLastPartition = DeterminateAvailableSpaceInChunk(rowsPerPartition, ref latestEventIndex);
        return availableSpaceInLastPartition == 0 &&
               document.Active.StreamChunks[^1].LastEventVersion == latestEventIndex;
    }

    private async Task ExecutePostCommitActionsAsync(List<JsonEvent> allEvents)
    {
        if (postCommitActions.Count > 0)
        {
            using var postCommitActivity = FaesInstrumentation.Core.StartActivity("Session.PostCommitActions");
            postCommitActivity?.SetTag(FaesSemanticConventions.StreamId, document.Active.StreamIdentifier);
            postCommitActivity?.SetTag(FaesSemanticConventions.EventCount, allEvents.Count);

            foreach (var action in postCommitActions)
            {
                postCommitActivity?.SetTag(FaesSemanticConventions.ActionType, action.GetType().FullName);
                await action.PostCommitAsync(allEvents, document);
            }
        }
    }

    private int DeterminateAvailableSpaceInChunk(int rowsPerPartition, ref int latestEventIndex)
    {
        if (document.Active.StreamChunks != null && document.Active.StreamChunks.Count > 0)
        {
            var lastChunk = document.Active.StreamChunks[^1];
            if (lastChunk.LastEventVersion.HasValue)
            {
                latestEventIndex = lastChunk.LastEventVersion.Value;
            }
        }
        int positionInCurrentPartition = (latestEventIndex + 1) -
                                         ((latestEventIndex / rowsPerPartition) * rowsPerPartition);

        int availableSpaceInCurrentPartition = rowsPerPartition - positionInCurrentPartition;

         return availableSpaceInCurrentPartition;
    }

    private async Task CreateNewChunk(int chunkIdentifier, int lastVersion)
    {
        StreamChunk? lastChunk = null;
        if (document.Active.StreamChunks.Count > 0)
        {
            // close up the last one
            lastChunk = document.Active.StreamChunks[^1];
            lastChunk.LastEventVersion = lastVersion;
        }

        // Create a new chunk, there is still data left.
        document.Active.StreamChunks.Add(new StreamChunk
        {
            ChunkIdentifier = chunkIdentifier + 1,
            FirstEventVersion = lastVersion + 1,
            LastEventVersion = lastVersion,
        });

        await documentstore.SetAsync(document);

        // Notify listeners of closed documents
        if (lastChunk != null)
        {
            foreach (var cAction in docClosedNotificationActions)
            {
                await cAction.StreamDocumentChunkClosed()(eventStream, lastChunk.ChunkIdentifier);
            }
        }
    }

    /// <summary>
    /// Checks if a stream is terminated (has reached a terminal state).
    /// </summary>
    /// <param name="streamIdentifier">The identifier of the stream to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the stream is terminated, otherwise false.</returns>
    public Task<bool> IsTerminatedAsync(string streamIdentifier, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(document.TerminatedStreams
            .Find(ts => ts.StreamIdentifier == streamIdentifier) != null);
    }

    /// <summary>
    /// Reads events from the stream within the specified version range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The ending version (inclusive). If null, reads to the latest version.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The collection of events, or null if none found.</returns>
    public Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null, CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Core.StartActivity("Session.Read");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.StreamId, document.Active.StreamIdentifier);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.StartVersion, startVersion);
            if (untilVersion.HasValue)
            {
                activity.SetTag(FaesSemanticConventions.TargetVersion, untilVersion.Value);
            }
        }

        return datastore.ReadAsync(document, startVersion, untilVersion, cancellationToken: cancellationToken);
    }
}
