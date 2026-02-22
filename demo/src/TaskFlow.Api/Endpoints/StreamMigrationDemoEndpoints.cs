using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Core;
using ErikLieben.FA.ES.EventStreamManagement.LiveMigration;
using TaskFlow.Api.Hubs;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Demo endpoints for stream migration functionality.
/// This demonstrates the 5-phase cutover strategy for event stream migrations.
/// </summary>
public static class StreamMigrationDemoEndpoints
{
    // In-memory storage for demo migrations (in real app, this would be persisted)
    private static readonly ConcurrentDictionary<string, MigrationDemoState> _activeMigrations = new();

    // Supported object types for migration
    private static readonly string[] SupportedObjectTypes = ["workitem", "project", "userprofile"];

    // Well-known demo streams that are guaranteed to exist after seeding
    private static readonly (string Id, string Type, string Name)[] WellKnownDemoStreams =
    [
        (DemoProjectIds.Legacy.CustomerPortalRedesign, "project", "Customer Portal Redesign (Legacy)"),
        (DemoProjectIds.Legacy.MarketingAutomationTool, "project", "Marketing Automation Tool (Legacy)"),
        (DemoProjectIds.NewEvents.MobileBankingApp, "project", "Mobile Banking App"),
        (DemoProjectIds.SchemaVersioning.DevOpsPipelineModernization, "project", "DevOps Pipeline Modernization"),
    ];

    public static RouteGroupBuilder MapStreamMigrationDemoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/migration")
            .WithTags("Stream Migration Demo")
            .WithDescription("Demo endpoints for stream migration with replication, tailing, and cutover");

        group.MapGet("/streams", GetAvailableStreams)
            .WithName("GetAvailableStreams")
            .WithSummary("Get available streams for migration demo");

        group.MapGet("/streams/{objectType}/{objectId}/events", GetStreamEvents)
            .WithName("GetStreamEvents")
            .WithSummary("Get events for a specific stream");

        group.MapGet("/streams/{objectType}/{objectId}/target-events/{streamIdentifier}", GetTargetStreamEvents)
            .WithName("GetTargetStreamEvents")
            .WithSummary("Get events for a specific target stream identifier (used during migration)");

        group.MapPost("/streams/{objectType}/{objectId}/live-event", AddLiveEventToStream)
            .WithName("AddLiveEventToStream")
            .WithSummary("Add a live event directly to a stream (used during live migration)");

        group.MapPost("/start", StartMigrationDemo)
            .WithName("StartMigrationDemo")
            .WithSummary("Start a new migration demo");

        group.MapGet("/{migrationId}/status", GetMigrationStatus)
            .WithName("GetMigrationStatus")
            .WithSummary("Get current migration status");

        group.MapPost("/{migrationId}/advance", AdvancePhase)
            .WithName("AdvanceMigrationPhase")
            .WithSummary("Advance migration to next phase");

        group.MapPost("/{migrationId}/live-event", AddLiveEvent)
            .WithName("AddLiveEventDuringMigration")
            .WithSummary("Add a live event during migration (demonstrates replication)");

        group.MapPost("/{migrationId}/reset", ResetMigration)
            .WithName("ResetMigrationDemo")
            .WithSummary("Reset the migration demo");

        group.MapGet("/transformations", GetTransformationRules)
            .WithName("GetTransformationRules")
            .WithSummary("Get transformation rules for the demo");

        group.MapPost("/execute", ExecuteRealMigration)
            .WithName("ExecuteRealMigration")
            .WithSummary("Execute an actual stream migration using IEventStreamMigrationService");

        group.MapPost("/execute-live", ExecuteLiveMigration)
            .WithName("ExecuteLiveMigration")
            .WithSummary("Execute a live migration with real-time SignalR progress");

