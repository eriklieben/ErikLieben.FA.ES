using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Hubs;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Events.Epic;
using TaskFlow.Domain.ValueObjects;
using ErikLieben.FA.ES;
using TaskFlow.Domain.ValueObjects.WorkItem;
using TaskFlow.Domain.ValueObjects.Sprint;
using TaskFlow.Domain.Projections;
using TaskFlow.Api.Helpers;
using ErikLieben.FA.ES.Projections;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Admin endpoints for event exploration, time travel, and demo management
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin")
            .WithTags("Admin & Diagnostics")
            .WithDescription("Admin endpoints for event exploration, time travel debugging, and demo data generation");

        // Event exploration
        group.MapGet("/events/project/{id}", GetProjectEvents)
            .WithName("GetProjectEvents")
            .WithSummary("Get raw event stream for a project");

        group.MapGet("/events/workitem/{id}", GetWorkItemEvents)
            .WithName("GetWorkItemEvents")
            .WithSummary("Get raw event stream for a work item");

        // Time travel
        group.MapGet("/workitems/{id}/version/{version}", GetWorkItemAtVersion)
            .WithName("GetWorkItemAtVersion")
            .WithSummary("Get work item state at a specific version (time travel)");

        group.MapGet("/projects/{id}/version/{version}", GetProjectAtVersion)
            .WithName("GetProjectAtVersion")
            .WithSummary("Get project state at a specific version (time travel)");

        group.MapGet("/projects/{id}/version/{version}/enriched", GetProjectAtVersionEnriched)
            .WithName("GetProjectAtVersionEnriched")
            .WithSummary("Get enriched project state with user profile data (on-demand projection)");

        // Demo data
        group.MapPost("/demo/seed", SeedDemoData)
            .WithName("SeedDemoData")
            .WithSummary("Seed database with demo projects and work items");

        group.MapPost("/demo/seed-users", SeedDemoUsers)
            .WithName("SeedDemoUsers")
            .WithSummary("Seed just the demo user profiles");

        group.MapPost("/demo/seed-epics", SeedDemoEpics)
            .WithName("SeedDemoEpics")
            .WithSummary("Seed demo epics stored in Azure Table Storage");

        group.MapPost("/demo/seed-sprints", SeedDemoSprints)
            .WithName("SeedDemoSprints")
            .WithSummary("Seed demo sprints stored in Azure CosmosDB");

        group.MapGet("/events/epic/{id}", GetEpicEvents)
            .WithName("GetEpicEvents")
            .WithSummary("Get raw event stream for an epic (stored in Table Storage)");

        group.MapGet("/projections/userprofiles/status", GetUserProfilesProjectionStatus)
            .WithName("GetUserProfilesProjectionStatus")
            .WithSummary("Get UserProfiles projection status and data");

        // Storage connection info
        group.MapGet("/storage/connection", GetStorageConnection)
            .WithName("GetStorageConnection")
            .WithSummary("Get Azure Storage connection string for Azure Storage Explorer");

        group.MapGet("/storage/debug", GetStorageDebugInfo)
            .WithName("GetStorageDebugInfo")
            .WithSummary("Get all storage connection strings and container information for debugging");

        group.MapGet("/cosmosdb/documents", GetCosmosDbDocuments)
            .WithName("GetCosmosDbDocuments")
            .WithSummary("Get raw documents from CosmosDB for debugging");

        // Projection management
        group.MapGet("/projections", GetProjectionStatus)
            .WithName("GetProjectionStatus")
            .WithSummary("Get status of all projections including checkpoint info");

        group.MapGet("/projections/{name}/json", GetProjectionJson)
            .WithName("GetProjectionJson")
            .WithSummary("Get the JSON representation of a projection");

        group.MapGet("/projections/{name}/metadata", GetProjectionMetadata)
            .WithName("GetProjectionMetadata")
            .WithSummary("Get metadata about a projection including last modified timestamp");

        group.MapPost("/projections/{name}/rebuild", RebuildProjection)
            .WithName("RebuildProjection")
            .WithSummary("Rebuild a projection by updating it to the latest version");

        group.MapPost("/projections/{name}/status", SetProjectionStatusEndpoint)
            .WithName("SetProjectionStatus")
            .WithSummary("Set projection status (Active, Rebuilding, or Disabled)");

        group.MapPost("/projections/{name}/reset", ResetProjection)
            .WithName("ResetProjection")
            .WithSummary("Clear projection checkpoint and rebuild from scratch");

        group.MapPost("/projections/build-all", BuildAllProjections)
            .WithName("BuildAllProjections")
            .WithSummary("Build/rebuild all projections from the event store with progress tracking");

        group.MapDelete("/storage/clear", ClearAllStorage)
            .WithName("ClearAllStorage")
            .WithSummary("Delete all blob containers and reset storage (use with caution!)");

        group.MapGet("/storage/providers", GetStorageProviderStatus)
            .WithName("GetStorageProviderStatus")
            .WithSummary("Get which storage providers are configured and available");

        // Benchmark results
        group.MapGet("/benchmarks", ListBenchmarkFiles)
            .WithName("ListBenchmarkFiles")
            .WithSummary("List available benchmark result files");

        group.MapGet("/benchmarks/{filename}", GetBenchmarkFile)
            .WithName("GetBenchmarkFile")
            .WithSummary("Get a specific benchmark result file by name");

        return group;
    }

    private static IResult GetStorageProviderStatus(
        [FromServices] StorageProviderStatus status)
    {
        return Results.Ok(new
        {
            providers = new
            {
                blob = new { enabled = status.BlobEnabled, name = "Azure Blob Storage" },
                table = new { enabled = status.TableEnabled, name = "Azure Table Storage" },
                cosmos = new { enabled = status.CosmosDbEnabled, name = "Azure CosmosDB" }
            },
            timestamp = DateTimeOffset.UtcNow
        });
    }

    private static async Task<IResult> GetProjectEvents(
        string id,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IProjectFactory projectFactory)
    {
        // Get the object document for the project
        var objectDocument = await objectDocumentFactory.GetAsync("project", id);

        if (objectDocument == null)
        {
            return Results.NotFound(new { message = "Project not found" });
        }

        // Create the event stream from the document WITHOUT initializing the aggregate
        // This means NO upcasters are registered, so we get RAW events as stored in blob storage
        var stream = eventStreamFactory.Create(objectDocument);

        // Read all events from the stream (raw, without upcasting)
        // These are displayed in the LEFT side "Event Stream" card
        var rawEvents = await stream.ReadAsync();

        // Get a Project instance to access the EventTypeRegistry for type lookups
        // Note: We use a factory to get a properly initialized aggregate with registered event types
        var project = await projectFactory.GetAsync(ProjectId.From(id));
        var eventTypeRegistry = project.EventTypeRegistry;

        // Transform events into a simplified format for the UI
        var events = rawEvents.Select((e, index) =>
        {
            // Parse the Payload JSON string to get the actual data object
            object? data = null;
            if (!string.IsNullOrEmpty(e.Payload))
            {
                try
                {
                    data = System.Text.Json.JsonSerializer.Deserialize<object>(e.Payload);
                }
                catch
                {
                    // If deserialization fails, return the raw payload string
                    data = e.Payload;
                }
            }

            // Look up the actual C# type from the EventTypeRegistry
            string? deserializationType = null;
            if (eventTypeRegistry.TryGetByNameAndVersion(e.EventType, e.SchemaVersion, out var typeInfo) && typeInfo != null)
            {
                deserializationType = typeInfo.Type.Name;
            }

            return new
            {
                eventType = e.EventType,
                timestamp = e.ActionMetadata?.EventOccuredAt ?? DateTimeOffset.UtcNow,
                data,
                version = e.EventVersion,
                schemaVersion = e.SchemaVersion,
                deserializationType,  // The actual C# record type used for deserialization
                metadata = e.Metadata
            };
        }).ToList();

        return Results.Ok(events);
    }

    private static async Task<IResult> GetWorkItemEvents(
        string id,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory)
    {
        // Get the object document for the work item
        var objectDocument = await objectDocumentFactory.GetAsync("workitem", id);

        if (objectDocument == null)
        {
            return Results.NotFound(new { message = "Work item not found" });
        }

        // Create the event stream from the document WITHOUT initializing the aggregate
        // This means NO upcasters are registered, so we get RAW events as stored in blob storage
        var stream = eventStreamFactory.Create(objectDocument);

        // Read all events from the stream (raw, without upcasting)
        // These are displayed in the LEFT side "Event Stream" card
        var rawEvents = await stream.ReadAsync();

        // Transform events into a simplified format for the UI
        var events = rawEvents.Select((e, index) =>
        {
            // Parse the Payload JSON string to get the actual data object
            object? data = null;
            if (!string.IsNullOrEmpty(e.Payload))
            {
                try
                {
                    data = System.Text.Json.JsonSerializer.Deserialize<object>(e.Payload);
                }
                catch
                {
                    // If deserialization fails, return the raw payload string
                    data = e.Payload;
                }
            }

            return new
            {
                eventType = e.EventType,
                timestamp = e.ActionMetadata?.EventOccuredAt ?? DateTimeOffset.UtcNow,
                data,
                version = e.EventVersion,
                schemaVersion = e.SchemaVersion,
                metadata = e.Metadata
            };
        }).ToList();

        return Results.Ok(events);
    }

    private static async Task<IResult> GetWorkItemAtVersion(
        string id,
        long version,
        [FromServices] IWorkItemFactory factory,
        [FromServices] ErikLieben.FA.ES.IEventStreamFactory eventStreamFactory,
        [FromServices] ErikLieben.FA.ES.IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IProjectionService projectionService)
    {
        var workItemId = Guid.Parse(id);

        // Use factory's built-in time travel support to rebuild the aggregate at the specified version
        // This INCLUDES any upcasting logic that may be registered
        // The resulting state shows the "real" aggregate state as it would be loaded in the application
        // This is displayed in the RIGHT side "State at Event" card
        var workItem = await factory.GetAsync(WorkItemId.From(workItemId.ToString()), (int)version);

        // Get document to read all events separately (for metadata)
        var document = await objectDocumentFactory.GetAsync("workitem", workItemId.ToString());
        var eventStream = eventStreamFactory.Create(document);
        var allEvents = await eventStream.ReadAsync();
        var currentVersion = allEvents.MaxBy(e => e.EventVersion)?.EventVersion ?? 0;

        // Get user profiles to enrich assignee data
        var userProfiles = projectionService.GetUserProfiles();

        // Enrich assignee information
        object? assignedToInfo = null;
        if (!string.IsNullOrEmpty(workItem.AssignedTo))
        {
            var assigneeProfile = userProfiles.GetProfile(workItem.AssignedTo);
            assignedToInfo = new
            {
                userId = workItem.AssignedTo,
                name = assigneeProfile?.Name ?? workItem.AssignedTo
            };
        }

        return Results.Ok(new
        {
            workItemId = id,
            version = version,
            currentVersion = currentVersion,
            state = new
            {
                projectId = workItem.ProjectId,
                title = workItem.Title,
                description = workItem.Description,
                priority = workItem.Priority.ToString(),
                status = workItem.Status.ToString(),
                assignedTo = assignedToInfo,
                deadline = workItem.Deadline,
                estimatedHours = workItem.EstimatedHours,
                tags = workItem.Tags.ToArray(),
                commentCount = workItem.Comments.Count
            },
            events = allEvents.Where(e => e.EventVersion <= version).Select(e => new
            {
                eventType = e.EventType,
                timestamp = e.ActionMetadata?.EventOccuredAt ?? DateTimeOffset.UtcNow,
                data = (e as ErikLieben.FA.ES.IEventWithData)?.Data,
                version = e.EventVersion
            }).ToArray()
        });
    }

    private static async Task<IResult> GetProjectAtVersion(
        string id,
        long version,
        [FromServices] IProjectFactory factory,
        [FromServices] ErikLieben.FA.ES.IEventStreamFactory eventStreamFactory,
        [FromServices] ErikLieben.FA.ES.IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IProjectionService projectionService)
    {
        var projectId = Guid.Parse(id);

        // Use factory's built-in time travel support to rebuild the aggregate at the specified version
        // This INCLUDES upcasting: legacy ProjectCompleted events are transformed to specific outcome events
        // The resulting state shows the "real" aggregate state as it would be loaded in the application
        // This is displayed in the RIGHT side "State at Event" card
        var project = await factory.GetAsync(ProjectId.From(projectId.ToString()), (int)version);

        // DEBUG: Log the outcome value and work item counts
        Console.WriteLine($"[DEBUG] GetProjectAtVersion - ProjectId: {projectId}, Version: {version}");
        Console.WriteLine($"[DEBUG] IsCompleted: {project.IsCompleted}, Outcome: {project.Outcome} ({(int)project.Outcome})");
        Console.WriteLine($"[DEBUG] Planned Items: {project.PlannedItemsOrder.Count}");
        Console.WriteLine($"[DEBUG] In Progress Items: {project.InProgressItemsOrder.Count}");
        Console.WriteLine($"[DEBUG] Completed Items: {project.CompletedItemsOrder.Count}");
        Console.WriteLine($"[DEBUG] Total Work Items: {project.PlannedItemsOrder.Count + project.InProgressItemsOrder.Count + project.CompletedItemsOrder.Count}");

        // Get document to read all events separately (for metadata)
        var document = await objectDocumentFactory.GetAsync("project", projectId.ToString());
        var eventStream = eventStreamFactory.Create(document);
        var allEvents = await eventStream.ReadAsync();
        var currentVersion = allEvents.MaxBy(e => e.EventVersion)?.EventVersion ?? 0;

        // DEBUG: Log events up to this version
        var eventsUpToVersion = allEvents.Where(e => e.EventVersion <= version).ToList();
        Console.WriteLine($"[DEBUG] Events up to version {version}:");
        foreach (var evt in eventsUpToVersion)
        {
            Console.WriteLine($"  - Version {evt.EventVersion}: {evt.EventType}");
        }

        return Results.Ok(new
        {
            projectId = id,
            version = version,
            currentVersion = currentVersion,
            state = new
            {
                name = project.Name,
                description = project.Description,
                ownerId = project.OwnerId?.Value,
                isCompleted = project.IsCompleted,
                outcome = (int)project.Outcome,
                teamMembers = project.TeamMembers.ToDictionary(
                    tm => tm.Key.Value,
                    tm => tm.Value
                ),
                workItemCounts = new
                {
                    planned = project.PlannedItemsOrder.Count,
                    inProgress = project.InProgressItemsOrder.Count,
                    completed = project.CompletedItemsOrder.Count,
                    total = project.PlannedItemsOrder.Count + project.InProgressItemsOrder.Count + project.CompletedItemsOrder.Count
                }
            },
            events = allEvents.Where(e => e.EventVersion <= version).Select(e => new
            {
                eventType = e.EventType,
                timestamp = e.ActionMetadata?.EventOccuredAt ?? DateTimeOffset.UtcNow,
                data = (e as ErikLieben.FA.ES.IEventWithData)?.Data,
                version = e.EventVersion
            }).ToArray()
        });
    }

    /// <summary>
    /// Enriched version of GetProjectAtVersion that performs on-demand projection
    /// with user profile data. This demonstrates how to project for a specific aggregate
    /// without maintaining a global projection for all aggregates.
    /// </summary>
    private static async Task<IResult> GetProjectAtVersionEnriched(
        string id,
        long version,
        [FromServices] IProjectFactory factory,
        [FromServices] ErikLieben.FA.ES.IEventStreamFactory eventStreamFactory,
        [FromServices] ErikLieben.FA.ES.IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IProjectionService projectionService)
    {
        var projectId = Guid.Parse(id);

        // Use factory's built-in time travel support to rebuild the aggregate at the specified version
        var project = await factory.GetAsync(ProjectId.From(projectId.ToString()), (int)version);

        // Get document to read all events separately (for metadata)
        var document = await objectDocumentFactory.GetAsync("project", projectId.ToString());
        var eventStream = eventStreamFactory.Create(document);
        var allEvents = await eventStream.ReadAsync();
        var currentVersion = allEvents.MaxBy(e => e.EventVersion)?.EventVersion ?? 0;

        // ON-DEMAND PROJECTION: Get user profiles and enrich team member data
        // This is done only for this specific project, not for all projects
        var userProfiles = projectionService.GetUserProfiles();

        // Enrich team members with user profile information
        var enrichedTeamMembers = project.TeamMembers.Select(tm =>
        {
            var profile = userProfiles.GetProfile(tm.Key.Value);
            return new
            {
                userId = tm.Key.Value,
                name = profile?.Name ?? tm.Key.Value,
                role = tm.Value
            };
        }).ToArray();

        // Enrich owner information
        var ownerProfile = userProfiles.GetProfile(project.OwnerId!.Value);
        var ownerInfo = new
        {
            userId = project.OwnerId.Value,
            name = ownerProfile?.Name ?? project.OwnerId.Value,
            role = project.TeamMembers.TryGetValue(project.OwnerId, out var ownerRole) ? ownerRole : "Project Owner"
        };

        return Results.Ok(new
        {
            projectId = id,
            version = version,
            currentVersion = currentVersion,
            state = new
            {
                name = project.Name,
                description = project.Description,
                owner = ownerInfo,
                isCompleted = project.IsCompleted,
                outcome = (int)project.Outcome,
                teamMembers = enrichedTeamMembers,
                workItemCounts = new
                {
                    planned = project.PlannedItemsOrder.Count,
                    inProgress = project.InProgressItemsOrder.Count,
                    completed = project.CompletedItemsOrder.Count,
                    total = project.PlannedItemsOrder.Count + project.InProgressItemsOrder.Count + project.CompletedItemsOrder.Count
                }
            },
            events = allEvents.Where(e => e.EventVersion <= version).Select(e => new
            {
                eventType = e.EventType,
                timestamp = e.ActionMetadata?.EventOccuredAt ?? DateTimeOffset.UtcNow,
                data = (e as ErikLieben.FA.ES.IEventWithData)?.Data,
                version = e.EventVersion
            }).ToArray()
        });
    }

    private static async Task<IResult> SeedDemoData(
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] ErikLieben.FA.ES.IObjectDocumentFactory objectDocumentFactory,
        [FromServices] ErikLieben.FA.ES.IEventStreamFactory eventStreamFactory,
        [FromServices] IWebHostEnvironment environment,
        [FromServices] IProjectionService projectionService,
        [FromServices] IEnumerable<TaskFlow.Api.Projections.IProjectionHandler> projectionHandlers,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        // Disable projection publishing during seeding - projections should be built separately
        using var projectionDisableScope = PublishProjectionUpdateAction.DisableScope();

        try
        {
            var random = new Random();
            var projectIds = new List<Guid>();
            var projectInitiationDates = new System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime>(); // Track when projects were initiated (thread-safe for parallel creation)
            var now = DateTime.UtcNow;

            // Create demo user profiles - UserProfileId is auto-generated, email is used for lookup via tags
            // Active team members who participate in projects
            var activeTeamMembers = new[]
            {
                ("Admin User", "admin@taskflow.demo", "System Administrator"),
                ("Alice Johnson", "alice@company.com", "Designer"),
                ("Bob Smith", "bob@company.com", "Developer"),
                ("Carol Davis", "carol@company.com", "QA Engineer"),
                ("David Martinez", "david@company.com", "DevOps"),
                ("Eve Wilson", "eve@company.com", "Product Manager"),
                ("Frank Brown", "frank@company.com", "Developer"),
                ("Grace Lee", "grace@company.com", "UX Researcher"),
                ("Henry Taylor", "henry@company.com", "Business Analyst"),
                ("Iris Chen", "iris@company.com", "Technical Writer"),
                ("Jack Anderson", "jack@company.com", "Scrum Master")
            };

            // Stakeholders who don't participate in projects (139 users to reach 150 total)
            var stakeholderFirstNames = new[] { "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "Richard", "Barbara", "Joseph", "Susan", "Thomas", "Jessica", "Christopher", "Sarah", "Charles", "Karen", "Daniel", "Nancy", "Matthew", "Lisa", "Anthony", "Betty", "Mark", "Margaret", "Donald", "Sandra", "Steven", "Ashley", "Paul", "Kimberly", "Andrew", "Emily", "Joshua", "Donna", "Kenneth", "Michelle", "Kevin", "Dorothy", "Brian", "Carol", "George", "Amanda", "Timothy", "Melissa", "Ronald", "Deborah" };
            var stakeholderLastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores" };
            var stakeholderRoles = new[] { "Executive Sponsor", "Business Stakeholder", "Finance Director", "Legal Counsel", "Compliance Officer", "External Consultant", "Board Member", "Investor Relations", "Strategic Advisor", "Department Head" };

            var stakeholders = new List<(string name, string email, string role)>();
            for (int i = 0; i < 139; i++)
            {
                var firstName = stakeholderFirstNames[i % stakeholderFirstNames.Length];
                var lastName = stakeholderLastNames[i % stakeholderLastNames.Length];
                // Add number suffix to ensure unique emails
                var emailSuffix = i / (stakeholderFirstNames.Length * stakeholderLastNames.Length / 2) + 1;
                var email = $"{firstName.ToLower()}.{lastName.ToLower()}{(emailSuffix > 1 ? emailSuffix.ToString() : "")}@stakeholders.com";
                var role = stakeholderRoles[i % stakeholderRoles.Length];
                stakeholders.Add(($"{firstName} {lastName}", email, role));
            }

            // Combine active team members and stakeholders (150 total users)
            var demoUsers = activeTeamMembers.Concat(stakeholders.Select(s => (s.name, s.email, s.role))).ToArray();

            // Create users and build email -> UserProfileId mapping for later lookups
            var userIdByEmail = new Dictionary<string, UserProfileId>();

            // Create users - handle conflicts gracefully (for re-seeding scenarios)
            Console.WriteLine($"[SEED] Creating {demoUsers.Length} users...");
            var userCreateCount = 0;
            var userExistCount = 0;
            var totalUsers = demoUsers.Length;
            var userIndex = 0;

            // Send initial progress
            await hubContext.BroadcastSeedProgress("blob", 0, 150 + 50 + 1000, "Creating users...");

            foreach (var (name, email, jobRole) in demoUsers)
            {
                try
                {
                    var (result, userProfile) = await userProfileFactory.CreateProfileAsync(name, email, jobRole, createdByUser: null);
                    if (result.IsSuccess && userProfile != null)
                    {
                        userIdByEmail[email] = userProfile.Metadata!.Id!;
                        userCreateCount++;
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
                {
                    // User already exists, load the existing one
                    try
                    {
                        var existingUser = await objectDocumentFactory.GetFirstByObjectDocumentTag("userprofile", email);
                        if (existingUser != null)
                        {
                            userIdByEmail[email] = UserProfileId.From(existingUser.ObjectId);
                            userExistCount++;
                        }
                    }
                    catch
                    {
                        // Ignore - user won't be available for project assignments
                    }
                }

                // Report progress every 10 users
                userIndex++;
                if (userIndex % 10 == 0 || userIndex == totalUsers)
                {
                    await hubContext.BroadcastSeedProgress("blob", userIndex, 150 + 50 + 1000, $"Creating users ({userIndex}/{totalUsers})...");
                }
            }
            Console.WriteLine($"[SEED] Users: {userCreateCount} created, {userExistCount} existing");

            // Helper function to get UserProfileId by email
            UserProfileId GetUserId(string email) => userIdByEmail.TryGetValue(email, out var id) ? id : throw new Exception($"User with email {email} not found");

            // Helper function to get permissions based on role (for V2 MemberJoinedProject events)
            MemberPermissions GetPermissionsForRole(string role) => role switch
            {
                "Developer" => new MemberPermissions(CanEdit: true, CanDelete: false, CanInvite: false, CanManageWorkItems: true),
                "Designer" => new MemberPermissions(CanEdit: true, CanDelete: false, CanInvite: false, CanManageWorkItems: false),
                "QA Engineer" => new MemberPermissions(CanEdit: true, CanDelete: false, CanInvite: false, CanManageWorkItems: true),
                "DevOps Engineer" => new MemberPermissions(CanEdit: true, CanDelete: true, CanInvite: false, CanManageWorkItems: true),
                "Product Manager" => new MemberPermissions(CanEdit: true, CanDelete: false, CanInvite: true, CanManageWorkItems: true),
                "Tech Lead" => new MemberPermissions(CanEdit: true, CanDelete: true, CanInvite: true, CanManageWorkItems: true),
                "Scrum Master" => new MemberPermissions(CanEdit: false, CanDelete: false, CanInvite: true, CanManageWorkItems: true),
                "Business Analyst" => new MemberPermissions(CanEdit: true, CanDelete: false, CanInvite: false, CanManageWorkItems: false),
                _ => new MemberPermissions(CanEdit: false, CanDelete: false, CanInvite: false, CanManageWorkItems: false)
            };

            // Realistic project data
            var projectTemplates = new[]
            {
                ("Customer Portal Redesign", "Modernize customer-facing portal with new UX/UI"),
                ("Mobile Banking App", "Build secure mobile banking application for iOS and Android"),
                ("Cloud Migration Initiative", "Migrate legacy systems to cloud infrastructure"),
                ("Data Analytics Platform", "Build real-time analytics and reporting platform"),
                ("E-commerce Marketplace", "Develop multi-vendor marketplace platform"),
                ("CRM System Upgrade", "Upgrade and modernize customer relationship management system"),
                ("Payment Gateway Integration", "Integrate multiple payment providers and methods"),
                ("Inventory Management System", "Build warehouse and inventory tracking solution"),
                ("HR Management Portal", "Employee self-service and HR management system"),
                ("Content Management System", "Enterprise content management and publishing platform"),
                ("Marketing Automation Tool", "Automated email and campaign management system"),
                ("Supply Chain Optimization", "Optimize supply chain logistics and tracking"),
                ("Business Intelligence Dashboard", "Executive dashboard with KPIs and metrics"),
                ("API Gateway Modernization", "Rebuild API gateway with microservices architecture"),
                ("Security Compliance Project", "Implement SOC2 and GDPR compliance measures"),
                ("DevOps Pipeline Enhancement", "Improve CI/CD pipeline and deployment automation"),
                ("Customer Support Platform", "Build omnichannel customer support solution"),
                ("Real-time Chat Application", "Develop scalable real-time messaging platform"),
                ("Document Management System", "Digital document storage and workflow system"),
                ("Video Streaming Platform", "Build video hosting and streaming service"),
                ("Social Media Integration", "Integrate social media APIs and analytics"),
                ("Machine Learning Pipeline", "ML model training and deployment infrastructure"),
                ("IoT Device Management", "Platform for managing and monitoring IoT devices"),
                ("Blockchain Implementation", "Implement blockchain for supply chain tracking"),
                ("Microservices Migration", "Migrate monolith to microservices architecture"),
                ("Performance Optimization", "Improve application performance and scalability"),
                ("Mobile App Redesign", "Complete redesign of mobile application"),
                ("Search Engine Enhancement", "Improve search capabilities and relevance"),
                ("Notification System", "Build multi-channel notification delivery system"),
                ("User Authentication Service", "Centralized authentication and authorization"),
                ("Reporting Engine", "Custom report builder and scheduling system"),
                ("Workflow Automation", "Business process automation and orchestration"),
                ("Multi-tenant SaaS Platform", "Build multi-tenant SaaS infrastructure"),
                ("GraphQL API Development", "Develop GraphQL API for mobile and web clients"),
                ("Kubernetes Migration", "Containerize and migrate to Kubernetes"),
                ("Monitoring and Alerting", "Implement comprehensive monitoring solution"),
                ("Disaster Recovery Plan", "Build disaster recovery and backup systems"),
                ("Accessibility Compliance", "Ensure WCAG 2.1 AA compliance across platform"),
                ("Localization Project", "Add multi-language and regional support"),
                ("A/B Testing Framework", "Build experimentation and feature flag platform"),
                ("OAuth Provider Service", "Become OAuth/OIDC identity provider"),
                ("Email Campaign System", "Build email marketing and campaign manager"),
                ("Knowledge Base Platform", "Internal wiki and documentation system"),
                ("Project Management Tool", "Build project tracking and collaboration tool"),
                ("Time Tracking System", "Employee time tracking and reporting"),
                ("Resource Planning Tool", "Resource allocation and capacity planning"),
                ("Quality Assurance Suite", "Automated testing and QA management"),
                ("Partner Integration Portal", "B2B partner integration and management"),
                ("Audit Logging System", "Comprehensive audit trail and compliance logging"),
                ("Feature Flag Service", "Dynamic feature toggle and rollout system")
            };

            var workItemTitles = new[]
            {
                "Design database schema", "Implement user authentication", "Create REST API endpoints",
                "Build frontend components", "Write unit tests", "Setup CI/CD pipeline",
                "Configure production environment", "Implement caching layer", "Add error logging",
                "Create user documentation", "Optimize database queries", "Fix memory leaks",
                "Implement rate limiting", "Add data validation", "Create admin dashboard",
                "Setup monitoring alerts", "Implement search functionality", "Add email notifications",
                "Create mobile responsive design", "Implement OAuth integration", "Add PDF export feature",
                "Setup load balancer", "Implement data encryption", "Create backup strategy",
                "Add audit logging", "Implement role-based access", "Create API documentation",
                "Setup staging environment", "Add analytics tracking", "Implement WebSocket support",
                "Create migration scripts", "Add localization support", "Implement file upload",
                "Setup Redis cache", "Add performance monitoring", "Create E2E tests",
                "Implement lazy loading", "Add image optimization", "Create deployment scripts",
                "Setup SSL certificates", "Implement pagination", "Add export to CSV",
                "Create health check endpoint", "Implement retry logic", "Add request validation",
                "Setup log aggregation", "Implement circuit breaker", "Create integration tests",
                "Add dark mode support", "Implement infinite scroll", "Create webhook system"
            };

            // Translations for work item titles (Dutch and German)
            var workItemTitleTranslations = new Dictionary<string, (string Dutch, string German)>
            {
                ["Design database schema"] = ("Ontwerp databaseschema", "Datenbankschema entwerfen"),
                ["Implement user authentication"] = ("Implementeer gebruikersauthenticatie", "Benutzerauthentifizierung implementieren"),
                ["Create REST API endpoints"] = ("Maak REST API-eindpunten", "REST API-Endpunkte erstellen"),
                ["Build frontend components"] = ("Bouw frontend-componenten", "Frontend-Komponenten erstellen"),
                ["Write unit tests"] = ("Schrijf unit tests", "Unit-Tests schreiben"),
                ["Setup CI/CD pipeline"] = ("Stel CI/CD-pipeline in", "CI/CD-Pipeline einrichten"),
                ["Configure production environment"] = ("Configureer productieomgeving", "Produktionsumgebung konfigurieren"),
                ["Implement caching layer"] = ("Implementeer caching-laag", "Caching-Schicht implementieren"),
                ["Add error logging"] = ("Voeg foutregistratie toe", "Fehlerprotokollierung hinzufügen"),
                ["Create user documentation"] = ("Maak gebruikersdocumentatie", "Benutzerdokumentation erstellen"),
                ["Optimize database queries"] = ("Optimaliseer databasequeries", "Datenbankabfragen optimieren"),
                ["Fix memory leaks"] = ("Repareer geheugenlekken", "Speicherlecks beheben"),
                ["Implement rate limiting"] = ("Implementeer snelheidsbeperking", "Ratenbegrenzung implementieren"),
                ["Add data validation"] = ("Voeg datavalidatie toe", "Datenvalidierung hinzufügen"),
                ["Create admin dashboard"] = ("Maak beheerdersdashboard", "Admin-Dashboard erstellen"),
                ["Setup monitoring alerts"] = ("Stel monitoringwaarschuwingen in", "Überwachungswarnungen einrichten"),
                ["Implement search functionality"] = ("Implementeer zoekfunctionaliteit", "Suchfunktionalität implementieren"),
                ["Add email notifications"] = ("Voeg e-mailmeldingen toe", "E-Mail-Benachrichtigungen hinzufügen"),
                ["Create mobile responsive design"] = ("Maak mobiel responsief ontwerp", "Mobiles responsives Design erstellen"),
                ["Implement OAuth integration"] = ("Implementeer OAuth-integratie", "OAuth-Integration implementieren"),
                ["Add PDF export feature"] = ("Voeg PDF-exportfunctie toe", "PDF-Exportfunktion hinzufügen"),
                ["Setup load balancer"] = ("Stel load balancer in", "Load Balancer einrichten"),
                ["Implement data encryption"] = ("Implementeer gegevensversleuteling", "Datenverschlüsselung implementieren"),
                ["Create backup strategy"] = ("Maak back-upstrategie", "Backup-Strategie erstellen"),
                ["Add audit logging"] = ("Voeg auditregistratie toe", "Audit-Protokollierung hinzufügen"),
                ["Implement role-based access"] = ("Implementeer rolgebaseerde toegang", "Rollenbasierte Zugriffskontrolle implementieren"),
                ["Create API documentation"] = ("Maak API-documentatie", "API-Dokumentation erstellen"),
                ["Setup staging environment"] = ("Stel staging-omgeving in", "Staging-Umgebung einrichten"),
                ["Add analytics tracking"] = ("Voeg analytics tracking toe", "Analytics-Tracking hinzufügen"),
                ["Implement WebSocket support"] = ("Implementeer WebSocket-ondersteuning", "WebSocket-Unterstützung implementieren"),
                ["Create migration scripts"] = ("Maak migratiescripts", "Migrationsskripte erstellen"),
                ["Add localization support"] = ("Voeg lokalisatie-ondersteuning toe", "Lokalisierungsunterstützung hinzufügen"),
                ["Implement file upload"] = ("Implementeer bestandsupload", "Datei-Upload implementieren"),
                ["Setup Redis cache"] = ("Stel Redis cache in", "Redis-Cache einrichten"),
                ["Add performance monitoring"] = ("Voeg prestatiemonitoring toe", "Leistungsüberwachung hinzufügen"),
                ["Create E2E tests"] = ("Maak E2E-tests", "E2E-Tests erstellen"),
                ["Implement lazy loading"] = ("Implementeer lazy loading", "Lazy Loading implementieren"),
                ["Add image optimization"] = ("Voeg afbeeldingsoptimalisatie toe", "Bildoptimierung hinzufügen"),
                ["Create deployment scripts"] = ("Maak deployment-scripts", "Deployment-Skripte erstellen"),
                ["Setup SSL certificates"] = ("Stel SSL-certificaten in", "SSL-Zertifikate einrichten"),
                ["Implement pagination"] = ("Implementeer paginering", "Paginierung implementieren"),
                ["Add export to CSV"] = ("Voeg export naar CSV toe", "Export nach CSV hinzufügen"),
                ["Create health check endpoint"] = ("Maak health check-eindpunt", "Health-Check-Endpunkt erstellen"),
                ["Implement retry logic"] = ("Implementeer herprobeerlogica", "Wiederholungslogik implementieren"),
                ["Add request validation"] = ("Voeg verzoekvalidatie toe", "Anforderungsvalidierung hinzufügen"),
                ["Setup log aggregation"] = ("Stel logaggregatie in", "Log-Aggregation einrichten"),
                ["Implement circuit breaker"] = ("Implementeer circuit breaker", "Circuit Breaker implementieren"),
                ["Create integration tests"] = ("Maak integratietests", "Integrationstests erstellen"),
                ["Add dark mode support"] = ("Voeg donkere modus toe", "Dunkelmodus-Unterstützung hinzufügen"),
                ["Implement infinite scroll"] = ("Implementeer oneindig scrollen", "Unendliches Scrollen implementieren"),
                ["Create webhook system"] = ("Maak webhook-systeem", "Webhook-System erstellen")
            };

            // Projects that require multilingual titles (nl-NL, de-DE in addition to en-US)
            // Using indices 2 (Cloud Migration Initiative) and 38 (Localization Project)
            var multilingualProjectIndices = new HashSet<int> { 2, 38 };
            var multilingualProjectIds = new System.Collections.Concurrent.ConcurrentBag<Guid>(); // Track actual project IDs for multilingual projects

            var users = new[] { "alice@company.com", "bob@company.com", "carol@company.com",
                "david@company.com", "eve@company.com", "frank@company.com",
                "grace@company.com", "henry@company.com", "iris@company.com", "jack@company.com" };

            var roles = new[] { "Developer", "Designer", "QA Engineer", "DevOps", "Product Manager", "Tech Lead" };

            // Create a mapping of project indices to hardcoded demo project IDs
            // This ensures consistent IDs across demo data regenerations
            var demoProjectIdsMap = new Dictionary<int, string>
            {
                // Legacy projects (will use old Project.Completed event)
                { 0, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.CustomerPortalRedesign },
                { 10, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.MarketingAutomationTool },
                { 20, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.SocialMediaIntegration },
                { 30, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.ReportingEngine },
                { 40, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.OAuthProviderService },

                // New event projects (will use specific outcome events)
                { 1, TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.MobileBankingApp },
                { 3, TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.DataAnalyticsPlatform },
                { 24, TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.MicroservicesMigration },
                { 33, TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.GraphQLApiDevelopment },
                { 39, TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.ABTestingFramework },

                // Schema versioning demo projects (MemberJoinedProject V1 vs V2)
                { 5, TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.EnterpriseCrmSystem },
                { 15, TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.CloudSecurityPlatform },
                { 25, TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.DevOpsPipelineModernization }
            };

            // Create 50 projects in smaller batches to avoid 409 conflicts
            const int projectBatchSize = 5;
            const int totalProjects = 50;
            await hubContext.BroadcastSeedProgress("blob", 150, 150 + 50 + 1000, "Creating projects...");

            for (int batch = 0; batch < 50; batch += projectBatchSize)
            {
                var batchTasks = new List<Task<Guid>>();
                var batchEnd = Math.Min(batch + projectBatchSize, 50);

                for (int i = batch; i < batchEnd; i++)
                {
                    var index = i; // Capture for closure
                    batchTasks.Add(Task.Run(async () =>
                    {
                        // Use hardcoded ID for demo projects, otherwise generate random GUID
                        var projectId = demoProjectIdsMap.ContainsKey(index)
                            ? Guid.Parse(demoProjectIdsMap[index])
                            : Guid.NewGuid();

                        var project = await projectFactory.CreateAsync(ProjectId.From(projectId.ToString()));

                        var (name, description) = projectTemplates[index];

                        // Legacy demo projects need to be initiated over a year ago (so they can be completed over a year ago)
                        // New event demo projects should be recent
                        // Schema versioning demo projects have specific timing for V1 vs V2 events
                        // Check if this is a legacy or new event demo project
                        var isLegacyDemo = index == 0 || index == 10 || index == 20 || index == 30 || index == 40;
                        var isNewEventDemo = index == 1 || index == 3 || index == 24 || index == 33 || index == 39;
                        var isSchemaVersionV1Demo = index == 5; // Enterprise CRM - old project with V1 events only
                        var isSchemaVersionV2Demo = index == 15; // Cloud Security - new project with V2 events only
                        var isSchemaVersionMixedDemo = index == 25; // DevOps Pipeline - mixed V1 and V2 events

                        int daysAgo;
                        if (isLegacyDemo || isSchemaVersionV1Demo)
                        {
                            daysAgo = random.Next(420, 500); // Legacy/V1 projects initiated 420-500 days ago
                        }
                        else if (isNewEventDemo || isSchemaVersionV2Demo)
                        {
                            daysAgo = random.Next(50, 90); // New event/V2 projects initiated 50-90 days ago
                        }
                        else if (isSchemaVersionMixedDemo)
                        {
                            daysAgo = random.Next(200, 250); // Mixed project started in the middle (before V2, still active)
                        }
                        else
                        {
                            daysAgo = random.Next(30, 365); // Other projects created between 30-365 days ago
                        }

                        var projectStartDate = now.AddDays(-daysAgo);
                        var owner = users[random.Next(users.Length)];

                        await project.InitiateProject(name, description, GetUserId(owner), null, projectStartDate);

                        // Store initiation date for later use (e.g., when completing demo projects)
                        projectInitiationDates[projectId] = projectStartDate;

                        // Configure multilingual support for specific projects
                        if (multilingualProjectIndices.Contains(index))
                        {
                            var languageConfigDate = projectStartDate.AddHours(random.Next(1, 8)); // Configure languages shortly after creation
                            await project.ConfigureLanguages(
                                new[] { "en-US", "nl-NL", "de-DE" },
                                GetUserId(owner),
                                null,
                                languageConfigDate);
                            multilingualProjectIds.Add(projectId); // Track this project for work item translations
                        }

                        // Add team members a few days after project initiation
                        // Use V1 or V2 MemberJoined events based on project type for schema versioning demo
                        var teamSize = random.Next(2, 6);
                        var addedUsers = new HashSet<string>();
                        for (int t = 0; t < teamSize; t++)
                        {
                            var user = users[random.Next(users.Length)];
                            if (!addedUsers.Contains(user))
                            {
                                addedUsers.Add(user);
                                var memberJoinDate = projectStartDate.AddDays(random.Next(1, 7)); // Members join within first week
                                var role = roles[random.Next(roles.Length)];

                                // Schema versioning demo: use V1 for old projects, V2 for new projects
                                if (isSchemaVersionV2Demo)
                                {
                                    // V2: New projects use permissions
                                    var permissions = GetPermissionsForRole(role);
                                    await project.AddTeamMemberWithPermissions(GetUserId(user), role, permissions, GetUserId(users[0]), null, memberJoinDate);
                                }
                                else if (isSchemaVersionMixedDemo && t >= teamSize / 2)
                                {
                                    // Mixed: Later members (second half) use V2 with permissions
                                    var permissions = GetPermissionsForRole(role);
                                    await project.AddTeamMemberWithPermissions(GetUserId(user), role, permissions, GetUserId(users[0]), null, memberJoinDate);
                                }
                                else
                                {
                                    // V1: Legacy and early members use old format (no permissions)
#pragma warning disable CS0618 // Type or member is obsolete
                                    await project.AddTeamMember(GetUserId(user), role, GetUserId(users[0]), null, memberJoinDate);
#pragma warning restore CS0618
                                }
                            }
                        }

                        return projectId;
                    }));
                }

                // Wait for this batch to complete before starting next batch
                var batchResults = await Task.WhenAll(batchTasks);
                projectIds.AddRange(batchResults);

                // Report progress after each batch
                var projectsCompleted = Math.Min(batch + projectBatchSize, totalProjects);
                await hubContext.BroadcastSeedProgress("blob", 150 + projectsCompleted, 150 + 50 + 1000, $"Creating projects ({projectsCompleted}/{totalProjects})...");

                // Small delay between batches to reduce storage pressure
                await Task.Delay(100);
            }

            // Helper function to generate random working hours timestamp (9 AM to 5 PM)
            DateTime GetWorkingHoursTimestamp(DateTime baseDate, Random rng)
            {
                var hour = rng.Next(9, 17); // 9 AM to 5 PM (exclusive end)
                var minute = rng.Next(0, 60);
                var second = rng.Next(0, 60);
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hour, minute, second, DateTimeKind.Utc);
            }

            // Create special "Time Travel Demo Project" with many events for testing time travel feature
            // Events are interleaved: project events, work items, team changes, more work items
            var timeTravelProjectId = Guid.NewGuid();
            var timeTravelProject = await projectFactory.CreateAsync(ProjectId.From(timeTravelProjectId.ToString()));
            await timeTravelProject.InitiateProject(
                "Time Travel Demo Project",
                "A project with many events for testing the time travel feature",
                GetUserId("eve@company.com"),  // Product Manager initiates the project
                null,  // No VersionToken for demo data
                GetWorkingHoursTimestamp(now.AddDays(-60), random));

            var timeTravelWorkItems = new List<Guid>();
            var timeTravelMembers = new[] { "eve@company.com" }; // Start with just PM

            // Create first 3 work items right after project initiation
            for (int i = 0; i < 3; i++)
            {
                var taskCreatedAt = GetWorkingHoursTimestamp(now.AddDays(-59 + i), random);
                var workItemId = WorkItemId.New();
                var workItem = await workItemFactory.CreateAsync(workItemId);

                var planResult = await workItem.PlanTask(
                    timeTravelProjectId.ToString(),
                    $"Time Travel Task {i + 1}",
                    $"Initial planning task - created early in project lifecycle",
                    WorkItemPriority.High,
                    GetUserId("eve@company.com"),
                    null,  // No VersionToken for demo data
                    taskCreatedAt);

                if (planResult.IsSuccess)
                {
                    await timeTravelProject.AddWorkItem(workItemId, GetUserId("eve@company.com"), null, taskCreatedAt);
                    timeTravelWorkItems.Add(workItemId.Value);
                }
            }

            // Designer joins 3 days after project initiation
            await timeTravelProject.AddTeamMember(GetUserId("alice@company.com"), "Designer", GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-57), random));

            // Create 2 more work items after designer joins
            for (int i = 3; i < 5; i++)
            {
                var taskCreatedAt = GetWorkingHoursTimestamp(now.AddDays(-56 + (i - 3)), random);
                var workItemId = WorkItemId.New();
                var workItem = await workItemFactory.CreateAsync(workItemId);

                var planResult = await workItem.PlanTask(
                    timeTravelProjectId.ToString(),
                    $"Time Travel Task {i + 1}",
                    $"Design task - created after designer joined",
                    WorkItemPriority.Medium,
                    GetUserId("eve@company.com"),
                    null,  // No VersionToken for demo data
                    taskCreatedAt);

                if (planResult.IsSuccess)
                {
                    await timeTravelProject.AddWorkItem(workItemId, GetUserId("eve@company.com"), null, taskCreatedAt);
                    timeTravelWorkItems.Add(workItemId.Value);
                    // Assign to designer
                    await workItem.AssignResponsibility("alice@company.com", GetUserId("eve@company.com"), null,
                        taskCreatedAt.AddHours(random.Next(2, 8)));
                }
            }

            // Developer joins
            await timeTravelProject.AddTeamMember(GetUserId("bob@company.com"), "Developer", GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-54), random));

            timeTravelMembers = new[] { "alice@company.com", "bob@company.com", "eve@company.com" };

            // Create 4 more work items, assign and start work on some
            for (int i = 5; i < 9; i++)
            {
                var taskCreatedAt = GetWorkingHoursTimestamp(now.AddDays(-52 + (i - 5)), random);
                var workItemId = WorkItemId.New();
                var workItem = await workItemFactory.CreateAsync(workItemId);

                var planResult = await workItem.PlanTask(
                    timeTravelProjectId.ToString(),
                    $"Time Travel Task {i + 1}",
                    $"Development task - created after developer joined",
                    (WorkItemPriority)(i % 4),
                    GetUserId("eve@company.com"),
                    null,  // No VersionToken for demo data
                    taskCreatedAt);

                if (planResult.IsSuccess)
                {
                    await timeTravelProject.AddWorkItem(workItemId, GetUserId("eve@company.com"), null, taskCreatedAt);
                    timeTravelWorkItems.Add(workItemId.Value);

                    // Assign to team member
                    var assignee = timeTravelMembers[i % 3];
                    var assignedAt = taskCreatedAt.AddHours(random.Next(4, 24));
                    await workItem.AssignResponsibility(assignee, GetUserId("eve@company.com"), null, assignedAt);

                    // Start work on first 2
                    if (i < 7)
                    {
                        var workStartedAt = assignedAt.AddHours(random.Next(6, 48));
                        await workItem.CommenceWork(UserProfileId.From(assignee), null, workStartedAt);
                        await timeTravelProject.UpdateWorkItemStatus(
                            workItemId,
                            WorkItemStatus.Planned,
                            WorkItemStatus.InProgress,
                            UserProfileId.From(assignee),
                            null,  // No VersionToken for demo data
                            workStartedAt);

                        // Complete first one
                        if (i == 5)
                        {
                            var completedAt = workStartedAt.AddDays(random.Next(1, 3));
                            await workItem.CompleteWork("Successfully completed", UserProfileId.From(assignee), null, completedAt);
                            await timeTravelProject.UpdateWorkItemStatus(
                                workItemId,
                                WorkItemStatus.InProgress,
                                WorkItemStatus.Completed,
                                UserProfileId.From(assignee),
                                null,  // No VersionToken for demo data
                                completedAt);
                        }
                    }
                }
            }

            // Rename project
            var renameTimestamp = GetWorkingHoursTimestamp(now.AddDays(-48), random);
            await timeTravelProject.RebrandProject("Time Travel Test Project", GetUserId("eve@company.com"), null, renameTimestamp);

            // DevOps and QA join
            await timeTravelProject.AddTeamMember(GetUserId("david@company.com"), "DevOps", GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-46), random));
            await timeTravelProject.AddTeamMember(GetUserId("carol@company.com"), "QA Engineer", GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-45), random));

            timeTravelMembers = new[] { "alice@company.com", "bob@company.com", "carol@company.com", "david@company.com", "eve@company.com" };

            // Create remaining work items with full team
            for (int i = 9; i < 15; i++)
            {
                var taskCreatedAt = GetWorkingHoursTimestamp(now.AddDays(-43 + (i - 9)), random);
                var workItemId = WorkItemId.New();
                var workItem = await workItemFactory.CreateAsync(workItemId);

                var planResult = await workItem.PlanTask(
                    timeTravelProjectId.ToString(),
                    $"Time Travel Task {i + 1}",
                    $"Full team task - created with complete team",
                    (WorkItemPriority)(i % 4),
                    GetUserId("eve@company.com"),
                    null,  // No VersionToken for demo data
                    taskCreatedAt);

                if (planResult.IsSuccess)
                {
                    await timeTravelProject.AddWorkItem(workItemId, GetUserId("eve@company.com"), null, taskCreatedAt);
                    timeTravelWorkItems.Add(workItemId.Value);

                    if (i < 12)
                    {
                        var assignee = timeTravelMembers[i % 5];
                        if (assignee != "eve@company.com") // Don't assign to PM
                        {
                            var assignedAt = taskCreatedAt.AddHours(random.Next(2, 12));
                            await workItem.AssignResponsibility(assignee, GetUserId("eve@company.com"), null, assignedAt);

                            if (i < 11)
                            {
                                var workStartedAt = assignedAt.AddHours(random.Next(6, 24));
                                await workItem.CommenceWork(UserProfileId.From(assignee), null, workStartedAt);
                                await timeTravelProject.UpdateWorkItemStatus(
                                    workItemId,
                                    WorkItemStatus.Planned,
                                    WorkItemStatus.InProgress,
                                    UserProfileId.From(assignee),
                                    null,  // No VersionToken for demo data
                                    workStartedAt);

                                if (i == 10)
                                {
                                    var completedAt = workStartedAt.AddDays(1);
                                    await workItem.CompleteWork("Completed with full team", UserProfileId.From(assignee), null, completedAt);
                                    await timeTravelProject.UpdateWorkItemStatus(
                                        workItemId,
                                        WorkItemStatus.InProgress,
                                        WorkItemStatus.Completed,
                                        UserProfileId.From(assignee),
                                        null,  // No VersionToken for demo data
                                        completedAt);
                                }
                            }
                        }
                    }
                }
            }

            // DevOps leaves
            await timeTravelProject.RemoveTeamMember(GetUserId("david@company.com"), GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-35), random));

            // Refine scope
            await timeTravelProject.RefineScope("Enhanced description: This project demonstrates time travel capabilities with a rich event history including multiple state changes and work item operations.", GetUserId("eve@company.com"), null,
                GetWorkingHoursTimestamp(now.AddDays(-34), random));

            projectIds.Add(timeTravelProjectId);

            // Create 1000 work items in smaller batches to avoid 409 conflicts
            // Pre-generate work item assignments to projects
            int totalWorkItems = 1000;
            var workItemsByProject = new Dictionary<Guid, List<(WorkItemPriority priority, string title, string description, string owner, DateTime createdDate, int assignChance, int progressChance, int completeChance, int deadlineChance, int reprioritizeChance, int estimateChance)>>();
            var allWorkItemIds = new System.Collections.Concurrent.ConcurrentBag<Guid>(); // Track work item IDs for projection updates

            for (int i = 0; i < totalWorkItems; i++)
            {
                var projectId = projectIds[random.Next(projectIds.Count)];

                if (!workItemsByProject.ContainsKey(projectId))
                {
                    workItemsByProject[projectId] = new List<(WorkItemPriority, string, string, string, DateTime, int, int, int, int, int, int)>();
                }

                // Pre-generate all random values
                var priority = random.Next(100) switch
                {
                    < 10 => WorkItemPriority.Critical,  // 10%
                    < 35 => WorkItemPriority.High,      // 25%
                    < 75 => WorkItemPriority.Medium,    // 40%
                    _ => WorkItemPriority.Low           // 25%
                };

                var title = workItemTitles[random.Next(workItemTitles.Length)];
                var description = $"Detailed implementation requirements for: {title}. This task involves multiple steps and coordination with the team.";
                var owner = users[random.Next(users.Length)];
                var workItemAge = random.Next(1, 365);
                var workItemCreatedDate = now.AddDays(-workItemAge);

                // Pre-generate random chances for various actions
                var assignChance = random.Next(100);
                var progressChance = random.Next(100);
                var completeChance = random.Next(100);
                var deadlineChance = random.Next(100);
                var reprioritizeChance = random.Next(100);
                var estimateChance = random.Next(100);

                workItemsByProject[projectId].Add((priority, title, description, owner, workItemCreatedDate, assignChance, progressChance, completeChance, deadlineChance, reprioritizeChance, estimateChance));
            }

            // Process projects in batches of 50 - each project processes its work items sequentially
            const int workItemProjectBatchSize = 50;
            var projectList = workItemsByProject.Keys.ToList();
            var workItemsProcessed = 0;

            await hubContext.BroadcastSeedProgress("blob", 200, 150 + 50 + 1000, "Creating work items...");

            for (int batch = 0; batch < projectList.Count; batch += workItemProjectBatchSize)
            {
                var batchEnd = Math.Min(batch + workItemProjectBatchSize, projectList.Count);
                var projectTasks = new List<Task>();

                for (int i = batch; i < batchEnd; i++)
                {
                    var projectId = projectList[i];
                    var workItems = workItemsByProject[projectId];

                    projectTasks.Add(Task.Run(async () =>
                    {
                        // Process all work items for this project sequentially
                        foreach (var (priority, title, description, owner, workItemCreatedDate, assignChance, progressChance, completeChance, deadlineChance, reprioritizeChance, estimateChance) in workItems)
                        {
                            // Create work item manually and add to project
                            var workItemId = WorkItemId.New();
                            var workItem = await workItemFactory.CreateAsync(workItemId);

                            // Build translations dictionary if this is a multilingual project
                            Dictionary<string, string>? titleTranslationsDict = null;
                            if (multilingualProjectIds.Contains(projectId) && workItemTitleTranslations.TryGetValue(title, out var translations))
                            {
                                titleTranslationsDict = new Dictionary<string, string>
                                {
                                    ["nl-NL"] = translations.Dutch,
                                    ["de-DE"] = translations.German
                                };
                            }

                            var planResult = await workItem.PlanTask(
                                projectId.ToString(),
                                title,
                                description,
                                priority,
                                UserProfileId.From(owner),
                                null,  // No VersionToken for demo data
                                workItemCreatedDate,
                                titleTranslationsDict);

                            if (!planResult.IsSuccess)
                            {
                                continue; // Skip this work item if planning failed
                            }

                            // Get the project once for all operations on this work item
                            var project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));

                            // Add work item to project
                            var addResult = await project.AddWorkItem(workItemId, UserProfileId.From(owner), null, workItemCreatedDate);
                            if (!addResult.IsSuccess)
                            {
                                continue; // Skip if adding failed
                            }

                            // Track work item ID for projection updates
                            allWorkItemIds.Add(workItemId.Value);

                            var isCompleted = false;
                            var currentStatus = WorkItemStatus.Planned;

                            // 70% of items get assigned
                            if (assignChance < 70)
                            {
                                var assignee = users[random.Next(users.Length)];
                                var assignedDate = workItemCreatedDate.AddDays(random.Next(1, 10));
                                await workItem.AssignResponsibility(assignee, UserProfileId.From(owner), null, assignedDate);

                                // 50% of assigned items are in progress
                                if (progressChance < 50)
                                {
                                    var commencedDate = assignedDate.AddDays(random.Next(1, 7));
                                    await workItem.CommenceWork(UserProfileId.From(assignee), null, commencedDate);

                                    // Need to fetch project again for status update
                                    project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));
                                    await project.UpdateWorkItemStatus(
                                        workItemId,
                                        currentStatus,
                                        WorkItemStatus.InProgress,
                                        UserProfileId.From(assignee),
                                        null,  // No VersionToken for demo data
                                        commencedDate);
                                    currentStatus = WorkItemStatus.InProgress;

                                    // 40% of in-progress items are completed
                                    if (completeChance < 40)
                                    {
                                        var completedDate = commencedDate.AddDays(random.Next(1, 14));
                                        await workItem.CompleteWork("Work completed successfully. All requirements met and tested.", UserProfileId.From(assignee), null, completedDate);

                                        // Need to fetch project again for status update
                                        project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));
                                        await project.UpdateWorkItemStatus(
                                            workItemId,
                                            currentStatus,
                                            WorkItemStatus.Completed,
                                            UserProfileId.From(assignee),
                                            null,  // No VersionToken for demo data
                                            completedDate);

                                        isCompleted = true;
                                    }
                                }
                            }

                            // Only set deadlines, reprioritize, and estimate on non-completed items
                            if (!isCompleted)
                            {
                                // 20% have deadlines (all in the future)
                                if (deadlineChance < 20)
                                {
                                    var daysUntilDeadline = random.Next(1, 90);
                                    await workItem.EstablishDeadline(now.AddDays(daysUntilDeadline), UserProfileId.From(owner), null);
                                }

                                // 10% get reprioritized
                                if (reprioritizeChance < 10)
                                {
                                    var newPriority = (WorkItemPriority)random.Next(4);
                                    await workItem.Reprioritize(newPriority, "Priority adjusted based on business needs", UserProfileId.From(owner), null);
                                }

                                // 15% have time estimates
                                if (estimateChance < 15)
                                {
                                    var hours = random.Next(2, 40);
                                    await workItem.ReestimateEffort(hours, UserProfileId.From(owner), null);
                                }
                            }
                        }
                    }));
                }

                // Wait for this batch of projects to complete before starting next batch
                await Task.WhenAll(projectTasks);

                // Count work items processed in this batch
                for (int i = batch; i < batchEnd; i++)
                {
                    workItemsProcessed += workItemsByProject[projectList[i]].Count;
                }
                await hubContext.BroadcastSeedProgress("blob", 200 + workItemsProcessed, 150 + 50 + 1000, $"Creating work items ({workItemsProcessed}/{totalWorkItems})...");

                Console.WriteLine($"[SEED] Processed {batchEnd} / {projectList.Count} projects...");
            }

            // UPCASTING DEMO: Complete 5 projects using the LEGACY ProjectCompleted event
            // These projects were completed ~140 days ago (before the domain schema change)
            // They will be automatically upcasted to the new specific outcome events when read
            // Use hardcoded project IDs for consistent demo across regenerations
            var legacyCompletionOutcomes = new[]
            {
                ("Project completed successfully with all deliverables met", 0, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.CustomerPortalRedesign),  // Will upcast to ProjectCompletedSuccessfully
                ("Project cancelled due to budget constraints", 10, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.MarketingAutomationTool),             // Will upcast to ProjectCancelled
                ("Project failed to meet technical requirements", 20, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.SocialMediaIntegration),           // Will upcast to ProjectFailed
                ("Project delivered to production environment", 30, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.ReportingEngine),             // Will upcast to ProjectDelivered
                ("Project suspended pending stakeholder approval", 40, TaskFlow.Domain.Constants.DemoProjectIds.Legacy.OAuthProviderService)           // Will upcast to ProjectSuspended
            };

            var legacyProjectIds = new List<Guid>();
            var legacyProjectNames = new List<string>();

            for (int i = 0; i < legacyCompletionOutcomes.Length; i++)
            {
                var (outcome, projectIndex, demoProjectId) = legacyCompletionOutcomes[i];
                if (projectIndex < projectIds.Count)
                {
                    // Use the hardcoded project ID (already set during project creation)
                    var projectId = projectIds[projectIndex];

                    var completedBy = users[random.Next(users.Length)];

                    // Load the project aggregate
                    var project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));

                    // Get the initiation date from our tracking dictionary
                    var initiationDate = projectInitiationDates[projectId];

                    // Legacy projects should be completed well over a year ago
                    // Calculate completion as: initiation + (30-60 days project duration)
                    // Since initiation is 420-500 days ago, completion will be ~390-440 days ago
                    var projectDuration = random.Next(30, 60); // Project ran for 30-60 days
                    var completionTimestamp = initiationDate.AddDays(projectDuration);

                    Console.WriteLine($"[SEED] Legacy project {projectId} (index {projectIndex}): Initiated {initiationDate:yyyy-MM-dd}, Completing at {completionTimestamp:yyyy-MM-dd} (duration: {projectDuration} days)");

                    try
                    {
                        // Add some intermediate events to make the event stream more interesting
                        // This demonstrates the zigzag visualization in the upcasting demo

                        // Add 2-4 team members at different times (all before completion)
                        var teamMemberCount = random.Next(2, 5);
                        for (int t = 0; t < teamMemberCount; t++)
                        {
                            var member = users[random.Next(users.Length)];
                            var role = roles[random.Next(roles.Length)];
                            var memberJoinedAt = completionTimestamp.AddDays(-random.Next(7, 30)); // Joined 1-4 weeks before completion
                            await project.AddTeamMember(UserProfileId.From(member), role, UserProfileId.From(completedBy), null, memberJoinedAt);
                        }

                        // Maybe rebrand the project (20% chance)
                        if (random.Next(100) < 20)
                        {
                            var newName = $"{project.Name} v2.0";
                            var rebrandedAt = completionTimestamp.AddDays(-random.Next(3, 20)); // Rebranded before completion
                            await project.RebrandProject(newName, UserProfileId.From(completedBy), null, rebrandedAt);
                        }

                        // Remove a team member occasionally (30% chance)
                        if (random.Next(100) < 30 && teamMemberCount > 1)
                        {
                            var memberToRemove = users[random.Next(users.Length)];
                            try
                            {
                                var memberLeftAt = completionTimestamp.AddDays(-random.Next(1, 10)); // Left shortly before completion
                                await project.RemoveTeamMember(UserProfileId.From(memberToRemove), UserProfileId.From(completedBy), null, memberLeftAt);
                            }
                            catch
                            {
                                // Member might not exist, that's ok
                            }
                        }

                        // Complete the project with legacy event at the historical timestamp
#pragma warning disable CS0618
                        await project.CompleteProject(outcome, UserProfileId.From(completedBy), null, completionTimestamp);
#pragma warning restore CS0618
                        Console.WriteLine($"[SEED] ✓ Successfully completed project: {project.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SEED] ✗ Failed to setup/complete project: {ex.Message}");
                        throw;
                    }

                    legacyProjectIds.Add(projectId);
                    legacyProjectNames.Add(project.Name ?? "Unknown");

                    Console.WriteLine($"[SEED] Completed project: {project.Name}, Outcome: {project.Outcome}");
                }
            }

            // VERIFY: Check that the legacy events were actually stored
            Console.WriteLine($"\n[SEED] === VERIFICATION: Reading back legacy ProjectCompleted events ===");
            for (int i = 0; i < legacyProjectIds.Count; i++)
            {
                try
                {
                    var projectId = legacyProjectIds[i];
                    var doc = await objectDocumentFactory.GetAsync("project", projectId.ToString());
                    var stream = eventStreamFactory.Create(doc);
                    var events = await stream.ReadAsync();

                    var completedEvent = events.FirstOrDefault(e => e.EventType == "Project.Completed");
                    if (completedEvent != null)
                    {
                        Console.WriteLine($"[SEED] ✓ Found Project.Completed at version {completedEvent.EventVersion} for {legacyProjectNames[i]}");
                    }
                    else
                    {
                        Console.WriteLine($"[SEED] ✗ WARNING: No Project.Completed event found for {legacyProjectNames[i]}!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED] ✗ Error verifying {legacyProjectNames[i]}: {ex.Message}");
                }
            }
            Console.WriteLine($"[SEED] === END VERIFICATION ===\n");

            // NEW EVENTS DEMO: Complete 5 projects using the NEW specific outcome events
            // These projects were completed recently (10-30 days ago) after the domain schema change
            // They demonstrate the proper way to complete projects with explicit outcome types
            // Use hardcoded project IDs for consistent demo across regenerations
            var newEventCompletions = new[]
            {
                (1, "CompletedSuccessfully", "All features delivered ahead of schedule with excellent quality metrics", TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.MobileBankingApp),  // Mobile Banking App
                (3, "Cancelled", "Stakeholder decided to pursue alternative solution", TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.DataAnalyticsPlatform),  // Data Analytics Platform
                (24, "Failed", "Unable to achieve required performance benchmarks after multiple iterations", TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.MicroservicesMigration),  // Microservices Migration
                (33, "Delivered", "Successfully deployed to production with zero downtime. All acceptance criteria met.", TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.GraphQLApiDevelopment),  // GraphQL API Development
                (39, "Suspended", "Waiting for legal approval on data privacy compliance. Will resume in Q2.", TaskFlow.Domain.Constants.DemoProjectIds.NewEvents.ABTestingFramework)  // A/B Testing Framework
            };

            var newEventProjectIds = new List<Guid>();
            var newEventProjectNames = new List<string>();

            for (int i = 0; i < newEventCompletions.Length; i++)
            {
                var (projectIndex, outcomeType, message, demoProjectId) = newEventCompletions[i];
                if (projectIndex < projectIds.Count)
                {
                    // Use the hardcoded project ID (already set during project creation)
                    var projectId = projectIds[projectIndex];

                    var project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));
                    var user = users[random.Next(users.Length)];

                    // Get the initiation date from our tracking dictionary
                    var initiationDate = projectInitiationDates[projectId];

                    // New event projects should be completed in the past 6 weeks
                    // They were initiated 50-90 days ago, so they ran for some time before completion
                    // Calculate completion to be in the past 1-42 days, but after initiation
                    var daysAgoToComplete = random.Next(1, 43); // Complete 1-42 days ago
                    var completedAt = now.AddDays(-daysAgoToComplete);

                    // Ensure completion is after initiation
                    if (completedAt < initiationDate)
                    {
                        // If our random completion date is before initiation, adjust it to be after
                        var projectDuration = random.Next(30, 40); // Project duration
                        completedAt = initiationDate.AddDays(projectDuration);
                    }

                    Console.WriteLine($"[SEED] New event project {projectId} (index {projectIndex}): Initiated {initiationDate:yyyy-MM-dd}, Completing at {completedAt:yyyy-MM-dd}");

                    // Add intermediate events to create a richer event stream for visualization
                    var teamMemberCount = random.Next(2, 5); // Add 2-4 team members
                    for (int t = 0; t < teamMemberCount; t++)
                    {
                        var member = users[random.Next(users.Length)];
                        var role = roles[random.Next(roles.Length)];
                        var memberJoinedAt = completedAt.AddDays(-random.Next(7, 30)); // Joined 1-4 weeks before completion
                        await project.AddTeamMember(UserProfileId.From(member), role, UserProfileId.From(user), null, memberJoinedAt);
                    }

                    // Maybe rebrand the project (20% chance)
                    if (random.Next(100) < 20)
                    {
                        var newName = project.Name + " v2";
                        var rebrandedAt = completedAt.AddDays(-random.Next(3, 20)); // Rebranded before completion
                        await project.RebrandProject(newName, UserProfileId.From(user), null, rebrandedAt);
                    }

                    // Maybe remove a team member (30% chance, but only if we have more than 1)
                    if (random.Next(100) < 30 && teamMemberCount > 1)
                    {
                        var member = users[random.Next(users.Length)];
                        var memberLeftAt = completedAt.AddDays(-random.Next(1, 10)); // Left shortly before completion
                        await project.RemoveTeamMember(UserProfileId.From(member), UserProfileId.From(user), null, memberLeftAt);
                    }

                    // Call the appropriate completion method based on outcome type
                    switch (outcomeType)
                    {
                        case "CompletedSuccessfully":
                            await project.CompleteProjectSuccessfully(message, UserProfileId.From(user), null, completedAt);
                            break;
                        case "Cancelled":
                            await project.CancelProject(message, UserProfileId.From(user), null, completedAt);
                            break;
                        case "Failed":
                            await project.FailProject(message, UserProfileId.From(user), null, completedAt);
                            break;
                        case "Delivered":
                            await project.DeliverProject(message, UserProfileId.From(user), null, completedAt);
                            break;
                        case "Suspended":
                            await project.SuspendProject(message, UserProfileId.From(user), null, completedAt);
                            break;
                    }

                    newEventProjectIds.Add(projectId);
                    newEventProjectNames.Add(project.Name ?? "Unknown");
                }
            }

            // After seeding is complete, trigger projection updates
            // Reload aggregates to get proper version tokens from their Metadata
            Console.WriteLine($"[SEED] Triggering projection updates for {projectIds.Count} projects, {allWorkItemIds.Count} work items, {userIdByEmail.Count} user profiles...");

            var projectionEvents = new List<TaskFlow.Domain.Messaging.ProjectionUpdateRequested>();

            // Add project events - reload each project to get proper version token
            foreach (var projectId in projectIds)
            {
                try
                {
                    var project = await projectFactory.GetAsync(ProjectId.From(projectId.ToString()));
                    var versionToken = project.Metadata!.ToVersionToken("project").ToLatestVersion();
                    projectionEvents.Add(new TaskFlow.Domain.Messaging.ProjectionUpdateRequested
                    {
                        VersionToken = versionToken,
                        ObjectName = "project",
                        StreamIdentifier = project.Metadata!.StreamId!,
                        EventCount = 1
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED] Warning: Could not load project {projectId} for projection update: {ex.Message}");
                }
            }

            // Add work item events - reload each work item to get proper version token
            foreach (var workItemId in allWorkItemIds)
            {
                try
                {
                    var workItem = await workItemFactory.GetAsync(WorkItemId.From(workItemId.ToString()));
                    var versionToken = workItem.Metadata!.ToVersionToken("workitem").ToLatestVersion();
                    projectionEvents.Add(new TaskFlow.Domain.Messaging.ProjectionUpdateRequested
                    {
                        VersionToken = versionToken,
                        ObjectName = "workitem",
                        StreamIdentifier = workItem.Metadata!.StreamId!,
                        EventCount = 1
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED] Warning: Could not load work item {workItemId} for projection update: {ex.Message}");
                }
            }

            // Add user profile events - reload each user profile to get proper version token
            foreach (var userId in userIdByEmail.Values)
            {
                try
                {
                    var userProfile = await userProfileFactory.GetAsync(userId);
                    var versionToken = userProfile.Metadata!.ToVersionToken("userprofile").ToLatestVersion();
                    projectionEvents.Add(new TaskFlow.Domain.Messaging.ProjectionUpdateRequested
                    {
                        VersionToken = versionToken,
                        ObjectName = "userprofile",
                        StreamIdentifier = userProfile.Metadata!.StreamId!,
                        EventCount = 1
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED] Warning: Could not load user profile {userId} for projection update: {ex.Message}");
                }
            }

            Console.WriteLine($"[SEED] Loaded {projectionEvents.Count} version tokens. Updating projections...");

            // Call all projection handlers with the events
            foreach (var handler in projectionHandlers)
            {
                try
                {
                    Console.WriteLine($"[SEED] Updating projection: {handler.ProjectionName}");
                    await handler.HandleBatchAsync(projectionEvents);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED] Warning: Failed to update projection {handler.ProjectionName}: {ex.Message}");
                }
            }

            Console.WriteLine($"[SEED] Projection updates complete.");

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully generated {projectIds.Count} projects (including Time Travel Demo Project, {legacyProjectIds.Count} with legacy events for upcasting demo, and {newEventProjectIds.Count} with new specific outcome events) and {totalWorkItems} work items with historical data spanning the last year. Projections will update and persist within ~2 seconds after activity stops.",
                projectsCreated = projectIds.Count,
                workItemsCreated = totalWorkItems,

                // Time Travel Demo
                timeTravelProject = new
                {
                    id = timeTravelProjectId,
                    name = "Time Travel Demo Project",
                    description = "Demonstrates event sourcing time travel with rich event history",
                    eventCount = 23, // Approximate count
                    outcomes = new[]
                    {
                        new { version = 7, outcome = "David (DevOps) leaves the project", eventType = "Project.MemberLeft" }
                    }
                },

                // Legacy Event Upcasting Demo (Old way - will be upcasted)
                legacyEventDemo = new
                {
                    description = "These 5 projects use the OLD 'ProjectCompleted' event format. The upcaster automatically converts them to specific outcome events when read.",
                    projects = legacyProjectIds.Select((id, index) => new
                    {
                        id = id,
                        name = legacyProjectNames[index],
                        legacyEventType = "Project.Completed",
                        upcastsTo = new[] {
                            "Project.CompletedSuccessfully",
                            "Project.Cancelled",
                            "Project.Failed",
                            "Project.Delivered",
                            "Project.Suspended"
                        }[index],
                        outcome = new[] {
                            "Successful",
                            "Cancelled",
                            "Failed",
                            "Delivered",
                            "Suspended"
                        }[index]
                    }).ToArray()
                },

                // New Specific Events Demo (New way - proper event design)
                newEventDemo = new
                {
                    description = "These 5 projects use the NEW specific outcome events. Each outcome has its own event type with a strongly-typed enum state.",
                    projects = newEventProjectIds.Select((id, index) => new
                    {
                        id = id,
                        name = newEventProjectNames[index],
                        eventType = new[] {
                            "Project.CompletedSuccessfully",
                            "Project.Cancelled",
                            "Project.Failed",
                            "Project.Delivered",
                            "Project.Suspended"
                        }[index],
                        outcome = new[] {
                            "Successful",
                            "Cancelled",
                            "Failed",
                            "Delivered",
                            "Suspended"
                        }[index],
                        enumValue = new[] {
                            "ProjectOutcome.Successful",
                            "ProjectOutcome.Cancelled",
                            "ProjectOutcome.Failed",
                            "ProjectOutcome.Delivered",
                            "ProjectOutcome.Suspended"
                        }[index]
                    }).ToArray()
                },

                // Schema Versioning Demo (EventVersion attribute - same event name, different schema versions)
                schemaVersioningDemo = new
                {
                    description = "These 3 projects demonstrate the [EventVersion] attribute. The same event name 'Project.MemberJoined' has two schema versions: V1 (legacy, no permissions) and V2 (current, with permissions).",
                    eventName = "Project.MemberJoined",
                    v1Project = new
                    {
                        id = TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.EnterpriseCrmSystem,
                        name = "Enterprise CRM System",
                        schemaVersion = 1,
                        description = "Old project (400+ days ago) - all team members added with V1 events (no permissions tracked)",
                        eventFormat = "MemberJoinedProjectV1(MemberId, Role, InvitedBy, JoinedAt)"
                    },
                    v2Project = new
                    {
                        id = TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.CloudSecurityPlatform,
                        name = "Cloud Security Platform",
                        schemaVersion = 2,
                        description = "New project (recent) - all team members added with V2 events (with permissions)",
                        eventFormat = "MemberJoinedProject(MemberId, Role, Permissions, InvitedBy, JoinedAt)"
                    },
                    mixedProject = new
                    {
                        id = TaskFlow.Domain.Constants.DemoProjectIds.SchemaVersioning.DevOpsPipelineModernization,
                        name = "DevOps Pipeline Modernization",
                        schemaVersions = "1 and 2 (mixed)",
                        description = "Transitional project - early members have V1 events, later members have V2 events with permissions",
                        eventFormat = "Both V1 and V2 events coexist in the same stream"
                    },
                    storageNote = "SchemaVersion=1 is NOT stored in JSON (saves space). SchemaVersion>=2 IS stored as 'schemaVersion' property."
                },

                projects = projectIds.Take(10).ToArray() // Return first 10 project IDs
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Seed Demo Data Failed",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500);
        }
    }

    private static async Task<IResult> SeedDemoUsers(
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IProjectionService projectionService)
    {
        try
        {
            // Create demo user profiles - UserProfileId is auto-generated, email is used for lookup via tags
            // Active team members who participate in projects
            var activeTeamMembers = new[]
            {
                ("Admin User", "admin@taskflow.demo", "System Administrator"),
                ("Alice Johnson", "alice@company.com", "Designer"),
                ("Bob Smith", "bob@company.com", "Developer"),
                ("Carol Davis", "carol@company.com", "QA Engineer"),
                ("David Martinez", "david@company.com", "DevOps"),
                ("Eve Wilson", "eve@company.com", "Product Manager"),
                ("Frank Brown", "frank@company.com", "Developer"),
                ("Grace Lee", "grace@company.com", "UX Researcher"),
                ("Henry Taylor", "henry@company.com", "Business Analyst"),
                ("Iris Chen", "iris@company.com", "Technical Writer"),
                ("Jack Anderson", "jack@company.com", "Scrum Master")
            };

            // Stakeholders who don't participate in projects (139 users to reach 150 total)
            var stakeholderFirstNames = new[] { "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "Richard", "Barbara", "Joseph", "Susan", "Thomas", "Jessica", "Christopher", "Sarah", "Charles", "Karen", "Daniel", "Nancy", "Matthew", "Lisa", "Anthony", "Betty", "Mark", "Margaret", "Donald", "Sandra", "Steven", "Ashley", "Paul", "Kimberly", "Andrew", "Emily", "Joshua", "Donna", "Kenneth", "Michelle", "Kevin", "Dorothy", "Brian", "Carol", "George", "Amanda", "Timothy", "Melissa", "Ronald", "Deborah" };
            var stakeholderLastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores" };
            var stakeholderRoles = new[] { "Executive Sponsor", "Business Stakeholder", "Finance Director", "Legal Counsel", "Compliance Officer", "External Consultant", "Board Member", "Investor Relations", "Strategic Advisor", "Department Head" };

            var stakeholders = new List<(string name, string email, string role)>();
            for (int i = 0; i < 139; i++)
            {
                var firstName = stakeholderFirstNames[i % stakeholderFirstNames.Length];
                var lastName = stakeholderLastNames[i % stakeholderLastNames.Length];
                // Add number suffix to ensure unique emails
                var emailSuffix = i / (stakeholderFirstNames.Length * stakeholderLastNames.Length / 2) + 1;
                var email = $"{firstName.ToLower()}.{lastName.ToLower()}{(emailSuffix > 1 ? emailSuffix.ToString() : "")}@stakeholders.com";
                var role = stakeholderRoles[i % stakeholderRoles.Length];
                stakeholders.Add(($"{firstName} {lastName}", email, role));
            }

            // Combine active team members and stakeholders (150 total users)
            var demoUsers = activeTeamMembers.Concat(stakeholders.Select(s => (s.name, s.email, s.role))).ToArray();

            foreach (var (name, email, jobRole) in demoUsers)
            {
                // Use the new factory method which generates UserProfileId and creates the aggregate in one step
                var (result, userProfile) = await userProfileFactory.CreateProfileAsync(name, email, jobRole, createdByUser: null);

                if (result.IsFailure)
                {
                    var errors = string.Join(", ", result.Errors.ToArray().Select(e => e.Message));
                    Console.WriteLine($"[SEED-USERS] ✗ Failed to create user {name}: {errors}");
                }
                else
                {
                    var userId = userProfile!.Metadata!.Id!.Value;
                    Console.WriteLine($"[SEED-USERS] ✓ Created user {name} ({userId})");
                }
            }

            return Results.Ok(new
            {
                success = true,
                message = "Demo users seeded successfully. Use 'Build Projections' to update read models.",
                userCount = demoUsers.Length
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Seed Users Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static IResult GetStorageConnection(
        [FromServices] IConfiguration configuration)
    {
        // Get connection strings for both storage accounts
        var eventsConnectionString = configuration.GetConnectionString("events");
        var userProfileConnectionString = configuration.GetConnectionString("userprofile");

        var storageAccounts = new List<object>();

        // Add Store account (port 10010)
        if (!string.IsNullOrEmpty(eventsConnectionString))
        {
            var storeConnection = BuildStorageConnection(eventsConnectionString, "Store (Main Storage)",
                "Events, Projects, Work Items, Projections, Object Documents");
            storageAccounts.Add(storeConnection);
        }

        // Add UserDataStore account (port 10020)
        if (!string.IsNullOrEmpty(userProfileConnectionString))
        {
            var userDataConnection = BuildStorageConnection(userProfileConnectionString, "UserDataStore",
                "User Profiles, Object Documents");
            storageAccounts.Add(userDataConnection);
        }

        return Results.Ok(new
        {
            storageAccounts = storageAccounts,
            instructions = "Copy the connection string and paste it into Microsoft Azure Storage Explorer to browse the storage account."
        });
    }

    private static object BuildStorageConnection(string containerConnectionString, string name, string containers)
    {
        // Parse connection string to extract blob endpoint
        string blobEndpoint = "http://127.0.0.1:10010/devstoreaccount1";
        string queueEndpoint = "http://127.0.0.1:10011/devstoreaccount1";
        string tableEndpoint = "http://127.0.0.1:10012/devstoreaccount1";

        var parts = containerConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("BlobEndpoint=", StringComparison.OrdinalIgnoreCase))
            {
                blobEndpoint = part.Substring("BlobEndpoint=".Length);

                // Extract base URL and construct queue/table endpoints
                var uri = new Uri(blobEndpoint);
                var baseUrl = $"{uri.Scheme}://{uri.Host}";
                var accountName = uri.AbsolutePath.TrimStart('/');

                queueEndpoint = $"{baseUrl}:{uri.Port + 1}/{accountName}";
                tableEndpoint = $"{baseUrl}:{uri.Port + 2}/{accountName}";
                break;
            }
        }

        // Build full connection string without ContainerName
        var fullConnectionString = "DefaultEndpointsProtocol=http;" +
            "AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint={blobEndpoint};" +
            $"QueueEndpoint={queueEndpoint};" +
            $"TableEndpoint={tableEndpoint};";

        return new
        {
            connectionString = fullConnectionString,
            connectionName = name,
            containers = containers,
            isAzurite = true
        };
    }

    private static async Task<IResult> GetStorageDebugInfo(
        [FromServices] IConfiguration configuration,
        [FromServices] Microsoft.Extensions.Azure.IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient> clientFactory)
    {
        try
        {
            var debugInfo = new
            {
                connectionStrings = new
                {
                    // Storage account connection strings (for BlobServiceClient)
                    Store = configuration.GetConnectionString("Store") ?? "NOT FOUND",
                    userdataStore = configuration.GetConnectionString("userdataStore") ?? "NOT FOUND",

                    // Container connection strings (for BlobContainerClient)
                    events = configuration.GetConnectionString("events") ?? "NOT FOUND",
                    project = configuration.GetConnectionString("project") ?? "NOT FOUND",
                    workitem = configuration.GetConnectionString("workitem") ?? "NOT FOUND",
                    projections = configuration.GetConnectionString("projections") ?? "NOT FOUND",
                    storeObjectDocuments = configuration.GetConnectionString("store-object-document-store") ?? "NOT FOUND",
                    userProfile = configuration.GetConnectionString("userprofile") ?? "NOT FOUND",
                    userstoreObjectDocuments = configuration.GetConnectionString("userstore-object-document-store") ?? "NOT FOUND"
                },
                blobServiceClients = new
                {
                    Store = await GetContainersForClient(clientFactory, "Store"),
                    UserDataStore = await GetContainersForClient(clientFactory, "UserDataStore"),
                    BlobStorage = await GetContainersForClient(clientFactory, "BlobStorage")
                }
            };

            return Results.Ok(debugInfo);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Storage Debug Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<object> GetContainersForClient(
        Microsoft.Extensions.Azure.IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient> clientFactory,
        string clientName)
    {
        try
        {
            var client = clientFactory.CreateClient(clientName);
            var containers = new List<string>();

            await foreach (var container in client.GetBlobContainersAsync())
            {
                containers.Add(container.Name);
            }

            return new
            {
                status = "Success",
                containers = containers,
                containerCount = containers.Count
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "Error",
                error = ex.Message,
                containers = new List<string>()
            };
        }
    }

    private static async Task<IResult> GetCosmosDbDocuments(
        [FromServices] IServiceProvider serviceProvider,
        [FromQuery] string? objectName = null,
        [FromQuery] string? containerName = null)
    {
        try
        {
            var cosmosClient = serviceProvider.GetService<CosmosClient>();
            if (cosmosClient == null)
            {
                return Results.Ok(new { error = "CosmosDB not configured", documents = new List<object>() });
            }

            var db = cosmosClient.GetDatabase("eventstore");
            var targetContainer = containerName ?? "documents";
            var container = db.GetContainer(targetContainer);

            var query = string.IsNullOrEmpty(objectName)
                ? new QueryDefinition("SELECT * FROM c")
                : targetContainer == "events"
                    ? new QueryDefinition("SELECT * FROM c WHERE c.streamId = @streamId")
                        .WithParameter("@streamId", objectName)
                    : new QueryDefinition("SELECT * FROM c WHERE c.objectName = @objectName")
                        .WithParameter("@objectName", objectName);

            // Use JsonElement to preserve actual JSON values
            var documents = new List<System.Text.Json.JsonElement>();
            using var feedIterator = container.GetItemQueryIterator<System.Text.Json.JsonElement>(query);
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                foreach (var doc in response)
                {
                    documents.Add(doc);
                }
            }

            return Results.Ok(new
            {
                database = "eventstore",
                container = targetContainer,
                filter = objectName ?? "(all)",
                count = documents.Count,
                documents = documents
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "CosmosDB Query Failed",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetProjectionStatus(
        [FromServices] IProjectionService projectionService,
        [FromServices] IServiceProvider serviceProvider)
    {
        try
        {
            var activeWorkItemsFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ActiveWorkItemsFactory>();
            var projectDashboardFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ProjectDashboardFactory>();
            var userProfilesFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.UserProfilesFactory>();
            var eventUpcastingDemonstrationFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.EventUpcastingDemonstrationFactory>();
            var projectKanbanBoardFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ProjectKanbanBoardFactory>();

            var activeWorkItems = projectionService.GetActiveWorkItems();
            var projectDashboard = projectionService.GetProjectDashboard();
            var userProfiles = projectionService.GetUserProfiles();
            var eventUpcastingDemonstration = projectionService.GetEventUpcastingDemonstration();
            var projectKanbanBoard = projectionService.GetProjectKanbanBoard();

            // EpicSummary might fail if Table Storage isn't configured correctly
            EpicSummary? epicSummary = null;
            DateTimeOffset? epicSummaryLastModified = null;
            try
            {
                epicSummary = projectionService.GetEpicSummary();
                var epicSummaryFactory = serviceProvider.GetService<TaskFlow.Domain.Projections.EpicSummaryFactory>();
                if (epicSummaryFactory != null)
                {
                    epicSummaryLastModified = await epicSummaryFactory.GetLastModifiedAsync();
                }
            }
            catch
            {
                // EpicSummary might fail due to Table Storage configuration
            }

            // SprintDashboard is optional - only available when CosmosDB is configured
            var sprintDashboardFactory = serviceProvider.GetService<TaskFlow.Domain.Projections.ISprintDashboardFactory>();
            SprintDashboard? sprintDashboard = null;
            if (sprintDashboardFactory != null)
            {
                try
                {
                    sprintDashboard = await sprintDashboardFactory.GetAsync();
                }
                catch
                {
                    // CosmosDB might not be available
                }
            }

            // Get last modified timestamps from storage
            var activeWorkItemsLastModified = await activeWorkItemsFactory.GetLastModifiedAsync();
            var projectDashboardLastModified = await projectDashboardFactory.GetLastModifiedAsync();
            var userProfilesLastModified = await userProfilesFactory.GetLastModifiedAsync();
            var eventUpcastingDemonstrationLastModified = await eventUpcastingDemonstrationFactory.GetLastModifiedAsync();
            var projectKanbanBoardLastModified = await projectKanbanBoardFactory.GetLastModifiedAsync();

        // SprintDashboard uses CosmosDB - check if it exists
        var sprintDashboardExists = sprintDashboard != null && sprintDashboard.Checkpoint.Count > 0;

        var projections = new[]
        {
            new
            {
                name = "ActiveWorkItems",
                storageType = "Blob",
                status = activeWorkItemsLastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = activeWorkItemsLastModified,
                checkpoint = activeWorkItems.Checkpoint.Count,
                checkpointFingerprint = activeWorkItems.CheckpointFingerprint,
                eventCount = activeWorkItems.WorkItems.Count,
                pageCount = (int?)null,
                isPersisted = activeWorkItemsLastModified.HasValue,
                projectionStatus = activeWorkItems.Status.ToString(),
                schemaVersion = activeWorkItems.SchemaVersion,
                codeSchemaVersion = activeWorkItems.CodeSchemaVersion,
                needsSchemaUpgrade = activeWorkItems.NeedsSchemaUpgrade
            },
            new
            {
                name = "ProjectDashboard",
                storageType = "Blob",
                status = projectDashboardLastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = projectDashboardLastModified,
                checkpoint = projectDashboard.Checkpoint.Count,
                checkpointFingerprint = projectDashboard.CheckpointFingerprint,
                eventCount = projectDashboard.Projects.Count,
                pageCount = (int?)null,
                isPersisted = projectDashboardLastModified.HasValue,
                projectionStatus = projectDashboard.Status.ToString(),
                schemaVersion = projectDashboard.SchemaVersion,
                codeSchemaVersion = projectDashboard.CodeSchemaVersion,
                needsSchemaUpgrade = projectDashboard.NeedsSchemaUpgrade
            },
            new
            {
                name = "UserProfiles",
                storageType = "Blob",
                status = userProfilesLastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = userProfilesLastModified,
                checkpoint = userProfiles.Checkpoint.Count,
                checkpointFingerprint = userProfiles.CheckpointFingerprint,
                eventCount = userProfiles.TotalUsers,
                pageCount = (int?)userProfiles.TotalPages,
                isPersisted = userProfilesLastModified.HasValue,
                projectionStatus = userProfiles.Status.ToString(),
                schemaVersion = userProfiles.SchemaVersion,
                codeSchemaVersion = userProfiles.CodeSchemaVersion,
                needsSchemaUpgrade = userProfiles.NeedsSchemaUpgrade
            },
            new
            {
                name = "ProjectKanbanBoard",
                storageType = "Blob",
                status = projectKanbanBoardLastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = projectKanbanBoardLastModified,
                checkpoint = projectKanbanBoard.Checkpoint.Count,
                checkpointFingerprint = projectKanbanBoard.CheckpointFingerprint,
                eventCount = projectKanbanBoard.Projects.Count,
                pageCount = (int?)projectKanbanBoard.Registry.Destinations.Count,
                isPersisted = projectKanbanBoardLastModified.HasValue,
                projectionStatus = projectKanbanBoard.Status.ToString(),
                schemaVersion = projectKanbanBoard.SchemaVersion,
                codeSchemaVersion = projectKanbanBoard.CodeSchemaVersion,
                needsSchemaUpgrade = projectKanbanBoard.NeedsSchemaUpgrade
            },
            new
            {
                name = "EventUpcastingDemonstration",
                storageType = "Blob",
                status = eventUpcastingDemonstrationLastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = eventUpcastingDemonstrationLastModified,
                checkpoint = eventUpcastingDemonstration.Checkpoint.Count,
                checkpointFingerprint = eventUpcastingDemonstration.CheckpointFingerprint,
                eventCount = eventUpcastingDemonstration.DemoProjects.Count,
                pageCount = (int?)null,
                isPersisted = eventUpcastingDemonstrationLastModified.HasValue,
                projectionStatus = eventUpcastingDemonstration.Status.ToString(),
                schemaVersion = eventUpcastingDemonstration.SchemaVersion,
                codeSchemaVersion = eventUpcastingDemonstration.CodeSchemaVersion,
                needsSchemaUpgrade = eventUpcastingDemonstration.NeedsSchemaUpgrade
            },
            new
            {
                name = "EpicSummary",
                storageType = "Blob",
                status = epicSummary == null ? "not-configured" : (epicSummaryLastModified.HasValue ? "idle" : "not-persisted"),
                lastUpdate = epicSummaryLastModified,
                checkpoint = epicSummary?.Checkpoint.Count ?? 0,
                checkpointFingerprint = epicSummary?.CheckpointFingerprint ?? "",
                eventCount = epicSummary?.Epics.Count ?? 0,
                pageCount = (int?)null,
                isPersisted = epicSummaryLastModified.HasValue,
                projectionStatus = epicSummary?.Status.ToString() ?? "Unknown",
                schemaVersion = epicSummary?.SchemaVersion ?? 0,
                codeSchemaVersion = epicSummary?.CodeSchemaVersion ?? 0,
                needsSchemaUpgrade = epicSummary?.NeedsSchemaUpgrade ?? false
            },
            new
            {
                name = "SprintDashboard",
                storageType = "CosmosDB",
                status = sprintDashboard == null ? "not-configured" : (sprintDashboardExists ? "idle" : "not-persisted"),
                lastUpdate = (DateTimeOffset?)null, // CosmosDB doesn't track this the same way
                checkpoint = sprintDashboard?.Checkpoint.Count ?? 0,
                checkpointFingerprint = sprintDashboard?.CheckpointFingerprint ?? "",
                eventCount = sprintDashboard?.Sprints.Count ?? 0,
                pageCount = (int?)null,
                isPersisted = sprintDashboardExists,
                projectionStatus = sprintDashboard?.Status.ToString() ?? "Unknown",
                schemaVersion = sprintDashboard?.SchemaVersion ?? 0,
                codeSchemaVersion = sprintDashboard?.CodeSchemaVersion ?? 0,
                needsSchemaUpgrade = sprintDashboard?.NeedsSchemaUpgrade ?? false
            }
        };

            return Results.Ok(projections);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get projection status",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> RebuildProjection(
        string name,
        [FromServices] IProjectionService projectionService)
    {
        switch (name.ToLowerInvariant())
            {
                case "activeworkitems":
                    var activeWorkItems = projectionService.GetActiveWorkItems();
                    await activeWorkItems.UpdateToLatestVersion();
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = activeWorkItems.Checkpoint.Count,
                        checkpointFingerprint = activeWorkItems.CheckpointFingerprint
                    });

                case "projectdashboard":
                    var projectDashboard = projectionService.GetProjectDashboard();
                    await projectDashboard.UpdateToLatestVersion();
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = projectDashboard.Checkpoint.Count,
                        checkpointFingerprint = projectDashboard.CheckpointFingerprint
                    });

                case "userprofiles":
                    var userProfiles = projectionService.GetUserProfiles();
                    await userProfiles.UpdateToLatestVersion();
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = userProfiles.Checkpoint.Count,
                        checkpointFingerprint = userProfiles.CheckpointFingerprint
                    });

                case "eventupcastingdemonstration":
                    var eventUpcastingDemonstration = projectionService.GetEventUpcastingDemonstration();
                    await eventUpcastingDemonstration.UpdateToLatestVersion();
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = eventUpcastingDemonstration.Checkpoint.Count,
                        checkpointFingerprint = eventUpcastingDemonstration.CheckpointFingerprint
                    });

                case "projectkanbanboard":
                    var projectKanbanBoard = projectionService.GetProjectKanbanBoard();
                    await projectKanbanBoard.UpdateToLatestVersion();
                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = projectKanbanBoard.Checkpoint.Count,
                        checkpointFingerprint = projectKanbanBoard.CheckpointFingerprint
                    });

                case "sprintdashboard":
                    var sprintDashboardRebuild = projectionService.GetSprintDashboard();
                    if (sprintDashboardRebuild == null)
                    {
                        return Results.NotFound(new { message = $"Projection '{name}' not available - CosmosDB may not be configured" });
                    }

                    // For SprintDashboard, we need to discover all Sprint documents from CosmosDB
                    var cosmosClientRebuild = projectionService.GetCosmosClient();
                    var docFactoryRebuild = projectionService.GetObjectDocumentFactory();
                    var streamFactoryRebuild = projectionService.GetEventStreamFactory();

                    if (cosmosClientRebuild != null && docFactoryRebuild != null && streamFactoryRebuild != null)
                    {
                        var dbRebuild = cosmosClientRebuild.GetDatabase("eventstore");
                        var docsContainerRebuild = dbRebuild.GetContainer("documents");
                        var queryRebuild = new QueryDefinition("SELECT * FROM c WHERE c.objectName = 'sprint'");
                        using var feedIteratorRebuild = docsContainerRebuild.GetItemQueryIterator<dynamic>(queryRebuild);
                        while (feedIteratorRebuild.HasMoreResults)
                        {
                            var responseRebuild = await feedIteratorRebuild.ReadNextAsync();
                            foreach (var doc in responseRebuild)
                            {
                                string sprintObjId = doc.objectId;
                                var sprintDocRebuild = await docFactoryRebuild.GetAsync("sprint", sprintObjId, documentType: "cosmosdb");
                                var sprintStreamRebuild = streamFactoryRebuild.Create(sprintDocRebuild);
                                // Process all events for this sprint - use VersionToken to track checkpoint
                                var eventsRebuild = await sprintStreamRebuild.ReadAsync();
                                foreach (var evt in eventsRebuild)
                                {
                                    var vtRebuild = new ErikLieben.FA.ES.VersionToken(
                                        sprintDocRebuild.ObjectName,
                                        sprintDocRebuild.ObjectId,
                                        sprintDocRebuild.Active.StreamIdentifier,
                                        evt.EventVersion);
                                    await sprintDashboardRebuild.Fold(evt, vtRebuild);
                                    sprintDashboardRebuild.Checkpoint[vtRebuild.ObjectIdentifier] = vtRebuild.VersionIdentifier;
                                }
                            }
                        }
                        if (sprintDashboardRebuild.Checkpoint.Count > 0)
                        {
                            sprintDashboardRebuild.CheckpointFingerprint = string.Join(",", sprintDashboardRebuild.Checkpoint.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}").Take(10));
                        }
                    }

                    return Results.Ok(new
                    {
                        success = true,
                        message = $"Projection '{name}' rebuilt successfully",
                        checkpoint = sprintDashboardRebuild.Checkpoint.Count,
                        checkpointFingerprint = sprintDashboardRebuild.CheckpointFingerprint,
                        sprintCount = sprintDashboardRebuild.Sprints.Count
                    });

                default:
                    return Results.NotFound(new { message = $"Projection '{name}' not found" });
        }
    }

    /// <summary>
    /// Request body for setting projection status.
    /// </summary>
    public record SetProjectionStatusRequest(string Status);

    private static async Task<IResult> SetProjectionStatusEndpoint(
        string name,
        [FromBody] SetProjectionStatusRequest request,
        [FromServices] IServiceProvider serviceProvider)
    {
        if (!Enum.TryParse<ProjectionStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Results.BadRequest(new { message = $"Invalid status '{request.Status}'. Valid values are: Active, Rebuilding, Disabled" });
        }

        try
        {
            switch (name.ToLowerInvariant())
            {
                case "activeworkitems":
                    var activeWorkItemsFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ActiveWorkItemsFactory>();
                    await activeWorkItemsFactory.SetStatusAsync(status);
                    return Results.Ok(new { success = true, message = $"Projection '{name}' status set to {status}", status = status.ToString() });

                case "projectdashboard":
                    var projectDashboardFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ProjectDashboardFactory>();
                    await projectDashboardFactory.SetStatusAsync(status);
                    return Results.Ok(new { success = true, message = $"Projection '{name}' status set to {status}", status = status.ToString() });

                case "userprofiles":
                    var userProfilesFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.UserProfilesFactory>();
                    await userProfilesFactory.SetStatusAsync(status);
                    return Results.Ok(new { success = true, message = $"Projection '{name}' status set to {status}", status = status.ToString() });

                case "eventupcastingdemonstration":
                    var eventUpcastingFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.EventUpcastingDemonstrationFactory>();
                    await eventUpcastingFactory.SetStatusAsync(status);
                    return Results.Ok(new { success = true, message = $"Projection '{name}' status set to {status}", status = status.ToString() });

                case "projectkanbanboard":
                    var kanbanFactory = serviceProvider.GetRequiredService<TaskFlow.Domain.Projections.ProjectKanbanBoardFactory>();
                    await kanbanFactory.SetStatusAsync(status);
                    return Results.Ok(new { success = true, message = $"Projection '{name}' status set to {status}", status = status.ToString() });

                default:
                    return Results.NotFound(new { message = $"Projection '{name}' not found or status cannot be set for this projection type" });
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to set projection status",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> ResetProjection(
        string name,
        [FromServices] IProjectionService projectionService)
    {
        switch (name.ToLowerInvariant())
        {
            case "activeworkitems":
                var activeWorkItems = projectionService.GetActiveWorkItems();
                activeWorkItems.Checkpoint.Clear();
                activeWorkItems.WorkItems.Clear();
                await activeWorkItems.UpdateToLatestVersion();
                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = activeWorkItems.Checkpoint.Count,
                    checkpointFingerprint = activeWorkItems.CheckpointFingerprint,
                    itemCount = activeWorkItems.WorkItems.Count
                });

            case "projectdashboard":
                var projectDashboard = projectionService.GetProjectDashboard();
                projectDashboard.Checkpoint.Clear();
                projectDashboard.Projects.Clear();
                await projectDashboard.UpdateToLatestVersion();
                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = projectDashboard.Checkpoint.Count,
                    checkpointFingerprint = projectDashboard.CheckpointFingerprint,
                    projectCount = projectDashboard.Projects.Count
                });

            case "userprofiles":
                var userProfiles = projectionService.GetUserProfiles();
                userProfiles.Checkpoint.Clear();
                userProfiles.TotalUsers = 0;
                userProfiles.TotalPages = 0;
                userProfiles.ClearDestinations();
                await userProfiles.UpdateToLatestVersion();
                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = userProfiles.Checkpoint.Count,
                    checkpointFingerprint = userProfiles.CheckpointFingerprint,
                    profileCount = userProfiles.TotalUsers,
                    pageCount = userProfiles.TotalPages
                });

            case "eventupcastingdemonstration":
                var eventUpcastingDemonstration = projectionService.GetEventUpcastingDemonstration();
                eventUpcastingDemonstration.Checkpoint.Clear();
                eventUpcastingDemonstration.DemoProjects.Clear();
                await eventUpcastingDemonstration.UpdateToLatestVersion();
                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = eventUpcastingDemonstration.Checkpoint.Count,
                    checkpointFingerprint = eventUpcastingDemonstration.CheckpointFingerprint,
                    projectCount = eventUpcastingDemonstration.DemoProjects.Count
                });

            case "projectkanbanboard":
                var projectKanbanBoard = projectionService.GetProjectKanbanBoard();
                projectKanbanBoard.Checkpoint.Clear();
                // projectKanbanBoard.ClearPartitions();
                // projectKanbanBoard.Registry.Partitions.Clear();
                await projectKanbanBoard.UpdateToLatestVersion();
                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = projectKanbanBoard.Checkpoint.Count,
                    checkpointFingerprint = projectKanbanBoard.CheckpointFingerprint,
                    // partitionCount = projectKanbanBoard.Partitions.Count
                });

            case "sprintdashboard":
                var sprintDashboard = projectionService.GetSprintDashboard();
                if (sprintDashboard == null)
                {
                    return Results.NotFound(new { message = $"Projection '{name}' not available - CosmosDB may not be configured" });
                }
                sprintDashboard.Checkpoint.Clear();
                sprintDashboard.Sprints.Clear();

                // For SprintDashboard, we need to discover all Sprint documents from CosmosDB
                // since UpdateToLatestVersion only processes streams already in the checkpoint
                var sprintDocFactory = projectionService.GetObjectDocumentFactory();
                var sprintStreamFactory = projectionService.GetEventStreamFactory();
                if (sprintDocFactory != null && sprintStreamFactory != null)
                {
                    // Query CosmosDB for all Sprint documents
                    var cosmosClientForSprints = projectionService.GetCosmosClient();
                    if (cosmosClientForSprints != null)
                    {
                        var dbForSprints = cosmosClientForSprints.GetDatabase("eventstore");
                        var docsContainerForSprints = dbForSprints.GetContainer("documents");
                        var queryForSprints = new QueryDefinition("SELECT * FROM c WHERE c.objectName = 'sprint'");
                        using var feedIteratorForSprints = docsContainerForSprints.GetItemQueryIterator<dynamic>(queryForSprints);
                        while (feedIteratorForSprints.HasMoreResults)
                        {
                            var responseForSprints = await feedIteratorForSprints.ReadNextAsync();
                            foreach (var doc in responseForSprints)
                            {
                                string sprintObjectId = doc.objectId;
                                var sprintDoc = await sprintDocFactory.GetAsync("sprint", sprintObjectId, documentType: "cosmosdb");
                                var sprintStream = sprintStreamFactory.Create(sprintDoc);
                                var sprintEvents = await sprintStream.ReadAsync();
                                foreach (var evt in sprintEvents)
                                {
                                    var versionToken = new ErikLieben.FA.ES.VersionToken(
                                        sprintDoc.ObjectName,
                                        sprintDoc.ObjectId,
                                        sprintDoc.Active.StreamIdentifier,
                                        evt.EventVersion);
                                    await sprintDashboard.Fold(evt, versionToken);
                                    sprintDashboard.Checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;
                                }
                            }
                        }
                        sprintDashboard.CheckpointFingerprint = sprintDashboard.Checkpoint.Count > 0
                            ? string.Join(",", sprintDashboard.Checkpoint.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}").Take(10))
                            : "";
                    }
                }

                return Results.Ok(new
                {
                    success = true,
                    message = $"Projection '{name}' reset and rebuilt successfully",
                    checkpoint = sprintDashboard.Checkpoint.Count,
                    checkpointFingerprint = sprintDashboard.CheckpointFingerprint,
                    sprintCount = sprintDashboard.Sprints.Count
                });

            default:
                return Results.NotFound(new { message = $"Projection '{name}' not found" });
        }
    }

    /// <summary>
    /// Build all projections from the event store with progress tracking via SignalR
    /// </summary>
    private static async Task<IResult> BuildAllProjections(
        [FromServices] IProjectionService projectionService,
        [FromServices] IHubContext<TaskFlowHub> hubContext,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] ActiveWorkItemsFactory activeWorkItemsFactory,
        [FromServices] ProjectDashboardFactory projectDashboardFactory,
        [FromServices] UserProfilesFactory userProfilesFactory,
        [FromServices] EventUpcastingDemonstrationFactory eventUpcastingDemonstrationFactory,
        [FromServices] ProjectKanbanBoardFactory projectKanbanBoardFactory)
    {
        var results = new List<object>();
        var projectionNames = new[] { "ActiveWorkItems", "ProjectDashboard", "UserProfiles", "EventUpcastingDemonstration", "ProjectKanbanBoard", "EpicSummary", "SprintDashboard" };

        async Task SendProgress(string projectionName, string status, int current, int total, string? message = null)
        {
            await hubContext.Clients.All.SendAsync("ProjectionBuildProgress", new
            {
                projectionName,
                status,
                current,
                total,
                message,
                timestamp = DateTimeOffset.UtcNow
            });
        }

        try
        {
            int current = 0;
            int total = projectionNames.Length;

            // 1. ActiveWorkItems
            current++;
            await SendProgress("ActiveWorkItems", "building", current, total, "Building ActiveWorkItems projection...");
            var activeWorkItems = projectionService.GetActiveWorkItems();
            await activeWorkItems.UpdateToLatestVersion();
            await activeWorkItemsFactory.SaveAsync(activeWorkItems);
            results.Add(new { name = "ActiveWorkItems", success = true, checkpoint = activeWorkItems.Checkpoint.Count });
            await SendProgress("ActiveWorkItems", "completed", current, total);

            // 2. ProjectDashboard
            current++;
            await SendProgress("ProjectDashboard", "building", current, total, "Building ProjectDashboard projection...");
            var projectDashboard = projectionService.GetProjectDashboard();
            await projectDashboard.UpdateToLatestVersion();
            await projectDashboardFactory.SaveAsync(projectDashboard);
            results.Add(new { name = "ProjectDashboard", success = true, checkpoint = projectDashboard.Checkpoint.Count });
            await SendProgress("ProjectDashboard", "completed", current, total);

            // 3. UserProfiles
            current++;
            await SendProgress("UserProfiles", "building", current, total, "Building UserProfiles projection...");
            var userProfiles = projectionService.GetUserProfiles();
            await userProfiles.UpdateToLatestVersion();
            await userProfilesFactory.SaveAsync(userProfiles);
            results.Add(new { name = "UserProfiles", success = true, checkpoint = userProfiles.Checkpoint.Count });
            await SendProgress("UserProfiles", "completed", current, total);

            // 4. EventUpcastingDemonstration
            current++;
            await SendProgress("EventUpcastingDemonstration", "building", current, total, "Building EventUpcastingDemonstration projection...");
            var eventUpcastingDemonstration = projectionService.GetEventUpcastingDemonstration();
            await eventUpcastingDemonstration.UpdateToLatestVersion();
            await eventUpcastingDemonstrationFactory.SaveAsync(eventUpcastingDemonstration);
            results.Add(new { name = "EventUpcastingDemonstration", success = true, checkpoint = eventUpcastingDemonstration.Checkpoint.Count });
            await SendProgress("EventUpcastingDemonstration", "completed", current, total);

            // 5. ProjectKanbanBoard
            current++;
            await SendProgress("ProjectKanbanBoard", "building", current, total, "Building ProjectKanbanBoard projection...");
            var projectKanbanBoard = projectionService.GetProjectKanbanBoard();
            await projectKanbanBoard.UpdateToLatestVersion();
            await projectKanbanBoardFactory.SaveAsync(projectKanbanBoard);
            results.Add(new { name = "ProjectKanbanBoard", success = true, checkpoint = projectKanbanBoard.Checkpoint.Count });
            await SendProgress("ProjectKanbanBoard", "completed", current, total);

            // 6. EpicSummary (optional - Table Storage)
            current++;
            await SendProgress("EpicSummary", "building", current, total, "Building EpicSummary projection...");
            try
            {
                var epicSummary = projectionService.GetEpicSummary();
                await epicSummary.UpdateToLatestVersion();
                var epicSummaryFactory = serviceProvider.GetService<EpicSummaryFactory>();
                if (epicSummaryFactory != null)
                {
                    await epicSummaryFactory.SaveAsync(epicSummary);
                }
                results.Add(new { name = "EpicSummary", success = true, checkpoint = epicSummary.Checkpoint.Count });
                await SendProgress("EpicSummary", "completed", current, total);
            }
            catch (Exception ex)
            {
                results.Add(new { name = "EpicSummary", success = false, error = ex.Message });
                await SendProgress("EpicSummary", "error", current, total, ex.Message);
            }

            // 7. SprintDashboard (optional - CosmosDB)
            current++;
            await SendProgress("SprintDashboard", "building", current, total, "Building SprintDashboard projection...");
            try
            {
                var sprintDashboardFactory = serviceProvider.GetService<ISprintDashboardFactory>();
                var cosmosClientForBuild = serviceProvider.GetService<CosmosClient>();
                // Use keyed services - IObjectDocumentFactory and IEventStreamFactory are registered with keys
                var objectDocFactoryForBuild = serviceProvider.GetKeyedService<IObjectDocumentFactory>("cosmosdb");
                var eventStreamFactoryForBuild = serviceProvider.GetKeyedService<IEventStreamFactory>("cosmosdb");

                Console.WriteLine($"[SPRINT-BUILD] Factory: {sprintDashboardFactory != null}, CosmosClient: {cosmosClientForBuild != null}, DocFactory: {objectDocFactoryForBuild != null}, StreamFactory: {eventStreamFactoryForBuild != null}");

                if (sprintDashboardFactory != null && cosmosClientForBuild != null && objectDocFactoryForBuild != null && eventStreamFactoryForBuild != null)
                {
                    var sprintDashboard = await sprintDashboardFactory.GetAsync();
                    Console.WriteLine($"[SPRINT-BUILD] Got SprintDashboard, current sprints: {sprintDashboard.Sprints.Count}, checkpoint: {sprintDashboard.Checkpoint.Count}");

                    // For SprintDashboard, we need to discover all Sprint documents from CosmosDB
                    // since UpdateToLatestVersion only processes streams already in the checkpoint
                    var dbForBuild = cosmosClientForBuild.GetDatabase("eventstore");
                    var docsContainerForBuild = dbForBuild.GetContainer("documents");
                    var queryForBuild = new QueryDefinition("SELECT * FROM c WHERE c.objectName = 'sprint'");
                    using var feedIteratorForBuild = docsContainerForBuild.GetItemQueryIterator<dynamic>(queryForBuild);
                    var sprintDocsFound = 0;
                    var totalEventsProcessed = 0;
                    while (feedIteratorForBuild.HasMoreResults)
                    {
                        var responseForBuild = await feedIteratorForBuild.ReadNextAsync();
                        foreach (var doc in responseForBuild)
                        {
                            sprintDocsFound++;
                            string sprintObjectId = doc.objectId;
                            Console.WriteLine($"[SPRINT-BUILD] Processing sprint doc: {sprintObjectId}");
                            var sprintDoc = await objectDocFactoryForBuild.GetAsync("sprint", sprintObjectId, documentType: "cosmosdb");
                            Console.WriteLine($"[SPRINT-BUILD] Document loaded: ObjectName={sprintDoc.ObjectName}, ObjectId={sprintDoc.ObjectId}, StreamId={sprintDoc.Active?.StreamIdentifier}, StreamType={sprintDoc.Active?.StreamType}");
                            var sprintStream = eventStreamFactoryForBuild.Create(sprintDoc);
                            Console.WriteLine($"[SPRINT-BUILD] Stream created: Type={sprintStream.GetType().Name}");
                            // Process all events for this sprint
                            var sprintEvents = await sprintStream.ReadAsync();
                            var eventList = sprintEvents.ToList();
                            Console.WriteLine($"[SPRINT-BUILD] Sprint {sprintObjectId} has {eventList.Count} events");
                            foreach (var evt in eventList)
                            {
                                totalEventsProcessed++;
                                var versionToken = new ErikLieben.FA.ES.VersionToken(
                                    sprintDoc.ObjectName,
                                    sprintDoc.ObjectId,
                                    sprintDoc.Active.StreamIdentifier,
                                    evt.EventVersion);
                                Console.WriteLine($"[SPRINT-BUILD] Processing event: {evt.EventType}, ObjectId: {versionToken.ObjectId}");
                                await sprintDashboard.Fold(evt, versionToken);
                                sprintDashboard.Checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;
                            }
                        }
                    }

                    Console.WriteLine($"[SPRINT-BUILD] Found {sprintDocsFound} sprint docs, processed {totalEventsProcessed} events, now have {sprintDashboard.Sprints.Count} sprints");

                    if (sprintDashboard.Checkpoint.Count > 0)
                    {
                        sprintDashboard.CheckpointFingerprint = string.Join(",", sprintDashboard.Checkpoint.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}").Take(10));
                    }

                    await sprintDashboardFactory.SaveAsync(sprintDashboard);
                    Console.WriteLine($"[SPRINT-BUILD] Saved SprintDashboard with {sprintDashboard.Sprints.Count} sprints");
                    results.Add(new { name = "SprintDashboard", success = true, checkpoint = sprintDashboard.Checkpoint.Count, sprintCount = sprintDashboard.Sprints.Count });
                    await SendProgress("SprintDashboard", "completed", current, total);
                }
                else
                {
                    Console.WriteLine($"[SPRINT-BUILD] SKIPPED - Missing dependencies");
                    results.Add(new { name = "SprintDashboard", success = false, error = "CosmosDB not configured or dependencies missing" });
                    await SendProgress("SprintDashboard", "skipped", current, total, "CosmosDB not configured");
                }
            }
            catch (Exception ex)
            {
                results.Add(new { name = "SprintDashboard", success = false, error = ex.Message });
                await SendProgress("SprintDashboard", "error", current, total, ex.Message);
            }

            await SendProgress("All", "completed", total, total, "All projections built successfully");

            return Results.Ok(new
            {
                success = true,
                message = "All projections built successfully",
                projections = results
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to build projections",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500);
        }
    }

    private static IResult GetProjectionJson(
        string name,
        [FromServices] IProjectionService projectionService)
    {
        switch (name.ToLowerInvariant())
        {
            case "activeworkitems":
                var activeWorkItems = projectionService.GetActiveWorkItems();
                return Results.Text(activeWorkItems.ToJson(), "application/json");

            case "projectdashboard":
                var projectDashboard = projectionService.GetProjectDashboard();
                return Results.Text(projectDashboard.ToJson(), "application/json");

            case "userprofiles":
                var userProfiles = projectionService.GetUserProfiles();
                return Results.Text(userProfiles.ToJson(), "application/json");

            case "eventupcastingdemonstration":
                var eventUpcastingDemonstration = projectionService.GetEventUpcastingDemonstration();
                return Results.Text(eventUpcastingDemonstration.ToJson(), "application/json");

            case "projectkanbanboard":
                var projectKanbanBoard = projectionService.GetProjectKanbanBoard();
                return Results.Text(projectKanbanBoard.ToJson(), "application/json");

            case "sprintdashboard":
                var sprintDashboard = projectionService.GetSprintDashboard();
                if (sprintDashboard == null)
                {
                    return Results.NotFound(new { message = $"Projection '{name}' not available - CosmosDB may not be configured" });
                }
                return Results.Text(sprintDashboard.ToJson(), "application/json");

            default:
                return Results.NotFound(new { message = $"Projection '{name}' not found" });
        }
    }

    private static async Task<IResult> GetProjectionMetadata(
        string name,
        [FromServices] Microsoft.Extensions.Azure.IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient> clientFactory)
    {
        try
        {
            var blobServiceClient = clientFactory.CreateClient("Store");
            var containerClient = blobServiceClient.GetBlobContainerClient("projections");
            var blobClient = containerClient.GetBlobClient($"{name.ToLowerInvariant()}.json");

            if (!await blobClient.ExistsAsync())
            {
                return Results.NotFound(new { message = $"Projection blob '{name}' not found" });
            }

            var properties = await blobClient.GetPropertiesAsync();

            return Results.Ok(new
            {
                name = name.ToLowerInvariant(),
                lastModified = properties.Value.LastModified.UtcDateTime,
                contentLength = properties.Value.ContentLength,
                contentType = properties.Value.ContentType
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to get projection metadata: {ex.Message}");
        }
    }

    private static async Task<IResult> ClearAllStorage(
        [FromServices] Microsoft.Extensions.Azure.IAzureClientFactory<Azure.Storage.Blobs.BlobServiceClient> clientFactory)
    {
        var blobServiceClient = clientFactory.CreateClient("Store");
            var deletedContainers = new List<string>();

            await foreach (var container in blobServiceClient.GetBlobContainersAsync())
            {
                await blobServiceClient.DeleteBlobContainerAsync(container.Name);
                deletedContainers.Add(container.Name);
            }

        return Results.Ok(new
        {
            success = true,
            message = $"Deleted {deletedContainers.Count} container(s)",
            deletedContainers = deletedContainers
        });
    }

    private static async Task<IResult> GetUserProfilesProjectionStatus(
        [FromServices] IProjectionService projectionService,
        [FromServices] TaskFlow.Domain.Projections.UserProfilesFactory userProfilesFactory)
    {
        try
        {
            var userProfiles = projectionService.GetUserProfiles();
            var lastModified = await userProfilesFactory.GetLastModifiedAsync();
            var allProfiles = userProfiles.GetAllProfiles();

            return Results.Ok(new
            {
                name = "UserProfiles",
                status = lastModified.HasValue ? "idle" : "not-persisted",
                lastUpdate = lastModified,
                checkpoint = userProfiles.Checkpoint.Count,
                checkpointFingerprint = userProfiles.CheckpointFingerprint,
                profileCount = userProfiles.TotalUsers,
                pageCount = userProfiles.TotalPages,
                usersPerPage = 10,
                isPersisted = lastModified.HasValue,
                pages = Enumerable.Range(1, userProfiles.TotalPages).Select(pageNum => new
                {
                    pageNumber = pageNum,
                    userCount = userProfiles.GetProfilesForPage(pageNum).Count
                }).ToArray(),
                profiles = allProfiles.Select(p => new
                {
                    userId = p.UserId,
                    name = p.Name,
                    email = p.Email,
                    createdAt = p.CreatedAt
                }).ToArray()
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get UserProfiles projection status",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Seed demo epics stored in Azure Table Storage
    /// </summary>
    private static async Task<IResult> SeedDemoEpics(
        [FromServices] IEpicFactory epicFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEnumerable<TaskFlow.Api.Projections.IProjectionHandler> projectionHandlers,
        [FromServices] IHubContext<TaskFlowHub> hubContext)
    {
        // Disable projection publishing during seeding - projections should be built separately
        using var projectionDisableScope = PublishProjectionUpdateAction.DisableScope();

        try
        {
            var now = DateTime.UtcNow;
            var random = new Random(42); // Fixed seed for reproducible results

            // Get or create a demo user for Epic operations
            var adminEmail = "admin@taskflow.demo";
            UserProfileId ownerId;
            try
            {
                var existingUser = await objectDocumentFactory.GetFirstByObjectDocumentTag("userprofile", adminEmail);
                if (existingUser != null)
                {
                    ownerId = UserProfileId.From(existingUser.ObjectId);
                }
                else
                {
                    var (result, userProfile) = await userProfileFactory.CreateProfileAsync(
                        "Epic Admin",
                        adminEmail,
                        "System Administrator");
                    ownerId = userProfile?.Metadata?.Id ?? UserProfileId.New();
                }
            }
            catch
            {
                // If user lookup fails, create a new user
                var (result, userProfile) = await userProfileFactory.CreateProfileAsync(
                    "Epic Admin",
                    $"epic-admin-{Guid.NewGuid():N}@taskflow.demo",
                    "System Administrator");
                ownerId = userProfile?.Metadata?.Id ?? UserProfileId.New();
            }

            // Demo Epic templates with realistic data
            var epicTemplates = new[]
            {
                ("Q1 2024 Product Launch", "Launch major product update including new features and improvements", EpicPriority.Critical, 90),
                ("Security Enhancement Initiative", "Implement comprehensive security improvements across all systems", EpicPriority.High, 120),
                ("Mobile Experience Overhaul", "Redesign and optimize mobile applications for better user experience", EpicPriority.High, 180),
                ("Data Platform Modernization", "Upgrade data infrastructure to support real-time analytics", EpicPriority.Medium, 150),
                ("Customer Onboarding Automation", "Automate customer onboarding process to reduce friction", EpicPriority.Medium, 60),
                ("API Gateway Consolidation", "Consolidate multiple API gateways into unified architecture", EpicPriority.Low, 200),
                ("DevOps Pipeline Enhancement", "Improve CI/CD pipelines for faster deployments", EpicPriority.Medium, 90),
                ("Compliance and Audit Readiness", "Prepare systems for SOC2 and GDPR compliance audits", EpicPriority.High, 120),
            };

            var createdEpics = new List<Epic>();
            var epicIds = new List<EpicId>();
            var totalEpics = epicTemplates.Length;

            await hubContext.BroadcastSeedProgress("table", 0, totalEpics, "Creating epics...");

            // Create Epics with varying creation dates
            for (int i = 0; i < epicTemplates.Length; i++)
            {
                var (name, description, priority, targetDays) = epicTemplates[i];
                var createdDaysAgo = random.Next(30, 180);
                var createdAt = now.AddDays(-createdDaysAgo);
                var targetDate = createdAt.AddDays(targetDays);

                var epicId = EpicId.New();
                epicIds.Add(epicId);

                var (result, epic) = await epicFactory.CreateEpicWithIdAsync(
                    epicId,
                    name,
                    description,
                    ownerId,
                    targetDate,
                    createdByUser: null,
                    createdAt: createdAt);

                if (result.IsSuccess && epic != null)
                {
                    createdEpics.Add(epic);

                    // Set priority if not Medium (default)
                    if (priority != EpicPriority.Medium)
                    {
                        await epic.ChangePriority(priority, ownerId, occurredAt: createdAt.AddMinutes(1));
                    }
                }

                await hubContext.BroadcastSeedProgress("table", i + 1, totalEpics, $"Creating epics ({i + 1}/{totalEpics})...");
            }

            // Complete some epics
            var completedEpicIndices = new[] { 0, 4 }; // Q1 Launch and Onboarding Automation
            foreach (var idx in completedEpicIndices)
            {
                if (idx < createdEpics.Count)
                {
                    var completionDate = now.AddDays(-random.Next(5, 30));
                    await createdEpics[idx].CompleteEpic(
                        "Epic completed successfully with all objectives met",
                        ownerId,
                        occurredAt: completionDate);
                }
            }

            // Try to link some existing projects to epics (if projects exist)
            try
            {
                // Get some demo project IDs (from DemoProjectIds if they exist)
                var demoProjectGuids = new[]
                {
                    "10000000-0000-0000-0000-000000000001",
                    "10000000-0000-0000-0000-000000000002",
                    "20000000-0000-0000-0000-000000000001",
                    "20000000-0000-0000-0000-000000000002",
                };

                // Add projects to epics (2-3 projects per epic)
                for (int epicIdx = 0; epicIdx < Math.Min(4, createdEpics.Count); epicIdx++)
                {
                    var epic = createdEpics[epicIdx];
                    if (epic.IsCompleted) continue;

                    for (int projIdx = 0; projIdx < 2 && epicIdx * 2 + projIdx < demoProjectGuids.Length; projIdx++)
                    {
                        var projectIdStr = demoProjectGuids[epicIdx * 2 + projIdx];
                        try
                        {
                            var projectId = ProjectId.From(projectIdStr);
                            // Verify project exists by trying to load it
                            var project = await projectFactory.GetAsync(projectId);
                            if (project != null)
                            {
                                await epic.AddProject(projectId, ownerId);
                            }
                        }
                        catch
                        {
                            // Project doesn't exist, skip it
                        }
                    }
                }
            }
            catch
            {
                // No projects to link, that's OK
            }

            // Trigger projection updates for all created epics
            Console.WriteLine($"[SEED-EPICS] Triggering projection updates for {epicIds.Count} epics...");

            var epicProjectionEvents = new List<TaskFlow.Domain.Messaging.ProjectionUpdateRequested>();

            foreach (var epicId in epicIds)
            {
                try
                {
                    var epic = await epicFactory.GetAsync(epicId);
                    if (epic?.Metadata != null)
                    {
                        var versionToken = epic.Metadata.ToVersionToken("epic").ToLatestVersion();
                        epicProjectionEvents.Add(new TaskFlow.Domain.Messaging.ProjectionUpdateRequested
                        {
                            VersionToken = versionToken,
                            ObjectName = "epic",
                            StreamIdentifier = epic.Metadata.StreamId!,
                            EventCount = 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED-EPICS] Warning: Failed to get version token for epic {epicId.Value}: {ex.Message}");
                }
            }

            // Call all projection handlers with the epic events
            foreach (var handler in projectionHandlers)
            {
                try
                {
                    Console.WriteLine($"[SEED-EPICS] Updating projection: {handler.ProjectionName}");
                    await handler.HandleBatchAsync(epicProjectionEvents);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED-EPICS] Warning: Failed to update projection {handler.ProjectionName}: {ex.Message}");
                }
            }

            Console.WriteLine($"[SEED-EPICS] Projection updates complete.");

            return Results.Ok(new
            {
                success = true,
                message = $"Created {createdEpics.Count} demo epics stored in Azure Table Storage",
                epicIds = epicIds.Select(e => e.Value.ToString()).ToArray(),
                note = "Epics are stored in Azure Table Storage (tables: epicevents, epicdocuments)"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to seed demo epics",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Seed demo sprints stored in Azure CosmosDB
    /// Demonstrates the CosmosDB storage provider for event sourcing
    /// </summary>
    private static async Task<IResult> SeedDemoSprints(
        [FromServices] ISprintFactory sprintFactory,
        [FromServices] IUserProfileFactory userProfileFactory,
        [FromServices] IProjectFactory projectFactory,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEnumerable<TaskFlow.Api.Projections.IProjectionHandler> projectionHandlers,
        [FromServices] IHubContext<TaskFlowHub> hubContext,
        [FromServices] CosmosClient? cosmosClient = null)
    {
        // Disable projection publishing during seeding - projections should be built separately
        using var projectionDisableScope = PublishProjectionUpdateAction.DisableScope();

        try
        {
            Console.WriteLine("[SEED-SPRINTS] Starting sprint seeding...");

            // Test CosmosDB connection directly
            if (cosmosClient == null)
            {
                Console.WriteLine("[SEED-SPRINTS] ERROR: CosmosClient is not registered!");
                return Results.Problem(
                    title: "CosmosDB Not Configured",
                    detail: "CosmosClient is not registered. Check the connection string configuration.",
                    statusCode: 503
                );
            }

            Console.WriteLine("[SEED-SPRINTS] Testing CosmosDB connection...");
            try
            {
                using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var testTask = cosmosClient.ReadAccountAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15), testCts.Token);

                var completedTask = await Task.WhenAny(testTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("[SEED-SPRINTS] CosmosDB connection test timed out after 15 seconds");
                    return Results.Problem(
                        title: "CosmosDB Connection Timeout",
                        detail: "Could not connect to CosmosDB within 15 seconds. The emulator may not be ready.",
                        statusCode: 503
                    );
                }

                var accountInfo = await testTask;
                Console.WriteLine($"[SEED-SPRINTS] Connected to CosmosDB: {accountInfo.Id}");
                testCts.Cancel(); // Cancel the timeout task

                // Test database and container access
                Console.WriteLine("[SEED-SPRINTS] Testing database access...");
                Console.WriteLine("[SEED-SPRINTS] Creating database if not exists...");
                var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync("eventstore");
                var database = databaseResponse.Database;
                Console.WriteLine($"[SEED-SPRINTS] Database ready: {database.Id} (Status: {databaseResponse.StatusCode})");

                Console.WriteLine("[SEED-SPRINTS] Creating containers if not exists...");

                // Create all required containers
                var containers = new[]
                {
                    new ContainerProperties("events", "/streamId"),
                    new ContainerProperties("documents", "/objectName"),
                    new ContainerProperties("tags", "/tagKey"),
                    new ContainerProperties("projections", "/projectionName")
                };

                foreach (var containerProps in containers)
                {
                    Console.WriteLine($"[SEED-SPRINTS] Creating container '{containerProps.Id}'...");
                    var containerResponse = await database.CreateContainerIfNotExistsAsync(containerProps);
                    Console.WriteLine($"[SEED-SPRINTS] Container ready: {containerResponse.Container.Id} (Status: {containerResponse.StatusCode})");
                }
            }
            catch (CosmosException cosmosEx)
            {
                Console.WriteLine($"[SEED-SPRINTS] CosmosDB error: {cosmosEx.StatusCode} - {cosmosEx.Message}");
                return Results.Problem(
                    title: "CosmosDB Error",
                    detail: $"CosmosDB returned error {cosmosEx.StatusCode}: {cosmosEx.Message}",
                    statusCode: 503
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED-SPRINTS] CosmosDB connection test failed: {ex.GetType().Name} - {ex.Message}");
                return Results.Problem(
                    title: "CosmosDB Connection Failed",
                    detail: $"Failed to connect to CosmosDB: {ex.Message}",
                    statusCode: 503
                );
            }

            var now = DateTime.UtcNow;
            var random = new Random(42); // Fixed seed for reproducible results

            Console.WriteLine("[SEED-SPRINTS] Looking up admin user...");
            // Get or create a demo user for Sprint operations
            var adminEmail = "admin@taskflow.demo";
            UserProfileId ownerId;
            try
            {
                Console.WriteLine("[SEED-SPRINTS] Querying for existing admin user...");
                var existingUser = await objectDocumentFactory.GetFirstByObjectDocumentTag("userprofile", adminEmail);
                Console.WriteLine($"[SEED-SPRINTS] User lookup complete: {(existingUser != null ? "found" : "not found")}");
                if (existingUser != null)
                {
                    ownerId = UserProfileId.From(existingUser.ObjectId);
                }
                else
                {
                    Console.WriteLine("[SEED-SPRINTS] Creating new admin user...");
                    var (result, userProfile) = await userProfileFactory.CreateProfileAsync(
                        "Sprint Admin",
                        adminEmail,
                        "Scrum Master");
                    ownerId = userProfile?.Metadata?.Id ?? UserProfileId.New();
                    Console.WriteLine("[SEED-SPRINTS] Admin user created");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED-SPRINTS] User lookup failed: {ex.Message}, creating new user...");
                // If user lookup fails, create a new user
                var (result, userProfile) = await userProfileFactory.CreateProfileAsync(
                    "Sprint Admin",
                    $"sprint-admin-{Guid.NewGuid():N}@taskflow.demo",
                    "Scrum Master");
                ownerId = userProfile?.Metadata?.Id ?? UserProfileId.New();
                Console.WriteLine("[SEED-SPRINTS] Fallback admin user created");
            }

            // Demo Sprint templates with realistic data
            var sprintTemplates = new[]
            {
                ("Sprint 2024-Q1-W1", "Kickoff sprint for Q1 initiatives", 14, "Establish foundation for Q1 product features"),
                ("Sprint 2024-Q1-W3", "Feature development sprint", 14, "Complete core authentication flow"),
                ("Sprint 2024-Q1-W5", "Integration sprint", 14, "API integration with external services"),
                ("Sprint 2024-Q2-W1", "Q2 Planning sprint", 14, "Plan and estimate Q2 deliverables"),
                ("Sprint 2024-Q2-W3", "Performance optimization", 14, "Improve application performance by 50%"),
                ("Sprint 2024-Q2-W5", "Mobile-first sprint", 14, "Responsive design implementation"),
                ("Sprint 2024-Q3-W1", "Security hardening", 14, "Implement security best practices"),
                ("Sprint 2024-Q3-W3", "User experience sprint", 14, "UX improvements based on feedback"),
                ("Sprint 2024-Q4-W1", "Current sprint", 14, "Ongoing development work"),
                ("Sprint 2024-Q4-W3", "Upcoming sprint", 14, "Next iteration planning"),
            };

            // Get a project to associate sprints with
            Console.WriteLine("[SEED-SPRINTS] Looking up project...");
            ProjectId projectId;
            try
            {
                // Try to get an existing project
                Console.WriteLine("[SEED-SPRINTS] Querying for existing project...");
                var existingProject = await objectDocumentFactory.GetFirstByObjectDocumentTag("project", "demo");
                Console.WriteLine($"[SEED-SPRINTS] Project lookup complete: {(existingProject != null ? "found" : "not found")}");
                if (existingProject != null)
                {
                    projectId = ProjectId.From(existingProject.ObjectId);
                }
                else
                {
                    Console.WriteLine("[SEED-SPRINTS] Creating new project...");
                    // Create a project for the sprints
                    var project = await projectFactory.CreateAsync(ProjectId.New());
                    await project.InitiateProject(
                        "Sprint Demo Project",
                        "Project demonstrating CosmosDB-backed sprints",
                        ownerId,
                        occurredAt: now.AddDays(-200));
                    projectId = project.Metadata?.Id ?? ProjectId.New();
                    Console.WriteLine("[SEED-SPRINTS] Project created");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED-SPRINTS] Project lookup failed: {ex.Message}, using random ID");
                projectId = ProjectId.From(Guid.NewGuid().ToString());
            }

            var createdSprints = new List<Sprint>();
            var sprintIds = new List<SprintId>();
            var totalSprints = sprintTemplates.Length;

            await hubContext.BroadcastSeedProgress("cosmos", 0, totalSprints, "Creating sprints...");

            // Create Sprints with varying dates
            for (int i = 0; i < sprintTemplates.Length; i++)
            {
                var (name, description, durationDays, goal) = sprintTemplates[i];

                // Calculate sprint dates - earlier sprints are further in the past
                var sprintStartDaysAgo = (sprintTemplates.Length - i) * 14 + random.Next(-3, 4);
                var startDate = now.AddDays(-sprintStartDaysAgo);
                var endDate = startDate.AddDays(durationDays);

                var sprintId = SprintId.New();
                sprintIds.Add(sprintId);

                Console.WriteLine($"[SEED-SPRINTS] Creating sprint {i + 1}/{sprintTemplates.Length}: {name}");

                // Add timeout to detect CosmosDB connection issues
                try
                {
                    var createTask = sprintFactory.CreateSprintWithIdAsync(
                        sprintId,
                        name,
                        projectId,
                        startDate,
                        endDate,
                        goal,
                        ownerId,
                        createdByUser: null,
                        createdAt: startDate.AddDays(-1)); // Created day before start

                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                    var completedTask = await Task.WhenAny(createTask, timeoutTask);
                    if (completedTask != createTask)
                    {
                        Console.WriteLine($"[SEED-SPRINTS] TIMEOUT creating sprint {name} - CosmosDB may not be responding. Is the CosmosDB emulator running?");
                        return Results.Problem(
                            title: "CosmosDB Timeout",
                            detail: "Sprint creation timed out after 30 seconds. Please ensure the CosmosDB emulator is running and accessible.",
                            statusCode: 503
                        );
                    }

                    var (result, sprint) = await createTask;

                    if (result.IsSuccess && sprint != null)
                    {
                        createdSprints.Add(sprint);
                        Console.WriteLine($"[SEED-SPRINTS] Created sprint: {name}");

                        // Start sprints that should be active or completed
                        if (startDate <= now)
                        {
                            await sprint.StartSprint(ownerId, occurredAt: startDate);

                            // Complete sprints that have passed their end date
                            if (endDate < now)
                            {
                                var completionDate = endDate.AddDays(-1);
                                await sprint.CompleteSprint(
                                    ownerId,
                                    summary: $"Sprint completed with {random.Next(70, 100)}% of planned items delivered",
                                    occurredAt: completionDate);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[SEED-SPRINTS] Failed to create sprint: {name} - {string.Join(", ", result.Errors.ToArray().Select(e => e.Message))}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED-SPRINTS] Exception creating sprint {name}: {ex.GetType().Name} - {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"[SEED-SPRINTS] Inner exception: {ex.InnerException.Message}");
                    // Continue to next sprint
                }

                await hubContext.BroadcastSeedProgress("cosmos", i + 1, totalSprints, $"Creating sprints ({i + 1}/{totalSprints})...");
            }
            Console.WriteLine($"[SEED-SPRINTS] Created {createdSprints.Count} sprints");

            // Add some work items to active/recent sprints
            Console.WriteLine("[SEED-SPRINTS] Looking up work items to add to sprints...");
            try
            {
                // Get some existing work items
                Console.WriteLine("[SEED-SPRINTS] Querying for work items...");
                var workItemDocs = await objectDocumentFactory.GetByObjectDocumentTag("workitem", "demo");
                Console.WriteLine($"[SEED-SPRINTS] Found {workItemDocs.Count()} work items");
                var workItemIds = workItemDocs.Take(10).Select(d => WorkItemId.From(d.ObjectId)).ToList();

                // Add work items to the last few sprints
                for (int sprintIdx = Math.Max(0, createdSprints.Count - 3); sprintIdx < createdSprints.Count; sprintIdx++)
                {
                    var sprint = createdSprints[sprintIdx];
                    var itemsToAdd = random.Next(2, 5);

                    for (int j = 0; j < itemsToAdd && j < workItemIds.Count; j++)
                    {
                        var workItemId = workItemIds[j];
                        try
                        {
                            await sprint.AddWorkItem(workItemId, ownerId);
                        }
                        catch
                        {
                            // Work item might already be in another sprint, skip
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED-SPRINTS] Work item lookup failed: {ex.Message} - continuing without work items");
                // No work items to add, that's OK
            }

            Console.WriteLine("[SEED-SPRINTS] Checking for sprint to cancel...");
            // Cancel one of the past sprints for demo purposes
            if (createdSprints.Count > 3)
            {
                var sprintToCancel = createdSprints[2]; // Third sprint
                if (sprintToCancel.Status == SprintStatus.Completed)
                {
                    // Already completed, skip
                }
                else if (sprintToCancel.Status == SprintStatus.Active)
                {
                    // Complete it instead of cancel
                }
                else
                {
                    try
                    {
                        await sprintToCancel.CancelSprint(
                            ownerId,
                            reason: "Sprint cancelled due to priority shift",
                            occurredAt: now.AddDays(-30));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SEED-SPRINTS] Could not cancel sprint: {ex.Message}");
                        // Sprint might be in wrong state, skip
                    }
                }
            }

            // Trigger projection updates for all created sprints
            Console.WriteLine($"[SEED-SPRINTS] Triggering projection updates for {sprintIds.Count} sprints...");

            var sprintProjectionEvents = new List<TaskFlow.Domain.Messaging.ProjectionUpdateRequested>();

            foreach (var sprintId in sprintIds)
            {
                try
                {
                    var sprint = await sprintFactory.GetAsync(sprintId);
                    if (sprint?.Metadata != null)
                    {
                        var versionToken = sprint.Metadata.ToVersionToken("sprint").ToLatestVersion();
                        sprintProjectionEvents.Add(new TaskFlow.Domain.Messaging.ProjectionUpdateRequested
                        {
                            VersionToken = versionToken,
                            ObjectName = "sprint",
                            StreamIdentifier = sprint.Metadata.StreamId!,
                            EventCount = 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED-SPRINTS] Warning: Failed to get version token for sprint {sprintId.Value}: {ex.Message}");
                }
            }

            // Call all projection handlers with the sprint events
            foreach (var handler in projectionHandlers)
            {
                try
                {
                    Console.WriteLine($"[SEED-SPRINTS] Updating projection: {handler.ProjectionName}");
                    await handler.HandleBatchAsync(sprintProjectionEvents);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SEED-SPRINTS] Warning: Failed to update projection {handler.ProjectionName}: {ex.Message}");
                }
            }

            Console.WriteLine("[SEED-SPRINTS] Projection updates complete.");

            Console.WriteLine("[SEED-SPRINTS] Seeding complete, returning result...");
            return Results.Ok(new
            {
                success = true,
                message = $"Created {createdSprints.Count} demo sprints stored in Azure CosmosDB",
                sprintIds = sprintIds.Select(s => s.Value).ToArray(),
                note = "Sprints are stored in Azure CosmosDB (database: eventstore)"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to seed demo sprints",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Get raw event stream for an epic (stored in Table Storage)
    /// </summary>
    private static async Task<IResult> GetEpicEvents(
        string id,
        [FromServices] IObjectDocumentFactory objectDocumentFactory,
        [FromServices] IEventStreamFactory eventStreamFactory,
        [FromServices] IEpicFactory epicFactory)
    {
        try
        {
            var epicId = EpicId.From(id);

            // Use the factory to load the epic and get its event stream
            var epic = await epicFactory.GetAsync(epicId);

            // Get the document to access the event stream
            var document = await objectDocumentFactory.GetAsync("epic", id);

            // Create the event stream from the document
            var stream = eventStreamFactory.Create(document);

            // Read all events (raw, without upcasting)
            var rawEvents = await stream.ReadAsync();

            // Transform events into a simplified format for the UI
            var events = rawEvents.Select((e, index) =>
            {
                object? data = null;
                if (!string.IsNullOrEmpty(e.Payload))
                {
                    try
                    {
                        data = System.Text.Json.JsonSerializer.Deserialize<object>(e.Payload);
                    }
                    catch
                    {
                        data = e.Payload;
                    }
                }

                return new
                {
                    eventType = e.EventType,
                    version = e.EventVersion,
                    schemaVersion = e.SchemaVersion,
                    data
                };
            }).ToList();

            return Results.Ok(new
            {
                epicId = id,
                epicName = epic.Name,
                storageType = "Azure Table Storage",
                eventCount = events.Count,
                events
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get epic events",
                detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Lists available benchmark result files from the benchmarks artifacts directory
    /// </summary>
    private static IResult ListBenchmarkFiles(IWebHostEnvironment env)
    {
        // Navigate from demo/src/TaskFlow.Api to benchmarks/ErikLieben.FA.ES.Benchmarks/BenchmarkDotNet.Artifacts/results
        var contentRoot = env.ContentRootPath;
        var benchmarkResultsPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "benchmarks", "ErikLieben.FA.ES.Benchmarks", "BenchmarkDotNet.Artifacts", "results"));

        if (!Directory.Exists(benchmarkResultsPath))
        {
            return Results.Ok(Array.Empty<object>());
        }

        var jsonFiles = Directory.GetFiles(benchmarkResultsPath, "*-report-full.json")
            .Select(f =>
            {
                var fileName = Path.GetFileName(f);
                var fileInfo = new FileInfo(f);

                // Extract framework from filename (e.g., "...-net10-report-full.json")
                var framework = "unknown";
                if (fileName.Contains("-net10-")) framework = "net10.0";
                else if (fileName.Contains("-net9-")) framework = "net9.0";
                else if (fileName.Contains("-net8-")) framework = "net8.0";

                // Try to parse JSON to get benchmark count
                var benchmarkCount = 0;
                try
                {
                    var json = System.IO.File.ReadAllText(f);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Benchmarks", out var benchmarks) && benchmarks.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        benchmarkCount = benchmarks.GetArrayLength();
                    }
                }
                catch { /* Ignore parse errors */ }

                return new
                {
                    name = fileName,
                    path = f,
                    date = fileInfo.LastWriteTimeUtc.ToString("o"),
                    framework,
                    benchmarkCount
                };
            })
            .OrderByDescending(f => f.date)
            .ToList();

        return Results.Ok(jsonFiles);
    }

    /// <summary>
    /// Returns the contents of a specific benchmark result file
    /// </summary>
    private static IResult GetBenchmarkFile(string filename, IWebHostEnvironment env)
    {
        // Validate filename to prevent path traversal
        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
        {
            return Results.BadRequest("Invalid filename");
        }

        var contentRoot = env.ContentRootPath;
        var benchmarkResultsPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "benchmarks", "ErikLieben.FA.ES.Benchmarks", "BenchmarkDotNet.Artifacts", "results"));
        var filePath = Path.Combine(benchmarkResultsPath, filename);

        if (!System.IO.File.Exists(filePath))
        {
            return Results.NotFound($"Benchmark file '{filename}' not found");
        }

        // Ensure the resolved path is still within the expected directory
        if (!Path.GetFullPath(filePath).StartsWith(benchmarkResultsPath))
        {
            return Results.BadRequest("Invalid filename");
        }

        try
        {
            var json = System.IO.File.ReadAllText(filePath);
            return Results.Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to read benchmark file",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}
