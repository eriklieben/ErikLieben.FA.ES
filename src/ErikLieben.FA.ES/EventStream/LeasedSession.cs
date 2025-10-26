using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Notifications;
using System.Diagnostics;
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
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

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
        using var activity = ActivitySource.StartActivity($"Session.{nameof(Append)}");
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
    /// <returns>A task representing the asynchronous commit operation.</returns>
    public async Task CommitAsync()
    {
        using var activity = ActivitySource.StartActivity($"Session.{nameof(CommitAsync)}");
        var allEvents = Buffer.ToList();
        activity?.SetTag("EventCount", Buffer.Count);

        try
        {
            if (!document.Active.ChunkingEnabled())
            {
                await documentstore.SetAsync(document);
                await datastore.AppendAsync(document, [.. Buffer]);
            }
            else
            {
                int rowsPerPartition = document.Active.ChunkSettings?.ChunkSize ?? 1000; // TODO: what to set as default
                int latestEventIndex = 0;
                int chunkIdentifier = 0;
                while (Buffer.Count > 0)
                {
                    if (document.Active.StreamChunks.Count > 0)
                    {
                        chunkIdentifier = document.Active.StreamChunks[^1].ChunkIdentifier;
                    }

                    var availableSpaceInCurrentPartition =
                        DeterminateAvailableSpaceInChunk(rowsPerPartition, ref latestEventIndex);

                    var eventsToAdd = Buffer.Take(availableSpaceInCurrentPartition).ToList();
                    Buffer = Buffer.Except(eventsToAdd).ToList();

                    if (eventsToAdd.Count > 0)
                    {
                        if (document.Active.StreamChunks.Count == 0)
                        {
                            await CreateNewChunk(
                                1,
                                eventsToAdd[^1].EventVersion);
                        }

                        var lastChunk = document.Active.StreamChunks[^1];
                        lastChunk.LastEventVersion = eventsToAdd[^1].EventVersion;

                        await documentstore.SetAsync(document);
                        await datastore.AppendAsync(document, [.. eventsToAdd]);
                    }

                    if (Buffer.Count > 0)
                    {
                        await CreateNewChunk(
                            chunkIdentifier,
                            eventsToAdd[^1].EventVersion);

                    } else {

                        var availableSpaceInLastPartition =
                            DeterminateAvailableSpaceInChunk(rowsPerPartition, ref latestEventIndex);

                        if (availableSpaceInLastPartition == 0 && document.Active.StreamChunks[^1].LastEventVersion == latestEventIndex)
                        {
                            await CreateNewChunk(
                                chunkIdentifier,
                                eventsToAdd[^1].EventVersion);
                        }
                    }

                }
            }
        }
        catch (Exception)
        {
            // TODO: Implement rollback functionality
            // When rollback is implemented, this should:
            // 1. Call dataStore.RemoveAsync(document, Events.ToArray()) to remove appended events
            // 2. Rollback the version number
            // 3. Wrap any rollback failures in StreamInIncorrectStateException

            throw;
        }

        // ACTIONS
        if (postCommitActions.Count > 0)
        {
            using var postCommitActivity = ActivitySource.StartActivity("Session.PostCommitActions");
            foreach (var action in postCommitActions)
            {
                postCommitActivity?.SetTag("Action", action.GetType().FullName);
                await action.PostCommitAsync(allEvents, document);
            }
        }

        // Clear the buffer here?
        Buffer.Clear();
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
        // int positionInCurrentPartition = (latestEventIndex + 1) % rowsPerPartition;
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
    /// <returns>True if the stream is terminated, otherwise false.</returns>
    public Task<bool> IsTerminatedASync(string streamIdentifier)
    {
        return Task.FromResult(document.TerminatedStreams
            .Find(ts => ts.StreamIdentifier == streamIdentifier) != null);
    }

    /// <summary>
    /// Reads events from the stream within the specified version range.
    /// </summary>
    /// <param name="startVersion">The starting version (inclusive). Defaults to 0.</param>
    /// <param name="untilVersion">The ending version (inclusive). If null, reads to the latest version.</param>
    /// <returns>The collection of events, or null if none found.</returns>
    public Task<IEnumerable<IEvent>?> ReadAsync(int startVersion = 0, int? untilVersion = null)
    {
        using var activity = ActivitySource.StartActivity("Session.ReadAsync");
        return datastore.ReadAsync(document, startVersion, untilVersion);
    }
}