        return group;
    }

    private static async Task<IResult> GetAvailableStreams(
        [FromServices] IObjectIdProvider objectIdProvider,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory)
    {
        try
        {
            var streams = new List<object>();
            var addedIds = new HashSet<string>();

            // First, add well-known demo streams (these are guaranteed to exist after seeding)
            foreach (var (streamId, streamType, streamName) in WellKnownDemoStreams)
            {
                try
                {
                    var document = await objectDocumentFactory.GetAsync(streamType, streamId);
                    var eventStream = eventStreamFactory.Create(document);
                    var events = await eventStream.ReadAsync();

                    streams.Add(new
                    {
                        id = streamId,
                        type = streamType,
                        name = streamName,
                        eventCount = events.Count,
                        isDemo = true
                    });
                    addedIds.Add(streamId);
                }
                catch
                {
                    // Demo stream doesn't exist yet (data not seeded)
                    streams.Add(new
                    {
                        id = streamId,
                        type = streamType,
                        name = $"{streamName} (not seeded)",
                        eventCount = 0,
                        isDemo = true
                    });
                    addedIds.Add(streamId);
                }
            }

            // Then query additional real streams from Azure Blob storage
            foreach (var objectType in SupportedObjectTypes)
            {
                try
                {
                    var result = await objectIdProvider.GetObjectIdsAsync(objectType, null, 20);

                    foreach (var objectId in result.Items)
                    {
                        // Skip if already added as a well-known demo stream
                        if (addedIds.Contains(objectId))
                            continue;

                        try
                        {
                            var document = await objectDocumentFactory.GetAsync(objectType, objectId);
                            var eventStream = eventStreamFactory.Create(document);
                            var events = await eventStream.ReadAsync();

                            streams.Add(new
                            {
                                id = objectId,
                                type = objectType,
                                name = $"{objectType}/{objectId}",
                                eventCount = events.Count,
                                isDemo = false
                            });
                            addedIds.Add(objectId);
                        }
                        catch
                        {
                            // Skip streams that can't be read
                        }
                    }
                }
                catch
                {
                    // Skip object types that fail to query
                }
            }

            return Results.Ok(new { streams });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get available streams",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetStreamEvents(
        string objectType,
        string objectId,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory)
    {
        try
        {
            var document = await objectDocumentFactory.GetAsync(objectType, objectId);
            var eventStream = eventStreamFactory.Create(document);
            var events = await eventStream.ReadAsync();

            var eventDtos = new List<object>();
            var version = 1;

            foreach (var evt in events)
            {
                // Parse the JSON payload into a dictionary for display
                var data = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(evt.Payload))
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(evt.Payload);
                        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                        {
                            data[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString() ?? "",
                                JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => prop.Value.ToString()
                            };
                        }
                    }
                    catch
                    {
                        data["_rawPayload"] = evt.Payload;
                    }
                }

                eventDtos.Add(new
                {
                    id = $"e{version}",
                    version,
                    type = evt.EventType,
                    timestamp = evt.ActionMetadata?.EventOccuredAt?.DateTime ?? DateTime.UtcNow,
                    data,
                    schemaVersion = evt.SchemaVersion,
                    isLiveEvent = false,
                    writtenTo = new List<string> { "source" }
                });
                version++;
            }

            return Results.Ok(new
            {
                streamId = objectId,
                streamType = objectType,
                events = eventDtos,
                totalEvents = eventDtos.Count
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get stream events",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Gets events from a specific stream identifier (used during migration to read target stream).
    /// This reads directly from the blob stream, bypassing the object document.
    /// </summary>
    private static async Task<IResult> GetTargetStreamEvents(
        string objectType,
        string objectId,
        string streamIdentifier,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IDataStore dataStore)
    {
        try
        {
            // Get the object document to get stream configuration
            var document = await objectDocumentFactory.GetAsync(objectType, objectId);

            // Create a temporary document pointing to the target stream identifier
            var targetStreamInfo = new StreamInformation
            {
                StreamIdentifier = streamIdentifier,
                StreamType = document.Active.StreamType,
                DocumentTagType = document.Active.DocumentTagType,
                CurrentStreamVersion = -1,
                StreamConnectionName = document.Active.StreamConnectionName,
                DocumentTagConnectionName = document.Active.DocumentTagConnectionName,
                StreamTagConnectionName = document.Active.StreamTagConnectionName,
                SnapShotConnectionName = document.Active.SnapShotConnectionName,
                ChunkSettings = document.Active.ChunkSettings,
                StreamChunks = [],
                SnapShots = [],
                DocumentType = document.Active.DocumentType,
                EventStreamTagType = document.Active.EventStreamTagType,
                DocumentRefType = document.Active.DocumentRefType,
                DataStore = document.Active.DataStore,
                DocumentStore = document.Active.DocumentStore,
                DocumentTagStore = document.Active.DocumentTagStore,
                StreamTagStore = document.Active.StreamTagStore,
                SnapShotStore = document.Active.SnapShotStore
            };

            var targetDocument = new MigrationTargetDocument(
                document.ObjectId,
                document.ObjectName,
                targetStreamInfo);

            // Read events from the target stream
            var events = await dataStore.ReadAsync(targetDocument, startVersion: 0, untilVersion: null, chunk: null);
            var eventList = events?.ToList() ?? [];

            var eventDtos = new List<object>();
            var version = 1;

            foreach (var evt in eventList)
            {
                // Parse the JSON payload into a dictionary for display
                var data = ParseEventPayload(evt.Payload);

                eventDtos.Add(new
                {
                    id = $"e{version}",
                    version,
                    type = evt.EventType,
                    timestamp = evt.ActionMetadata?.EventOccuredAt?.DateTime ?? DateTime.UtcNow,
                    data,
                    schemaVersion = evt.SchemaVersion,
                    isLiveEvent = false,
                    writtenTo = new List<string> { "target" }
                });
                version++;
            }

            return Results.Ok(new
            {
                streamId = objectId,
                streamType = objectType,
                streamIdentifier,
                events = eventDtos,
                totalEvents = eventDtos.Count
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get target stream events",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Adds a demo note to a stream.
    /// Uses the aggregate's AddDemoNote command which is always allowed regardless of aggregate state.
    /// Used during live migration demos to show events being caught up.
    /// </summary>
    private static async Task<IResult> AddLiveEventToStream(
        string objectType,
        string objectId,
        [FromBody] AddLiveEventToStreamRequest request,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IObjectDocumentFactory objectDocumentFactory)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Currently only support project streams
            if (objectType != "project")
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = "Live events are currently only supported for project streams"
                });
            }

            // Get the note text from the request, or generate a default one
            var noteText = request.EventData.TryGetValue("note", out var note)
                ? note?.ToString() ?? $"Demo note added at {now:HH:mm:ss}"
                : request.EventData.TryGetValue("description", out var desc)
                    ? desc?.ToString() ?? $"Demo note added at {now:HH:mm:ss}"
                    : $"Demo note added at {now:HH:mm:ss}";

            // Load the project aggregate
            var projectId = ProjectId.From(objectId);
            var project = await projectFactory.GetAsync(projectId);
            var userId = UserProfileId.From("migration-demo-user");

            // Use the AddDemoNote command which is always allowed (regardless of project state)
            var result = await project.AddDemoNote(noteText, userId, null, now);

            if (result.IsFailure)
            {
                return Results.BadRequest(new
                {
                    success = false,
                    message = $"Failed to add demo note: {string.Join(", ", result.Errors.ToArray().Select(e => e.Message))}"
                });
            }

            // Get the updated event count
            var document = await objectDocumentFactory.GetAsync(objectType, objectId);
            var eventStream = eventStreamFactory.Create(document);
            var events = await eventStream.ReadAsync();
            var newVersion = events.Count;

            return Results.Ok(new
            {
                success = true,
                eventVersion = newVersion,
                eventType = "Project.DemoNoteAdded",
                schemaVersion = 1,
                message = $"Demo note added to stream (v{newVersion}): {noteText}"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to add live event",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private class AddLiveEventToStreamRequest
    {
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object> EventData { get; set; } = new();
    }

    private static async Task<IResult> StartMigrationDemo(
        [FromBody] StartMigrationRequest request,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory)
    {
        try
        {
            var migrationId = Guid.NewGuid().ToString();

            // Read real events from Azure Blob storage
            var sourceEvents = new List<StreamEventDto>();
            IObjectDocument? sourceDocument = null;

            try
            {
                sourceDocument = await objectDocumentFactory.GetAsync(request.SourceStreamType, request.SourceStreamId);
                var eventStream = eventStreamFactory.Create(sourceDocument);
                var events = await eventStream.ReadAsync();

                var version = 1;
                foreach (var evt in events)
                {
                    // Parse the JSON payload into a dictionary for display
                    var data = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(evt.Payload))
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(evt.Payload);
                            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                            {
                                data[prop.Name] = prop.Value.ValueKind switch
                                {
                                    JsonValueKind.String => prop.Value.GetString() ?? "",
                                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => prop.Value.ToString()
                                };
                            }
                        }
                        catch
                        {
                            data["_rawPayload"] = evt.Payload;
                        }
                    }

                    sourceEvents.Add(new StreamEventDto
                    {
                        Id = $"e{version}",
                        Version = version,
                        Type = evt.EventType,
                        Timestamp = evt.ActionMetadata?.EventOccuredAt?.DateTime ?? DateTime.UtcNow,
                        Data = data,
                        SchemaVersion = evt.SchemaVersion,
                        IsLiveEvent = false,
                        WrittenTo = new List<string> { "source" }
                    });
                    version++;
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to read source stream",
                    detail: $"Could not read events from {request.SourceStreamType}/{request.SourceStreamId}: {ex.Message}",
                    statusCode: 400
                );
            }

            if (sourceEvents.Count == 0)
            {
                return Results.Problem(
                    title: "Empty source stream",
                    detail: $"No events found in {request.SourceStreamType}/{request.SourceStreamId}",
                    statusCode: 400
                );
            }

            // Calculate target stream identifier
            var currentStreamId = sourceDocument!.Active.StreamIdentifier;
            var streamNumber = 0;
            if (currentStreamId.Contains('-'))
            {
                var parts = currentStreamId.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                {
                    streamNumber = num + 1;
                }
            }
            var objectIdWithoutDashes = request.SourceStreamId.Replace("-", string.Empty);
            var targetStreamIdentifier = $"{objectIdWithoutDashes}-{streamNumber:D10}";

            var state = new MigrationDemoState
            {
                MigrationId = migrationId,
                Phase = "normal",
                SourceStreamId = request.SourceStreamId,
                SourceStreamType = request.SourceStreamType,
                TargetStreamId = $"{request.SourceStreamId}-migrated",
                TargetStreamIdentifier = targetStreamIdentifier,
                SourceEvents = sourceEvents,
                TargetEvents = new List<StreamEventDto>(),
                EventsProcessed = 0,
                TotalEvents = sourceEvents.Count,
                Progress = 0,
                LiveEventCounter = 0,
                SourceDocument = sourceDocument,
                LastSourceVersion = sourceEvents.Count
            };

            _activeMigrations[migrationId] = state;

            return Results.Ok(new
            {
                success = true,
                migrationId,
                message = $"Migration started for {request.SourceStreamType}/{request.SourceStreamId} with {sourceEvents.Count} events. Target stream: {targetStreamIdentifier}",
                initialState = MapToResponse(state)
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to start migration",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static Task<IResult> GetMigrationStatus(string migrationId)
    {
        if (!_activeMigrations.TryGetValue(migrationId, out var state))
        {
            return Task.FromResult(Results.NotFound(new { message = "Migration not found" }));
        }

        return Task.FromResult(Results.Ok(new
        {
            migrationId = state.MigrationId,
            phase = state.Phase,
            eventsProcessed = state.EventsProcessed,
            totalEvents = state.TotalEvents,
            progress = state.Progress,
            sourceEvents = state.SourceEvents,
            targetEvents = state.TargetEvents
        }));
    }

    private static async Task<IResult> AdvancePhase(
        string migrationId,
        [FromServices] IDataStore dataStore,
        [FromServices] IDocumentStore documentStore,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IObjectDocumentFactory objectDocumentFactory)
    {
        if (!_activeMigrations.TryGetValue(migrationId, out var state))
        {
            return Results.NotFound(new { message = "Migration not found" });
        }

        // Advance through phases
        switch (state.Phase)
        {
            case "normal":
                state.Phase = "replicating";

                // Actually copy events to target stream in Azure Blob
                if (state.SourceDocument != null && state.TargetStreamIdentifier != null)
                {
                    try
                    {
                        // Read source events
                        var sourceEventStream = eventStreamFactory.Create(state.SourceDocument);
                        var sourceEvents = await sourceEventStream.ReadAsync();

                        if (sourceEvents.Any())
                        {
                            // Create target stream info
                            var targetStreamInfo = new StreamInformation
                            {
                                StreamIdentifier = state.TargetStreamIdentifier,
                                StreamType = state.SourceDocument.Active.StreamType,
                                DocumentTagType = state.SourceDocument.Active.DocumentTagType,
                                CurrentStreamVersion = -1,
                                StreamConnectionName = state.SourceDocument.Active.StreamConnectionName,
                                DocumentTagConnectionName = state.SourceDocument.Active.DocumentTagConnectionName,
                                StreamTagConnectionName = state.SourceDocument.Active.StreamTagConnectionName,
                                SnapShotConnectionName = state.SourceDocument.Active.SnapShotConnectionName,
                                ChunkSettings = state.SourceDocument.Active.ChunkSettings,
                                StreamChunks = [],
                                SnapShots = [],
                                DocumentType = state.SourceDocument.Active.DocumentType,
                                EventStreamTagType = state.SourceDocument.Active.EventStreamTagType,
                                DocumentRefType = state.SourceDocument.Active.DocumentRefType,
                                DataStore = state.SourceDocument.Active.DataStore,
                                DocumentStore = state.SourceDocument.Active.DocumentStore,
                                DocumentTagStore = state.SourceDocument.Active.DocumentTagStore,
                                StreamTagStore = state.SourceDocument.Active.StreamTagStore,
                                SnapShotStore = state.SourceDocument.Active.SnapShotStore
                            };

                            // Create a temporary target document for writing
                            state.TargetDocument = new MigrationTargetDocument(
                                state.SourceDocument.ObjectId,
                                state.SourceDocument.ObjectName,
                                targetStreamInfo);

                            // Write all events to the target stream (preserve original timestamps)
                            await dataStore.AppendAsync(state.TargetDocument, preserveTimestamp: true, CancellationToken.None, sourceEvents.ToArray());

                            state.LastSourceVersion = sourceEvents.Count;
                        }

                        // Update in-memory state for UI
                        ProcessEventsToTarget(state);
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(
                            title: "Failed to copy events to target stream",
                            detail: ex.Message,
                            statusCode: 500
                        );
                    }
                }
                else
                {
                    // Fallback to in-memory processing
                    ProcessEventsToTarget(state);
                }
                break;

            case "replicating":
                // Catch up any events added during replication before moving to tailing
                await CatchUpEventsToTarget(state, eventStreamFactory, dataStore);
                state.Phase = "tailing";
                break;

            case "tailing":
                // Catch up any events added during tailing before cutover
                await CatchUpEventsToTarget(state, eventStreamFactory, dataStore);
                state.Phase = "cutover";
                break;

            case "cutover":
                // Perform the actual cutover - update object document
                if (state.SourceDocument != null && state.TargetStreamIdentifier != null && state.TargetDocument != null)
                {
                    try
                    {
                        // Refresh the source document to get the latest hash (it may have changed due to live events)
                        var freshSourceDocument = await objectDocumentFactory.GetAsync(state.SourceStreamType, state.SourceStreamId);
                        state.SourceDocument = freshSourceDocument;

                        // Final catch-up loop: keep catching up until no new events arrive
                        int catchUpIterations = 0;
                        const int maxIterations = 10; // Safety limit

                        while (catchUpIterations < maxIterations)
                        {
                            var previousVersion = state.LastSourceVersion;
                            await CatchUpEventsToTarget(state, eventStreamFactory, dataStore);

                            if (state.LastSourceVersion > previousVersion)
                            {
                                // New events were caught up, check again
                                catchUpIterations++;
                            }
                            else
                            {
                                // No new events - we're caught up
                                break;
                            }
                        }

                        // Add EventStream.Closed event to source stream to mark it as terminated
                        // NOTE: This event should NOT be copied to target - it marks the source as closed
                        // The data store's AppendAsync should check for this event and throw if present
                        var closedEvent = new JsonEvent
                        {
                            EventType = "EventStream.Closed",
                            EventVersion = state.LastSourceVersion + 1,
                            SchemaVersion = 1,
                            Payload = JsonSerializer.Serialize(new
                            {
                                reason = "Migration completed",
                                continuationStream = state.TargetStreamIdentifier,
                                closedAt = DateTimeOffset.UtcNow
                            }),
                            ActionMetadata = new ActionMetadata
                            {
                                EventOccuredAt = DateTimeOffset.UtcNow
                            }
                        };
                        await dataStore.AppendAsync(state.SourceDocument, CancellationToken.None, closedEvent);

                        // Create terminated stream entry for source
                        var terminatedStream = new TerminatedStream
                        {
                            StreamIdentifier = state.SourceDocument.Active.StreamIdentifier,
                            StreamType = state.SourceDocument.Active.StreamType,
                            StreamConnectionName = state.SourceDocument.Active.StreamConnectionName,
                            Reason = $"Migrated to {state.TargetStreamIdentifier}",
                            ContinuationStreamId = state.TargetStreamIdentifier,
                            TerminationDate = DateTimeOffset.UtcNow,
                            StreamVersion = state.SourceDocument.Active.CurrentStreamVersion,
                            Deleted = false
                        };

                        // Create new active stream pointing to target
                        var newActiveStream = new StreamInformation
                        {
                            StreamIdentifier = state.TargetStreamIdentifier,
                            StreamType = state.SourceDocument.Active.StreamType,
                            DocumentTagType = state.SourceDocument.Active.DocumentTagType,
                            CurrentStreamVersion = state.LastSourceVersion,
                            StreamConnectionName = state.SourceDocument.Active.StreamConnectionName,
                            DocumentTagConnectionName = state.SourceDocument.Active.DocumentTagConnectionName,
                            StreamTagConnectionName = state.SourceDocument.Active.StreamTagConnectionName,
                            SnapShotConnectionName = state.SourceDocument.Active.SnapShotConnectionName,
                            ChunkSettings = state.SourceDocument.Active.ChunkSettings,
                            StreamChunks = [],
                            SnapShots = [],
                            DocumentType = state.SourceDocument.Active.DocumentType,
                            EventStreamTagType = state.SourceDocument.Active.EventStreamTagType,
                            DocumentRefType = state.SourceDocument.Active.DocumentRefType,
                            DataStore = state.SourceDocument.Active.DataStore,
                            DocumentStore = state.SourceDocument.Active.DocumentStore,
                            DocumentTagStore = state.SourceDocument.Active.DocumentTagStore,
                            StreamTagStore = state.SourceDocument.Active.StreamTagStore,
                            SnapShotStore = state.SourceDocument.Active.SnapShotStore
                        };

                        // Build new terminated streams list
                        var newTerminatedStreams = state.SourceDocument.TerminatedStreams.ToList();
                        newTerminatedStreams.Add(terminatedStream);

                        // Create updated document
                        var updatedDocument = new MigrationCutoverDocument(
                            state.SourceDocument.ObjectId,
                            state.SourceDocument.ObjectName,
                            newActiveStream,
                            newTerminatedStreams,
                            state.SourceDocument.SchemaVersion,
                            state.SourceDocument.Hash,
                            state.SourceDocument.PrevHash);

                        // Save the updated document
                        await documentStore.SetAsync(updatedDocument);

                        state.Phase = "complete";
                        state.Progress = 100;
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(
                            title: "Failed to perform cutover",
                            detail: ex.Message,
                            statusCode: 500
                        );
                    }
                }
                else
                {
                    state.Phase = "complete";
                    state.Progress = 100;
                }
                break;
        }

        return Results.Ok(new
        {
            migrationId = state.MigrationId,
            phase = state.Phase,
            eventsProcessed = state.EventsProcessed,
            totalEvents = state.TotalEvents,
            progress = state.Progress,
            sourceEvents = state.SourceEvents,
            targetEvents = state.TargetEvents,
            targetStreamIdentifier = state.TargetStreamIdentifier
        });
    }

    /// <summary>
    /// Adds a live event by executing a real aggregate command.
    /// This demonstrates the full flow: aggregate action → event stream → blob storage.
    /// The migration manager catches up events from source to target.
    /// </summary>
    private static async Task<IResult> AddLiveEvent(
        string migrationId,
        [FromBody] AddLiveEventRequest request,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IDataStore dataStore)
    {
        if (!_activeMigrations.TryGetValue(migrationId, out var state))
        {
            return Results.NotFound(new { message = "Migration not found" });
        }

        // Only support project streams for now
        if (state.SourceStreamType != "project")
        {
            return Results.BadRequest(new { message = "Live events are currently only supported for project streams" });
        }

        state.LiveEventCounter++;
        var eventId = $"live-{state.LiveEventCounter}";
        var now = DateTime.UtcNow;

        StreamEventDto? sourceEvent = null;
        StreamEventDto? targetEvent = null;
        string message;
        string actualEventType;

        try
        {
            // Load the project aggregate and execute a real command
            var projectId = ProjectId.From(state.SourceStreamId);
            var project = await projectFactory.GetAsync(projectId);
            var userId = UserProfileId.From("migration-demo-user");

            // Execute a real aggregate command based on the requested event type
            // This writes to the active event stream (source before cutover, target after)
            switch (request.EventType)
            {
                case "Project.ScopeRefined":
                    var newDescription = request.EventData.TryGetValue("description", out var desc)
                        ? desc?.ToString() ?? $"Updated scope at {now:HH:mm:ss}"
                        : $"Scope refined during migration demo at {now:HH:mm:ss}";
                    var result = await project.RefineScope(newDescription, userId, null, now);
                    if (result.IsFailure)
                    {
                        return Results.BadRequest(new { message = $"Failed to refine scope: {string.Join(", ", result.Errors.ToArray().Select(e => e.Message))}" });
                    }
                    actualEventType = "Project.ScopeRefined";
                    break;

                case "Project.LanguagesConfigured":
                    var languages = new[] { "en-US", "nl-NL" };
                    var langResult = await project.ConfigureLanguages(languages, userId, null, now);
                    if (langResult.IsFailure)
                    {
                        return Results.BadRequest(new { message = $"Failed to configure languages: {string.Join(", ", langResult.Errors.ToArray().Select(e => e.Message))}" });
                    }
                    actualEventType = "Project.LanguagesConfigured";
                    break;

                default:
                    // Default: refine scope with a generic message
                    var defaultResult = await project.RefineScope($"Live event #{state.LiveEventCounter} at {now:HH:mm:ss}", userId, null, now);
                    if (defaultResult.IsFailure)
                    {
                        return Results.BadRequest(new { message = $"Failed to add event: {string.Join(", ", defaultResult.Errors.ToArray().Select(e => e.Message))}" });
                    }
                    actualEventType = "Project.ScopeRefined";
                    break;
            }

            // Re-read the source document to get the updated event count
            var sourceDocument = await eventStreamFactory.Create(state.SourceDocument!).ReadAsync();
            var newEventCount = sourceDocument.Count;

            // Determine where the event was written based on the current phase
            // Before cutover: events go to source stream (aggregate uses active stream from object document)
            // After cutover: events go to target stream (object document was updated at cutover)
            if (state.Phase == "normal" || state.Phase == "replicating")
            {
                sourceEvent = new StreamEventDto
                {
                    Id = eventId,
                    Version = newEventCount,
                    Type = actualEventType,
                    Timestamp = now,
                    Data = request.EventData,
                    SchemaVersion = 2,
                    IsLiveEvent = true,
                    WrittenTo = new List<string> { "source" }
                };
                state.SourceEvents.Add(sourceEvent);
                message = state.Phase == "normal"
                    ? $"Event '{actualEventType}' written to source stream via aggregate (normal operation)"
                    : $"Event '{actualEventType}' written to source stream via aggregate (will be caught up to target)";
            }
            else if (state.Phase == "tailing")
            {
                // During tailing: write to source AND immediately catch up to target
                sourceEvent = new StreamEventDto
                {
                    Id = eventId,
                    Version = newEventCount,
                    Type = actualEventType,
                    Timestamp = now,
                    Data = request.EventData,
                    SchemaVersion = 2,
                    IsLiveEvent = true,
                    WrittenTo = new List<string> { "source", "target" }
                };
                state.SourceEvents.Add(sourceEvent);

                // Also replicate to target (simulating the tailing catch-up)
                targetEvent = new StreamEventDto
                {
                    Id = $"{eventId}-target",
                    Version = state.TargetEvents.Count + 1,
                    Type = actualEventType,
                    Timestamp = now,
                    Data = request.EventData,
                    SchemaVersion = 2,
                    IsLiveEvent = true,
                    WrittenTo = new List<string> { "source", "target" }
                };
                state.TargetEvents.Add(targetEvent);

                // Also write to the actual target blob stream - catch up ALL missing events
                if (state.TargetDocument != null)
                {
                    // Re-read source to get all events since last catch-up
                    var allSourceEvents = await eventStreamFactory.Create(state.SourceDocument!).ReadAsync();
                    var currentSourceCount = allSourceEvents.Count;

                    if (currentSourceCount > state.LastSourceVersion)
                    {
                        // Get all events since last catch-up (not just the last one)
                        var newEvents = allSourceEvents.Skip(state.LastSourceVersion).ToList();
                        if (newEvents.Count > 0)
                        {
                            await dataStore.AppendAsync(state.TargetDocument, preserveTimestamp: true, CancellationToken.None, newEvents.ToArray());
                            state.LastSourceVersion = currentSourceCount;
                        }
                    }
                }

                message = $"Event '{actualEventType}' written to source and caught up to target (tailing)";
            }
            else
            {
                // After cutover, the aggregate writes to the new active stream (target)
                targetEvent = new StreamEventDto
                {
                    Id = eventId,
                    Version = state.TargetEvents.Count + 1,
                    Type = actualEventType,
                    Timestamp = now,
                    Data = request.EventData,
                    SchemaVersion = 2,
                    IsLiveEvent = true,
                    WrittenTo = new List<string> { "target" }
                };
                state.TargetEvents.Add(targetEvent);
                message = $"Event '{actualEventType}' written to target stream via aggregate (migration complete)";
            }

            state.TotalEvents = state.SourceEvents.Count(e => !e.IsLiveEvent);

            return Results.Ok(new
            {
                success = true,
                sourceEvent,
                targetEvent,
                phase = state.Phase,
                message,
                actualEventType
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to execute aggregate command",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static Task<IResult> ResetMigration(string migrationId)
    {
        _activeMigrations.TryRemove(migrationId, out _);
        return Task.FromResult(Results.Ok(new { success = true }));
    }

    private static Task<IResult> GetTransformationRules()
    {
        var rules = new[]
        {
            new
            {
                eventType = "WorkItem.Created",
                fromVersion = 1,
                toVersion = 2,
                changes = new[] { "Added \"priority\" field with default \"medium\"", "Renamed \"status\" to \"initialStatus\"" }
            },
            new
            {
                eventType = "WorkItem.AssigneeChanged",
                fromVersion = 1,
                toVersion = 2,
                changes = new[] { "Renamed \"assignee\" to \"assignedTo\"", "Added \"assignedAt\" timestamp" }
            },
            new
            {
                eventType = "WorkItem.StatusChanged",
                fromVersion = 1,
                toVersion = 2,
                changes = new[] { "Added \"previousStatus\" field", "Added \"changedBy\" field" }
            },
            new
            {
                eventType = "WorkItem.Completed",
                fromVersion = 1,
                toVersion = 2,
                changes = new[] { "Renamed \"completedBy\" to \"resolvedBy\"", "Added \"resolution\" enum field" }
            }
        };

        return Task.FromResult(Results.Ok(rules));
    }

    /// <summary>
    /// Executes an actual stream migration using the IEventStreamMigrationService.
    /// This will create a new stream in Azure Blob storage and update the object document.
    /// </summary>
    private static async Task<IResult> ExecuteRealMigration(
        [FromBody] ExecuteRealMigrationRequest request,
        [FromServices] IEventStreamMigrationService migrationService,
        [FromServices] IObjectDocumentFactory objectDocumentFactory)
    {
        try
        {
            // Get the source document
            var document = await objectDocumentFactory.GetAsync(request.ObjectType, request.ObjectId);

            // Generate the target stream identifier
            // Format: {objectIdWithoutDashes}-{streamNumber}
            var currentStreamId = document.Active.StreamIdentifier;
            var streamNumber = 0;

            // Parse the current stream number and increment it
            if (currentStreamId.Contains('-'))
            {
                var parts = currentStreamId.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                {
                    streamNumber = num + 1;
                }
            }

            var objectIdWithoutDashes = request.ObjectId.Replace("-", string.Empty);
            var targetStreamIdentifier = $"{objectIdWithoutDashes}-{streamNumber:D10}";

            // Execute the migration using the migration service
            var result = await migrationService
                .ForDocument(document)
                .CopyToNewStream(targetStreamIdentifier)
                .ExecuteAsync();

            if (result.Success)
            {
                return Results.Ok(new
                {
                    success = true,
                    migrationId = result.MigrationId,
                    sourceStreamId = currentStreamId,
                    targetStreamId = targetStreamIdentifier,
                    eventsCopied = result.Statistics?.TotalEvents ?? 0,
                    eventsTransformed = result.Statistics?.EventsTransformed ?? 0,
                    message = $"Migration completed successfully. Stream switched from {currentStreamId} to {targetStreamIdentifier}"
                });
            }
            else
            {
                return Results.Problem(
                    title: "Migration failed",
                    detail: result.ErrorMessage ?? "Unknown error",
                    statusCode: 500
                );
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to execute migration",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private class ExecuteRealMigrationRequest
    {
        public string ObjectId { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
    }

    private static async Task<IResult> ExecuteLiveMigration(
        [FromBody] ExecuteLiveMigrationRequest request,
        [FromServices] IEventStreamMigrationService migrationService,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IHubContext<TaskFlowHub> hubContext,
        CancellationToken cancellationToken)
    {
        var migrationId = Guid.NewGuid().ToString();
        var eventsTransformed = 0;
        var iterations = 0;

        try
        {
            // Get the source document
            var document = await objectDocumentFactory.GetAsync(request.ObjectType, request.ObjectId);

            // Count source events
            var sourceEventStream = eventStreamFactory.Create(document);
            var sourceEvents = await sourceEventStream.ReadAsync();
            var sourceEventCount = sourceEvents.Count;

            // Generate the target stream identifier
            var currentStreamId = document.Active.StreamIdentifier;
            var streamNumber = 0;

            // Parse the current stream number and increment it
            if (currentStreamId.Contains('-'))
            {
                var parts = currentStreamId.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                {
                    streamNumber = num + 1;
                }
            }

            var objectIdWithoutDashes = request.ObjectId.Replace("-", string.Empty);
            var targetStreamIdentifier = $"{objectIdWithoutDashes}-{streamNumber:D10}";

            // Broadcast migration started
            await hubContext.BroadcastLiveMigrationStarted(
                migrationId,
                currentStreamId,
                targetStreamIdentifier,
                sourceEventCount);

            // Execute the live migration with SignalR callbacks
            var result = await migrationService
                .ForDocument(document)
                .CopyToNewStream(targetStreamIdentifier)
                .WithLiveMigration(opts => opts
                    // Per-iteration progress callback
                    .OnCatchUpProgress(progress =>
                    {
                        iterations = progress.Iteration;
                        var phase = progress.IsSynced ? "synced" : "catchup";

                        // Fire-and-forget to avoid blocking the migration
                        _ = hubContext.BroadcastLiveMigrationIterationProgress(
                            migrationId,
                            phase,
                            progress.Iteration,
                            progress.SourceVersion,
                            progress.TargetVersion,
                            progress.EventsBehind,
                            progress.EventsCopiedThisIteration,
                            progress.TotalEventsCopied,
                            progress.ElapsedTime.ToString(@"hh\:mm\:ss"),
                            progress.IsSynced,
                            $"Iteration {progress.Iteration}: {progress.EventsCopiedThisIteration} events copied");
                    })
                    // Pre-append callback with optional demo delay
                    .OnBeforeAppend(async eventProgress =>
                    {
                        // Add demo delay if configured (slows down migration for visualization)
                        if (request.DemoDelayMs > 0)
                        {
                            await Task.Delay(request.DemoDelayMs);
                        }
                    })
                    // Per-event progress callback (async)
                    .OnEventCopied(async eventProgress =>
                    {
                        if (eventProgress.WasTransformed)
                        {
                            Interlocked.Increment(ref eventsTransformed);
                        }

                        // Broadcast event copied via SignalR
                        await hubContext.BroadcastLiveMigrationEventCopied(
                            migrationId,
                            eventProgress.EventVersion,
                            eventProgress.EventType,
                            eventProgress.WasTransformed,
                            eventProgress.OriginalEventType,
                            eventProgress.OriginalSchemaVersion,
                            eventProgress.NewSchemaVersion,
                            eventProgress.TotalEventsCopied,
                            eventProgress.SourceVersion);
                    })
                    .WithMaxIterations(request.MaxIterations)
                    .WithCloseTimeout(TimeSpan.FromMinutes(2)))
                .ExecuteLiveMigrationAsync(cancellationToken);

            if (result.Success)
            {
                // Broadcast completion
                await hubContext.BroadcastLiveMigrationCompleted(
                    migrationId,
                    result.TotalEventsCopied,
                    result.Iterations,
                    result.ElapsedTime.ToString(@"hh\:mm\:ss"),
                    eventsTransformed);

                return Results.Ok(new
                {
                    success = true,
                    migrationId,
                    sourceStreamId = currentStreamId,
                    targetStreamId = targetStreamIdentifier,
                    totalEventsCopied = result.TotalEventsCopied,
                    iterations = result.Iterations,
                    eventsTransformed,
                    elapsedTime = result.ElapsedTime.ToString(@"hh\:mm\:ss"),
                    message = $"Live migration completed successfully. Stream switched from {currentStreamId} to {targetStreamIdentifier}"
                });
            }
            else
            {
                // Broadcast failure
                await hubContext.BroadcastLiveMigrationFailed(
                    migrationId,
                    result.Error ?? "Unknown error",
                    iterations,
                    result.TotalEventsCopied);

                return Results.Problem(
                    title: "Live migration failed",
                    detail: result.Error ?? "Unknown error",
                    statusCode: 500
                );
            }
        }
        catch (Exception ex)
        {
            // Broadcast failure
            await hubContext.BroadcastLiveMigrationFailed(
                migrationId,
                ex.Message,
                iterations,
                0);

            return Results.Problem(
                title: "Failed to execute live migration",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private class ExecuteLiveMigrationRequest
    {
        public string ObjectId { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public bool ApplyTransformation { get; set; } = false;
        public int DemoDelayMs { get; set; } = 0; // Delay in milliseconds per event (0 = no delay)
        public int MaxIterations { get; set; } = 100; // Maximum catch-up iterations (0 = unlimited)
    }

    /// <summary>
    /// Catches up any new events from source to target stream.
    /// </summary>
    private static async Task CatchUpEventsToTarget(
        MigrationDemoState state,
        IEventStreamFactory eventStreamFactory,
        IDataStore dataStore)
    {
        if (state.SourceDocument == null || state.TargetDocument == null)
            return;

        // Re-read source stream to find new events
        var sourceEventStream = eventStreamFactory.Create(state.SourceDocument);
        var allSourceEvents = await sourceEventStream.ReadAsync();
        var currentSourceCount = allSourceEvents.Count;

        // Check if new events arrived since we last copied
        if (currentSourceCount > state.LastSourceVersion)
        {
            // Get the new events (skip the ones we already copied)
            var newEvents = allSourceEvents.Skip(state.LastSourceVersion).ToList();

            if (newEvents.Count > 0)
            {
                // Append new events to target stream (preserve original timestamps)
                await dataStore.AppendAsync(state.TargetDocument, preserveTimestamp: true, CancellationToken.None, newEvents.ToArray());

                // Update in-memory state for UI with actual event data
                foreach (var evt in newEvents)
                {
                    // Parse the actual event payload
                    var data = ParseEventPayload(evt.Payload);

                    var eventDto = new StreamEventDto
                    {
                        Id = $"catchup-{state.TargetEvents.Count + 1}",
                        Version = state.TargetEvents.Count + 1,
                        Type = evt.EventType,
                        Timestamp = evt.ActionMetadata?.EventOccuredAt?.DateTime ?? DateTime.UtcNow,
                        Data = data,
                        SchemaVersion = evt.SchemaVersion,
                        IsLiveEvent = true,
                        WrittenTo = new List<string> { "source", "target" }
                    };
                    state.TargetEvents.Add(eventDto);

                    // Also update the source event to show it was replicated
                    // Match by version since that's unique
                    var sourceEventVersion = state.LastSourceVersion + newEvents.IndexOf(evt) + 1;
                    var sourceEvent = state.SourceEvents.FirstOrDefault(e => e.Version == sourceEventVersion);
                    if (sourceEvent != null && !sourceEvent.WrittenTo.Contains("target"))
                    {
                        sourceEvent.WrittenTo.Add("target");
                    }
                }

                state.LastSourceVersion = currentSourceCount;
            }
        }
    }

    /// <summary>
    /// Parses event payload JSON into a dictionary for UI display.
    /// </summary>
    private static Dictionary<string, object> ParseEventPayload(string? payload)
    {
        var data = new Dictionary<string, object>();
        if (string.IsNullOrEmpty(payload))
            return data;

        try
        {
            var jsonDoc = JsonDocument.Parse(payload);
            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            {
                data[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ToString()
                };
            }
        }
        catch
        {
            data["_rawPayload"] = payload;
        }

        return data;
    }

    private static void ProcessEventsToTarget(MigrationDemoState state)
    {
        var nonLiveEvents = state.SourceEvents.Where(e => !e.IsLiveEvent).ToList();
        foreach (var sourceEvent in nonLiveEvents)
        {
            var targetEvent = TransformEvent(sourceEvent);
            state.TargetEvents.Add(targetEvent);
            state.EventsProcessed++;
            state.Progress = (double)state.EventsProcessed / state.TotalEvents * 100;
        }
    }

    private static StreamEventDto TransformEvent(StreamEventDto sourceEvent)
    {
        var newData = new Dictionary<string, object>(sourceEvent.Data);

        switch (sourceEvent.Type)
        {
            case "WorkItem.Created":
                newData["priority"] = "medium";
                if (newData.TryGetValue("status", out var status))
                {
                    newData["initialStatus"] = status;
                    newData.Remove("status");
                }
                break;

            case "WorkItem.AssigneeChanged":
                if (newData.TryGetValue("assignee", out var assignee))
                {
                    newData["assignedTo"] = assignee;
                    newData.Remove("assignee");
                }
                newData["assignedAt"] = sourceEvent.Timestamp.ToString("O");
                break;

            case "WorkItem.StatusChanged":
                newData["previousStatus"] = "unknown";
                newData["changedBy"] = "system";
                break;

            case "WorkItem.Completed":
                if (newData.TryGetValue("completedBy", out var completedBy))
                {
                    newData["resolvedBy"] = completedBy;
                    newData.Remove("completedBy");
                }
                newData["resolution"] = "completed";
                break;

            default:
                // For live events with new types, just add migration metadata
                newData["_migratedAt"] = DateTime.UtcNow.ToString("O");
                break;
        }

        return new StreamEventDto
        {
            Id = sourceEvent.Id,
            Version = sourceEvent.Version,
            Type = sourceEvent.Type,
            Timestamp = sourceEvent.Timestamp,
            Data = newData,
            SchemaVersion = 2,
            IsLiveEvent = sourceEvent.IsLiveEvent,
            WrittenTo = sourceEvent.WrittenTo?.ToList() ?? new List<string>()
        };
    }

    private static object MapToResponse(MigrationDemoState state)
    {
        return new
        {
            migrationId = state.MigrationId,
            phase = state.Phase,
            sourceStreamId = state.SourceStreamId,
            targetStreamId = state.TargetStreamId,
            sourceEvents = state.SourceEvents,
            targetEvents = state.TargetEvents,
            eventsProcessed = state.EventsProcessed,
            totalEvents = state.TotalEvents,
            progress = state.Progress,
            transformations = new[]
            {
                new
                {
                    eventType = "WorkItem.Created",
                    fromVersion = 1,
                    toVersion = 2,
                    changes = new[] { "Added \"priority\" field with default \"medium\"", "Renamed \"status\" to \"initialStatus\"" }
                },
                new
                {
                    eventType = "WorkItem.AssigneeChanged",
                    fromVersion = 1,
                    toVersion = 2,
                    changes = new[] { "Renamed \"assignee\" to \"assignedTo\"", "Added \"assignedAt\" timestamp" }
                },
                new
                {
                    eventType = "WorkItem.StatusChanged",
                    fromVersion = 1,
                    toVersion = 2,
                    changes = new[] { "Added \"previousStatus\" field", "Added \"changedBy\" field" }
                },
                new
                {
                    eventType = "WorkItem.Completed",
                    fromVersion = 1,
                    toVersion = 2,
                    changes = new[] { "Renamed \"completedBy\" to \"resolvedBy\"", "Added \"resolution\" enum field" }
                }
            }
        };
    }

    // Request/Response DTOs
    private class StartMigrationRequest
    {
        public string SourceStreamId { get; set; } = string.Empty;
        public string SourceStreamType { get; set; } = string.Empty;
    }

    private class AddLiveEventRequest
    {
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object> EventData { get; set; } = new();
    }

    private class StreamEventDto
    {
        public string Id { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public int SchemaVersion { get; set; }
        public bool IsLiveEvent { get; set; }
        public List<string> WrittenTo { get; set; } = new();
    }

    private class MigrationDemoState
    {
        public string MigrationId { get; set; } = string.Empty;
        public string Phase { get; set; } = "idle";
        public string SourceStreamId { get; set; } = string.Empty;
        public string SourceStreamType { get; set; } = string.Empty;
        public string? TargetStreamId { get; set; }
        public string? TargetStreamIdentifier { get; set; }
        public List<StreamEventDto> SourceEvents { get; set; } = new();
        public List<StreamEventDto> TargetEvents { get; set; } = new();
        public int EventsProcessed { get; set; }
        public int TotalEvents { get; set; }
        public double Progress { get; set; }
        public int LiveEventCounter { get; set; }

        // For real blob operations
        public IObjectDocument? SourceDocument { get; set; }
        public IObjectDocument? TargetDocument { get; set; }
        public int LastSourceVersion { get; set; }
    }

    /// <summary>
    /// A minimal IObjectDocument implementation used during migration to write events to the target stream.
    /// </summary>
    private class MigrationTargetDocument : IObjectDocument
    {
        public MigrationTargetDocument(
            string objectId,
            string objectName,
            StreamInformation active)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            Active = active;
            TerminatedStreams = new List<TerminatedStream>();
        }

        public StreamInformation Active { get; }
        public string ObjectId { get; }
        public string ObjectName { get; }
        public List<TerminatedStream> TerminatedStreams { get; }
        public string? SchemaVersion { get; } = null;
        public string? Hash { get; private set; }
        public string? PrevHash { get; private set; }

        public void SetHash(string? hash, string? prevHash = null)
        {
            Hash = hash;
            PrevHash = prevHash;
        }
    }

    /// <summary>
    /// An IObjectDocument implementation used during cutover to update the document with new active stream.
    /// </summary>
    private class MigrationCutoverDocument : IObjectDocument
    {
        public MigrationCutoverDocument(
            string objectId,
            string objectName,
            StreamInformation active,
            List<TerminatedStream> terminatedStreams,
            string? schemaVersion = null,
            string? hash = null,
            string? prevHash = null)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            Active = active;
            TerminatedStreams = terminatedStreams;
            SchemaVersion = schemaVersion;
            Hash = hash;
            PrevHash = prevHash;
        }

        public StreamInformation Active { get; }
        public string ObjectId { get; }
        public string ObjectName { get; }
        public List<TerminatedStream> TerminatedStreams { get; }
        public string? SchemaVersion { get; }
        public string? Hash { get; private set; }
        public string? PrevHash { get; private set; }

        public void SetHash(string? hash, string? prevHash = null)
        {
            Hash = hash;
            PrevHash = prevHash;
        }
    }
}
