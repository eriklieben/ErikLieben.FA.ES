# Implementation Plan
## TaskFlow Project Management System

**Version:** 1.0
**Date:** 2025-11-10
**Estimated Duration:** 6-8 weeks
**Team Size:** 2 developers

---

## Table of Contents
1. [Phase Overview](#phase-overview)
2. [Detailed Task Breakdown](#detailed-task-breakdown)
3. [Technical Setup](#technical-setup)
4. [Project Structure](#project-structure)
5. [Development Workflow](#development-workflow)
6. [Testing Strategy](#testing-strategy)
7. [Deployment Plan](#deployment-plan)

---

## Phase Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ Phase 1: Foundation (Week 1-2)                                  │
│ ├─ Project structure & solution setup                           │
│ ├─ Domain model (aggregates, events)                           │
│ ├─ Aspire orchestration configuration                          │
│ └─ Basic API with Azurite integration                          │
├─────────────────────────────────────────────────────────────────┤
│ Phase 2: Core Features (Week 2-3)                              │
│ ├─ All command endpoints                                       │
│ ├─ Basic query endpoints                                       │
│ ├─ Two projections (ActiveTasks, ProjectDashboard)            │
│ └─ Unit tests for domain                                       │
├─────────────────────────────────────────────────────────────────┤
│ Phase 3: Frontend Foundation (Week 3-4)                        │
│ ├─ Angular app structure with Aspire                          │
│ ├─ Project and task components                                │
│ ├─ SignalR integration                                        │
│ └─ Basic Material UI                                          │
├─────────────────────────────────────────────────────────────────┤
│ Phase 4: Advanced Features (Week 4-5)                         │
│ ├─ Time-travel UI & event explorer                           │
│ ├─ Event upcasting example                                   │
│ ├─ Snapshot management                                       │
│ ├─ Admin panel                                               │
│ └─ Additional projections                                    │
├─────────────────────────────────────────────────────────────────┤
│ Phase 5: Polish & Documentation (Week 5-6)                    │
│ ├─ Comprehensive README                                      │
│ ├─ Code documentation                                        │
│ ├─ Demo seed data                                            │
│ ├─ Integration tests                                         │
│ └─ Performance optimization                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Detailed Task Breakdown

### Phase 1: Foundation (Week 1-2)

#### 1.1 Project Structure Setup
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** None

**Tasks:**
- [ ] Create solution: `TaskFlow.sln`
- [ ] Create projects:
  - `TaskFlow.Domain` (Class Library, .NET 9.0)
  - `TaskFlow.Api` (ASP.NET Core Web API, .NET 9.0)
  - `TaskFlow.AppHost` (Aspire AppHost, .NET 9.0)
  - `TaskFlow.ServiceDefaults` (Class Library, .NET 9.0)
  - `TaskFlow.Domain.Tests` (xUnit Test Project)
  - `TaskFlow.Api.Tests` (xUnit Test Project)
- [ ] Add NuGet package references:
  - Domain: `ErikLieben.FA.ES` (latest)
  - API: `Microsoft.AspNetCore.SignalR`, `Microsoft.AspNetCore.OpenApi`
  - AppHost: `Aspire.Hosting`, `Aspire.Hosting.Azure.Storage`
  - ServiceDefaults: `Aspire.Hosting.Common`
- [ ] Configure `.editorconfig` for code style
- [ ] Create `.gitignore`
- [ ] Create initial `README.md`

**Deliverable:** Solution builds successfully, all projects referenced correctly

---

#### 1.2 Domain Model - Project Aggregate
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 1.1

**Tasks:**
- [ ] Create `Aggregates/Project.cs`:
  ```csharp
  [Aggregate]
  public partial class Project(IEventStream stream) : Aggregate(stream)
  {
      public string? Name { get; private set; }
      public string? Description { get; private set; }
      public string? OwnerId { get; private set; }
      public bool IsArchived { get; private set; }
      public Dictionary<string, string> TeamMembers { get; } = new();

      // When methods for event handlers
  }
  ```
- [ ] Create `Events/ProjectEvents.cs`:
  - `ProjectInitiated`
  - `ProjectRebranded`
  - `ProjectScopeRefined`
  - `ProjectCompleted`
  - `ProjectReactivated`
  - `MemberJoinedProject`
  - `MemberLeftProject`
- [ ] Add `[EventName]` attributes to all events
- [ ] Implement `When()` methods in Project aggregate
- [ ] Create command methods:
  - `CreateProject(name, description, ownerId)`
  - `RenameProject(newName)`
  - `UpdateDescription(newDescription)`
  - `ArchiveProject(reason)`
  - `RestoreProject()`
  - `AddTeamMember(userId, role)`
  - `RemoveTeamMember(userId)`
- [ ] Add domain validation in command methods

**Deliverable:** Project aggregate with all events and commands

---

#### 1.3 Domain Model - Task Aggregate
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** 1.1

**Tasks:**
- [ ] Create `Aggregates/Task.cs`:
  ```csharp
  [Aggregate]
  public partial class Task(IEventStream stream) : Aggregate(stream)
  {
      public string? ProjectId { get; private set; }
      public string? Title { get; private set; }
      public string? Description { get; private set; }
      public TaskPriority Priority { get; private set; }
      public TaskStatus Status { get; private set; }
      public string? AssignedTo { get; private set; }
      public DateTime? DueDate { get; private set; }
      public List<string> Tags { get; } = new();
      public List<TaskComment> Comments { get; } = new();

      // When methods for event handlers
  }
  ```
- [ ] Create enums: `TaskPriority`, `TaskStatus`
- [ ] Create `Events/TaskEvents.cs`:
  - `TaskPlanned`
  - `ResponsibilityAssigned`, `ResponsibilityRelinquished`
  - `WorkCommenced`, `WorkCompleted`, `TaskRevived`
  - `TaskReprioritized`
  - `EffortReestimated`
  - `RequirementsRefined`
  - `FeedbackProvided`
  - `TaskRelocated`
  - `TaskRetagged`
  - `DeadlineEstablished`, `DeadlineRemoved`
- [ ] Implement `When()` methods in Task aggregate
- [ ] Create command methods (13 total)
- [ ] Add domain validation

**Deliverable:** Task aggregate with all events and commands

---

#### 1.4 Code Generation
**Owner:** Dev 1
**Duration:** 1 day
**Dependencies:** 1.2, 1.3

**Tasks:**
- [ ] Install `ErikLieben.FA.ES.CLI` as local tool:
  ```bash
  dotnet new tool-manifest
  dotnet tool install ErikLieben.FA.ES.CLI
  ```
- [ ] Run code generator:
  ```bash
  dotnet tool run faes
  ```
- [ ] Verify generated files:
  - `Aggregates/Project.Generated.cs` (Fold method)
  - `Aggregates/Task.Generated.cs` (Fold method)
  - `IProject.cs`, `ITask.cs` (interfaces)
  - `ProjectSnapshot.cs`, `TaskSnapshot.cs`
  - `ProjectFactory.cs`, `TaskFactory.cs`
  - `DomainExtensions.Generated.cs` (DI registration)
  - `JsonSerializerContext.Generated.cs` (AOT)
- [ ] Add pre-build event to run generator automatically:
  ```xml
  <Target Name="RunCodeGen" BeforeTargets="BeforeBuild">
    <Exec Command="dotnet tool run faes" />
  </Target>
  ```

**Deliverable:** Code generation working, generated files compile

---

#### 1.5 Aspire AppHost Configuration
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** 1.1

**Tasks:**
- [ ] Create `TaskFlow.AppHost/Program.cs`:
  ```csharp
  var builder = DistributedApplication.CreateBuilder(args);

  // Add Azurite storage emulator
  var storage = builder.AddAzureStorage("storage")
                       .RunAsEmulator();

  var blobs = storage.AddBlobs("blobs");

  // Add API
  var api = builder.AddProject<Projects.TaskFlow_Api>("api")
                   .WithReference(blobs);

  // Add Angular frontend
  var webapp = builder.AddNpmApp("webapp", "../webapp")
                      .WithReference(api)
                      .WithHttpEndpoint(port: 4200, env: "PORT")
                      .PublishAsDockerFile();

  builder.Build().Run();
  ```
- [ ] Configure `TaskFlow.ServiceDefaults/Extensions.cs`:
  - OpenTelemetry configuration
  - Health checks
  - Service discovery
- [ ] Test Aspire dashboard launches
- [ ] Verify Azurite container starts automatically

**Deliverable:** Aspire orchestration working, Azurite accessible

---

#### 1.6 API Setup - Basic Configuration
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 1.4, 1.5

**Tasks:**
- [ ] Create `TaskFlow.Api/Program.cs`:
  ```csharp
  var builder = WebApplication.CreateBuilder(args);

  builder.AddServiceDefaults();
  builder.AddAzureBlobClient("blobs");

  // Register domain
  builder.Services.ConfigureTaskFlowDomainFactory();

  // Configure event store
  builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings(
      containerName: "taskflow-events",
      autoCreateContainer: true));

  builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

  // Add SignalR
  builder.Services.AddSignalR();

  // Add CORS for Angular
  builder.Services.AddCors(options => {
      options.AddPolicy("AllowAngular", policy => {
          policy.WithOrigins("http://localhost:4200")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
      });
  });

  var app = builder.Build();

  app.MapDefaultEndpoints();
  app.UseCors("AllowAngular");

  app.MapHub<TaskFlowHub>("/hub/taskflow");

  app.Run();
  ```
- [ ] Create `Hubs/TaskFlowHub.cs`:
  ```csharp
  public class TaskFlowHub : Hub
  {
      public async Task JoinProject(string projectId)
      {
          await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
      }

      public async Task LeaveProject(string projectId)
      {
          await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
      }
  }
  ```
- [ ] Test API starts and connects to Azurite
- [ ] Test SignalR hub accessible at `/hub/taskflow`

**Deliverable:** API running, SignalR hub operational, Azurite connected

---

#### 1.7 Unit Tests Setup
**Owner:** Dev 2
**Duration:** 1 day
**Dependencies:** 1.2, 1.3

**Tasks:**
- [ ] Add NuGet package: `ErikLieben.FA.ES.Testing`
- [ ] Create test base class:
  ```csharp
  public abstract class AggregateTestBase
  {
      protected TestContext GetContext()
      {
          var services = new ServiceCollection();
          services.ConfigureTaskFlowDomainFactory();
          var provider = services.BuildServiceProvider();
          return TestSetup.GetContext(provider, TaskFlowDomainFactory.Get);
      }
  }
  ```
- [ ] Create sample test:
  ```csharp
  public class ProjectTests : AggregateTestBase
  {
      [Fact]
      public async Task CreateProject_ShouldGenerateProjectInitiatedEvent()
      {
          var context = GetContext();
          var stream = await context.GetEventStreamFor("Project", "proj-001");
          var project = new Project(stream);

          await project.CreateProject("My Project", "Description", "user-123");

          context.Assert
              .ShouldHaveObject("Project", "proj-001")
              .WithEventCount(1)
              .WithEventAtPosition(0, new ProjectInitiated("My Project", "Description", "user-123"));
      }
  }
  ```
- [ ] Verify tests run successfully

**Deliverable:** Test infrastructure working, sample tests passing

---

### Phase 2: Core Features (Week 2-3)

#### 2.1 API Endpoints - Projects
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 1.6

**Tasks:**
- [ ] Create `Endpoints/ProjectEndpoints.cs`:
  ```csharp
  public static class ProjectEndpoints
  {
      public static RouteGroupBuilder MapProjectEndpoints(this IEndpointRouteBuilder routes)
      {
          var group = routes.MapGroup("/api/projects").WithTags("Projects");

          group.MapPost("/", CreateProject);
          group.MapPut("/{id}/name", RenameProject);
          group.MapPut("/{id}/description", UpdateDescription);
          group.MapPost("/{id}/archive", ArchiveProject);
          group.MapPost("/{id}/restore", RestoreProject);
          group.MapPost("/{id}/team", AddTeamMember);
          group.MapDelete("/{id}/team/{userId}", RemoveTeamMember);
          group.MapGet("/", GetAllProjects);
          group.MapGet("/{id}", GetProject);

          return group;
      }

      private static async Task<IResult> CreateProject(
          CreateProjectRequest request,
          IProjectFactory factory,
          IHubContext<TaskFlowHub> hub)
      {
          // Implementation
      }

      // Other endpoint handlers...
  }
  ```
- [ ] Create DTOs:
  - `CreateProjectRequest`, `CreateProjectResponse`
  - `RenameProjectRequest`
  - `UpdateDescriptionRequest`
  - Etc.
- [ ] Add validation using FluentValidation or Data Annotations
- [ ] Integrate SignalR notifications after commands
- [ ] Register endpoints in `Program.cs`
- [ ] Test with `curl` or Postman

**Deliverable:** All project command endpoints functional

---

#### 2.2 API Endpoints - Tasks
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** 1.6

**Tasks:**
- [ ] Create `Endpoints/TaskEndpoints.cs` with all 13 command endpoints
- [ ] Create DTOs for all requests/responses
- [ ] Add validation
- [ ] Integrate SignalR notifications
- [ ] Add query endpoints:
  - `GET /api/tasks` (with filtering)
  - `GET /api/tasks/{id}`
  - `GET /api/tasks/my-queue`
- [ ] Register endpoints in `Program.cs`
- [ ] Test all endpoints

**Deliverable:** All task command and basic query endpoints functional

---

#### 2.3 Projection - Active Tasks
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 2.2

**Tasks:**
- [ ] Create `Projections/ActiveTasksProjection.cs`:
  ```csharp
  [ProjectionWithExternalCheckpoint]
  public partial class ActiveTasksProjection : Projection
  {
      private readonly Dictionary<string, ActiveTaskDto> _tasks = new();

      private void When(TaskPlanned @event, string taskId)
      {
          _tasks[taskId] = new ActiveTaskDto
          {
              TaskId = taskId,
              ProjectId = @event.ProjectId,
              Title = @event.Title,
              Priority = @event.Priority,
              Status = TaskStatus.ToDo,
              PlannedAt = DateTime.UtcNow
          };
      }

      private void When(WorkCompleted @event, string taskId)
      {
          _tasks.Remove(taskId);
      }

      private void When(ResponsibilityAssigned @event, string taskId)
      {
          if (_tasks.TryGetValue(taskId, out var task))
              task.AssignedTo = @event.MemberId;
      }

      // Other When methods...

      public IEnumerable<ActiveTaskDto> GetTasks(string? projectId = null, string? assignedTo = null)
      {
          var query = _tasks.Values.AsEnumerable();
          if (projectId != null) query = query.Where(t => t.ProjectId == projectId);
          if (assignedTo != null) query = query.Where(t => t.AssignedTo == assignedTo);
          return query.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt);
      }
  }
  ```
- [ ] Run code generator to create `Fold()` method
- [ ] Create projection manager service:
  ```csharp
  public class ProjectionManager : BackgroundService
  {
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          while (!stoppingToken.IsCancellationRequested)
          {
              await UpdateProjection<ActiveTasksProjection>(stoppingToken);
              await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
          }
      }
  }
  ```
- [ ] Register projection manager in DI
- [ ] Add endpoint: `GET /api/tasks` that queries projection
- [ ] Test projection updates when events occur

**Deliverable:** ActiveTasksProjection working, query endpoint functional

---

#### 2.4 Projection - Project Dashboard
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** 2.2

**Tasks:**
- [ ] Create `Projections/ProjectDashboardProjection.cs`:
  ```csharp
  public partial class ProjectDashboardProjection : Projection
  {
      private readonly Dictionary<string, ProjectMetrics> _metrics = new();

      private void When(ProjectInitiated @event, string projectId)
      {
          _metrics[projectId] = new ProjectMetrics { ProjectId = projectId };
      }

      private void When(TaskPlanned @event, string taskId)
      {
          if (_metrics.TryGetValue(@event.ProjectId, out var metrics))
              metrics.TotalTasks++;
      }

      private void When(WorkCompleted @event, string taskId, string projectId)
      {
          if (_metrics.TryGetValue(projectId, out var metrics))
              metrics.CompletedTasks++;
      }

      // More When methods for comprehensive metrics...

      public ProjectMetrics? GetMetrics(string projectId)
          => _metrics.TryGetValue(projectId, out var metrics) ? metrics : null;
  }
  ```
- [ ] Create `ProjectMetrics` DTO with:
  - TotalTasks, CompletedTasks, ActiveTasks
  - TasksByPriority (dictionary)
  - TasksByAssignee (dictionary)
  - AverageCompletionTime
  - LastActivityAt
- [ ] Implement complex metric calculations
- [ ] Add endpoint: `GET /api/projects/{id}/dashboard`
- [ ] Test metric accuracy

**Deliverable:** ProjectDashboardProjection with rich metrics

---

#### 2.5 Unit Tests - Aggregates
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 1.7, 2.1, 2.2

**Tasks:**
- [ ] Write tests for Project aggregate:
  - CreateProject
  - RenameProject
  - UpdateDescription
  - ArchiveProject and RestoreProject
  - AddTeamMember and RemoveTeamMember
  - Validation scenarios
  - Edge cases
- [ ] Write tests for Task aggregate (all 13 commands)
- [ ] Test domain validation logic
- [ ] Test event generation
- [ ] Target > 90% code coverage for aggregates

**Deliverable:** Comprehensive unit tests for domain model

---

#### 2.6 Unit Tests - Projections
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** 2.3, 2.4

**Tasks:**
- [ ] Write tests for ActiveTasksProjection:
  ```csharp
  [Fact]
  public async Task When_TaskCreated_ShouldAddToActiveTasks()
  {
      var context = GetContext();
      var projection = new ActiveTasksProjection();

      var @event = new TaskCreated("proj-001", "Task 1", "Description", TaskPriority.High);
      await projection.Fold(context, new[] {
          new Event<TaskCreated>("task-001", 0, @event)
      });

      var tasks = projection.GetTasks();
      tasks.Should().HaveCount(1);
      tasks.First().Title.Should().Be("Task 1");
  }
  ```
- [ ] Write tests for ProjectDashboardProjection
- [ ] Test metric calculations
- [ ] Test checkpoint advancement
- [ ] Target > 80% code coverage for projections

**Deliverable:** Comprehensive unit tests for projections

---

### Phase 3: Frontend Foundation (Week 3-4)

#### 3.1 Angular Project Setup
**Owner:** Dev 1
**Duration:** 1 day
**Dependencies:** 1.5

**Tasks:**
- [ ] Create Angular app:
  ```bash
  cd demo
  npx @angular/cli@19 new webapp --standalone --routing --style scss
  ```
- [ ] Install dependencies:
  ```bash
  npm install @angular/material @microsoft/signalr
  npm install @ngrx/signals
  ```
- [ ] Configure `angular.json` for Aspire:
  - Build output: `dist/webapp/browser`
  - Port: 4200
- [ ] Create `package.json` scripts:
  ```json
  {
    "scripts": {
      "start": "ng serve",
      "build": "ng build",
      "test": "ng test",
      "lint": "ng lint"
    }
  }
  ```
- [ ] Configure proxy for API calls:
  ```json
  {
    "/api": {
      "target": "http://localhost:5000",
      "secure": false
    },
    "/hub": {
      "target": "http://localhost:5000",
      "secure": false,
      "ws": true
    }
  }
  ```
- [ ] Test Angular app launches via Aspire

**Deliverable:** Angular app building and serving via Aspire

---

#### 3.2 Angular Core Services
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** 3.1

**Tasks:**
- [ ] Create `core/services/signalr.service.ts`:
  ```typescript
  @Injectable({ providedIn: 'root' })
  export class SignalRService {
    private hubConnection: HubConnection;

    constructor() {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl('/hub/taskflow')
        .withAutomaticReconnect()
        .build();
    }

    async start(): Promise<void> {
      await this.hubConnection.start();
    }

    async joinProject(projectId: string): Promise<void> {
      await this.hubConnection.invoke('JoinProject', projectId);
    }

    onTaskPlanned(callback: (task: TaskDto) => void): void {
      this.hubConnection.on('TaskPlanned', callback);
    }

    // Other event subscriptions...
  }
  ```
- [ ] Create `core/services/project.service.ts`:
  ```typescript
  @Injectable({ providedIn: 'root' })
  export class ProjectService {
    private apiUrl = '/api/projects';

    constructor(private http: HttpClient) {}

    createProject(request: CreateProjectRequest): Observable<ProjectDto> {
      return this.http.post<ProjectDto>(this.apiUrl, request);
    }

    getProjects(): Observable<ProjectDto[]> {
      return this.http.get<ProjectDto[]>(this.apiUrl);
    }

    // Other methods...
  }
  ```
- [ ] Create `core/services/task.service.ts` with all task operations
- [ ] Create models in `core/models/` (ProjectDto, TaskDto, etc.)
- [ ] Test services with mock data

**Deliverable:** Core services for API and SignalR communication

---

#### 3.3 Material UI Setup & Theme
**Owner:** Dev 1
**Duration:** 1 day
**Dependencies:** 3.1

**Tasks:**
- [ ] Configure Angular Material:
  ```bash
  ng add @angular/material
  ```
- [ ] Create custom theme in `styles.scss`:
  ```scss
  @use '@angular/material' as mat;

  $primary: mat.define-palette(mat.$indigo-palette);
  $accent: mat.define-palette(mat.$pink-palette);
  $warn: mat.define-palette(mat.$red-palette);

  $theme: mat.define-light-theme((
    color: (primary: $primary, accent: $accent, warn: $warn)
  ));

  @include mat.all-component-themes($theme);
  ```
- [ ] Create shared components:
  - `shared/components/loading-spinner.component.ts`
  - `shared/components/error-message.component.ts`
  - `shared/components/confirm-dialog.component.ts`
- [ ] Create layout components:
  - `layout/header.component.ts`
  - `layout/sidebar.component.ts`
  - `layout/main-layout.component.ts`

**Deliverable:** Material UI configured, shared components ready

---

#### 3.4 Project Feature Components
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** 3.2, 3.3

**Tasks:**
- [ ] Create `features/projects/project-list.component.ts`:
  - Display all projects in Material cards
  - Filter and search
  - "New Project" button
  - Click to navigate to project detail
- [ ] Create `features/projects/project-detail.component.ts`:
  - Show project info
  - Team members section
  - Task list for project
  - Tabs: Tasks, Dashboard, Team, Settings
- [ ] Create `features/projects/project-form.component.ts`:
  - Reactive form for create/edit
  - Validation
  - Submit to API
- [ ] Create routing in `app.routes.ts`:
  ```typescript
  export const routes: Routes = [
    { path: '', redirectTo: '/projects', pathMatch: 'full' },
    { path: 'projects', component: ProjectListComponent },
    { path: 'projects/:id', component: ProjectDetailComponent },
    // More routes...
  ];
  ```
- [ ] Style components with Material design

**Deliverable:** Project management UI functional

---

#### 3.5 Task Feature Components
**Owner:** Dev 1
**Duration:** 3 days
**Dependencies:** 3.2, 3.3

**Tasks:**
- [ ] Create `features/tasks/task-list.component.ts`:
  - Display tasks in Material table or cards
  - Sortable columns
  - Priority badges with colors
  - Status indicators
  - Quick actions (assign, start, complete)
- [ ] Create `features/tasks/task-detail.component.ts`:
  - Show full task info
  - Edit inline
  - Comments section
  - Tabs: Details, History, Activity
- [ ] Create `features/tasks/task-form.component.ts`:
  - Create/edit task form
  - Priority dropdown
  - Assignee autocomplete
  - Due date picker
- [ ] Create `features/tasks/task-board.component.ts`:
  - Kanban-style board (To Do, In Progress, Completed)
  - Drag-and-drop (using Angular CDK)
  - Filter by assignee
- [ ] Add routing for task pages

**Deliverable:** Task management UI functional

---

#### 3.6 SignalR Integration
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** 3.2, 3.4, 3.5

**Tasks:**
- [ ] Initialize SignalR in `app.component.ts`:
  ```typescript
  export class AppComponent implements OnInit {
    constructor(private signalR: SignalRService) {}

    async ngOnInit() {
      await this.signalR.start();
      this.setupEventListeners();
    }

    private setupEventListeners() {
      this.signalR.onTaskPlanned(task => {
        // Update UI or show notification
      });
      // Other listeners...
    }
  }
  ```
- [ ] Add real-time updates to ProjectDetailComponent:
  - Join project room on component init
  - Leave project room on component destroy
  - Update task list when TaskCreated event received
- [ ] Add toast notifications using Material Snackbar:
  ```typescript
  this.snackBar.open('New task created by Bob', 'Close', { duration: 3000 });
  ```
- [ ] Add connection status indicator in header
- [ ] Test real-time updates with multiple browser windows

**Deliverable:** Real-time updates working across browser instances

---

### Phase 4: Advanced Features (Week 4-5)

#### 4.1 Event History & Timeline UI
**Owner:** Dev 1
**Duration:** 3 days
**Dependencies:** 3.5

**Tasks:**
- [ ] Create API endpoint: `GET /api/admin/events/task/{id}`:
  ```csharp
  private static async Task<IResult> GetTaskEvents(
      string id,
      IEventStream stream)
  {
      var events = await stream.ReadAsync(startVersion: 0);
      var dtos = events.Select(e => new EventDto
      {
          EventType = e.EventName,
          Timestamp = e.Metadata.Timestamp,
          Version = e.Version,
          Payload = e.Payload
      });
      return Results.Ok(dtos);
  }
  ```
- [ ] Create `features/admin/event-timeline.component.ts`:
  - Fetch all events for task
  - Display as vertical timeline
  - Material timeline component or custom CSS
  - Each event shows:
    - Icon (based on event type)
    - Event name (human-friendly)
    - Timestamp (relative: "2 hours ago")
    - User who triggered it
    - Expandable details (JSON payload)
  - Syntax highlighting for JSON (use `ngx-highlightjs`)
- [ ] Add "History" tab to TaskDetailComponent
- [ ] Style timeline with Material design

**Deliverable:** Event history viewer functional

---

#### 4.2 Time Travel UI
**Owner:** Dev 2
**Duration:** 4 days
**Dependencies:** 4.1

**Tasks:**
- [ ] Create API endpoint: `GET /api/admin/tasks/{id}/version/{version}`:
  ```csharp
  private static async Task<IResult> GetTaskAtVersion(
      string id,
      int version,
      ITaskFactory factory)
  {
      var task = await factory.Create(id);
      var stream = task.Stream;
      var events = await stream.ReadAsync(startVersion: 0, untilVersion: version);

      // Create new instance and fold only up to version
      var historicalTask = new Task(stream);
      historicalTask.Fold(events);

      return Results.Ok(new TaskDto
      {
          TaskId = id,
          Title = historicalTask.Title,
          // Map other properties...
          Version = version
      });
  }
  ```
- [ ] Create `features/admin/time-travel.component.ts`:
  - Slider control (Material Slider) for version selection
  - Min: 0, Max: total event count
  - Display current version number and date
  - Fetch task state at selected version
  - Display task state in read-only form
  - "Compare with Current" button shows diff
  - "Export State" button downloads JSON
- [ ] Create `shared/components/state-diff-viewer.component.ts`:
  - Side-by-side comparison
  - Highlight added (green), removed (red), changed (yellow) fields
  - Use JSON diff library (e.g., `jsondiffpatch`)
- [ ] Add "Time Travel" tab to TaskDetailComponent
- [ ] Visual indicator: "Viewing version 5 of 12"
- [ ] "Return to Present" button

**Deliverable:** Time travel UI with version slider and state comparison

---

#### 4.3 Event Upcasting Implementation
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** Phase 2

**Tasks:**
- [ ] Create legacy event: `TaskProjectChanged` (old schema):
  ```csharp
  [EventName("Task.ProjectChanged")]
  public record TaskProjectChanged(string NewProjectId);
  ```
- [ ] Create upcaster: `Upcasters/TaskRelocatedUpcaster.cs`:
  ```csharp
  public class TaskRelocatedUpcaster : IEventUpcaster
  {
      public bool CanUpcast(IEvent @event)
          => @event.EventName == "Task.ProjectChanged";

      public IEnumerable<IEvent> UpCast(IEvent oldEvent)
      {
          var data = JsonSerializer.Deserialize<TaskProjectChanged>(oldEvent.Payload);
          yield return Event.Create(new TaskRelocated(
              newProjectId: data.NewProjectId,
              relocatedBy: "system",
              rationale: "Migrated from legacy format"));
      }
  }
  ```
- [ ] Register upcaster in DI:
  ```csharp
  builder.Services.AddSingleton<IEventUpcaster, TaskRelocatedUpcaster>();
  ```
- [ ] Add seed data with legacy events
- [ ] Create admin endpoint: `GET /api/admin/upcasters/stats`:
  - Show upcaster name
  - Show events upcasted count
  - Show source → target event mapping
- [ ] Create UI component to display upcaster stats
- [ ] Verify old events are transparently upcasted when read

**Deliverable:** Event upcasting working with demo data

---

#### 4.4 Snapshot Management
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** Phase 2

**Tasks:**
- [ ] Create API endpoints:
  ```csharp
  POST /api/admin/tasks/{id}/snapshot  // Create snapshot
  GET  /api/admin/snapshots             // List all snapshots
  DELETE /api/admin/snapshots/{id}      // Delete snapshot
  ```
- [ ] Implement snapshot creation:
  ```csharp
  private static async Task<IResult> CreateSnapshot(
      string id,
      ITaskFactory factory)
  {
      var task = await factory.Create(id);
      var snapshot = new TaskSnapshot
      {
          Title = task.Title,
          Description = task.Description,
          // Map all properties...
      };

      await task.Stream.Snapshot(snapshot);
      return Results.Ok(new { message = "Snapshot created", version = task.Version });
  }
  ```
- [ ] Create seed data task with 1000+ events (comments loop)
- [ ] Create `features/admin/snapshot-manager.component.ts`:
  - List all snapshots with metadata
  - Create snapshot button per task
  - Delete snapshot button
  - Show snapshot stats: version, size, age
- [ ] Create performance comparison component:
  - "Fold without Snapshot" button → measure time
  - "Create Snapshot" button
  - "Fold with Snapshot" button → measure time
  - Display: "75% faster with snapshot (120ms → 30ms)"
- [ ] Add "Snapshots" section to admin panel

**Deliverable:** Snapshot management UI with performance demonstration

---

#### 4.5 Additional Projections
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** 2.3, 2.4

**Tasks:**
- [ ] Create `Projections/UserWorkQueueProjection.cs`:
  - Maintain per-user task lists
  - Update on TaskAssigned, TaskUnassigned, TaskCompleted
  - Query method: `GetUserTasks(userId)`
- [ ] Create `Projections/TaskActivityProjection.cs`:
  - Maintain timeline of all task activity
  - Natural language descriptions
  - Query method: `GetActivity(taskId)`
- [ ] Add both projections to projection manager
- [ ] Create API endpoints to query projections
- [ ] Create UI components:
  - "My Tasks" page (UserWorkQueue)
  - "Activity" tab on TaskDetail (TaskActivity)

**Deliverable:** Two additional projections with UI

---

#### 4.6 Admin Panel
**Owner:** Dev 2
**Duration:** 3 days
**Dependencies:** 4.1, 4.3, 4.4

**Tasks:**
- [ ] Create `features/admin/admin-panel.component.ts`:
  - Dashboard layout with Material cards
  - Sections:
    - Projection Health
    - Event Explorer
    - Snapshot Manager
    - Upcaster Stats
    - Demo Data
- [ ] Create `features/admin/projection-health.component.ts`:
  - Table showing all projections
  - Columns: Name, Status, Lag, Last Updated, Checkpoint
  - Health indicator badges (green/yellow/red)
  - "Rebuild" button per projection
  - Auto-refresh every 5 seconds
- [ ] Create API endpoint: `GET /api/projections`:
  ```csharp
  private static async Task<IResult> GetProjectionHealth(
      IEnumerable<IProjection> projections)
  {
      var health = projections.Select(p => new ProjectionHealthDto
      {
          Name = p.GetType().Name,
          Checkpoint = p.GetCheckpoint(),
          LastUpdated = p.LastUpdated,
          // Calculate lag...
      });
      return Results.Ok(health);
  }
  ```
- [ ] Create `features/admin/event-explorer.component.ts`:
  - Dropdown to select aggregate type (Project/Task)
  - Input for aggregate ID
  - "Load Events" button
  - Display events in expandable Material table
  - Copy JSON button
  - Download JSON button
- [ ] Add routing: `/admin`
- [ ] Secure admin routes (basic auth or mock user check)

**Deliverable:** Comprehensive admin panel with all tools

---

#### 4.7 Demo Seed Data
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** Phase 2

**Tasks:**
- [ ] Create `Endpoints/AdminEndpoints.cs`:
  ```csharp
  private static async Task<IResult> SeedDemoData(
      IProjectFactory projectFactory,
      ITaskFactory taskFactory)
  {
      // Project 1: Completed
      var proj1 = await projectFactory.Create("proj-001");
      await proj1.CreateProject("Website Redesign", "Q4 2024 redesign project", "user-001");
      await proj1.AddTeamMember("user-002", "Designer");
      await proj1.AddTeamMember("user-003", "Developer");

      // Task with many events (snapshot demo)
      var task1 = await taskFactory.Create("task-001");
      await task1.CreateTask("proj-001", "Homepage Mockup", "Design new homepage", TaskPriority.High);
      await task1.AssignTask("user-002");
      await task1.StartTask();

      // Add 100 feedback entries to create long event stream
      for (int i = 0; i < 100; i++)
      {
          await task1.ProvideFeedback($"Progress update #{i}", "user-002");
      }

      await task1.CompleteTask("Final design approved");

      // Task with legacy event (upcasting demo)
      var task2 = await taskFactory.Create("task-002");
      // Manually append legacy TaskProjectChanged event to storage...

      // More projects and tasks...

      return Results.Ok(new { message = "Demo data seeded", projects = 3, tasks = 30 });
  }
  ```
- [ ] Create detailed seed data:
  - 3 projects:
    - "Website Redesign" (archived, 10 completed tasks)
    - "Mobile App Launch" (active, 15 tasks in various states)
    - "Marketing Campaign" (new, 5 tasks)
  - 30 tasks total with varied:
    - Priorities
    - Statuses
    - Assignees
    - Due dates (some overdue)
    - Comment counts
  - 1 task with 1000 events (snapshot demo)
  - 1 task with legacy event (upcasting demo)
- [ ] Add "Seed Demo Data" button in admin panel
- [ ] Add "Clear All Data" button (with confirmation)

**Deliverable:** Rich demo data that showcases all features

---

### Phase 5: Polish & Documentation (Week 5-6)

#### 5.1 Comprehensive README
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** All previous phases

**Tasks:**
- [ ] Create `demo/README.md` with sections:
  - Introduction
  - What is TaskFlow?
  - What is Event Sourcing?
  - Features Demonstrated
  - Quick Start (5-minute setup)
  - Prerequisites
  - Running the Demo
  - Project Structure
  - Exploring the Demo (guided tour)
  - Architecture Overview
  - Key Concepts Explained
  - Code Examples
  - Troubleshooting
  - Contributing
  - License
- [ ] Add screenshots of key UI components
- [ ] Create architecture diagrams (use Mermaid or PlantUML)
- [ ] Add code snippets for key patterns
- [ ] Add links to ErikLieben.FA.ES docs
- [ ] Proofread and format

**Deliverable:** Professional README that enables easy onboarding

---

#### 5.2 Code Documentation
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** All previous phases

**Tasks:**
- [ ] Add XML documentation comments to:
  - All public APIs
  - All aggregate methods
  - All projection methods
  - All endpoint handlers
- [ ] Add inline comments explaining:
  - Why certain patterns are used
  - Event sourcing concepts
  - Complex logic
- [ ] Create `docs/` folder with:
  - `EventSourcingConcepts.md`
  - `AggregatePatterns.md`
  - `ProjectionStrategies.md`
  - `APIDesign.md`
- [ ] Add JSDoc comments to TypeScript code
- [ ] Generate API documentation (Swagger/OpenAPI)

**Deliverable:** Well-documented codebase with learning resources

---

#### 5.3 Integration Tests
**Owner:** Dev 1
**Duration:** 3 days
**Dependencies:** Phase 2

**Tasks:**
- [ ] Create `TaskFlow.Api.Tests/IntegrationTestBase.cs`:
  ```csharp
  public abstract class IntegrationTestBase : IAsyncLifetime
  {
      protected HttpClient Client { get; private set; }
      private WebApplicationFactory<Program> _factory;

      public async Task InitializeAsync()
      {
          _factory = new WebApplicationFactory<Program>()
              .WithWebHostBuilder(builder => {
                  builder.ConfigureServices(services => {
                      // Use in-memory event store for tests
                  });
              });
          Client = _factory.CreateClient();
      }

      // Cleanup...
  }
  ```
- [ ] Write integration tests:
  - Project CRUD operations
  - Task CRUD operations
  - Task lifecycle (create → assign → start → complete)
  - Projection updates after commands
  - SignalR event broadcasting
- [ ] Test error scenarios:
  - Invalid input
  - Concurrency conflicts
  - Not found scenarios
- [ ] Target > 70% integration test coverage
- [ ] Ensure tests are fast (< 30 seconds total)

**Deliverable:** Comprehensive integration test suite

---

#### 5.4 Performance Optimization
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** All previous phases

**Tasks:**
- [ ] Profile API endpoints:
  - Identify slow queries
  - Optimize projection queries
  - Add caching where appropriate
- [ ] Optimize frontend:
  - Lazy load feature modules
  - Use OnPush change detection strategy
  - Optimize bundle size (analyze with webpack-bundle-analyzer)
  - Add loading skeletons for better perceived performance
- [ ] Optimize SignalR:
  - Batch notifications if many events occur rapidly
  - Debounce projection update notifications
- [ ] Add performance benchmarks:
  - Fold time with/without snapshots
  - Projection lag under load
  - API response times
- [ ] Document performance characteristics in README

**Deliverable:** Optimized application with documented performance

---

#### 5.5 UI Polish
**Owner:** Dev 1
**Duration:** 2 days
**Dependencies:** Phase 3, 4

**Tasks:**
- [ ] Improve visual design:
  - Consistent spacing and alignment
  - Better color scheme
  - Enhanced typography
  - Add task priority color coding (red=critical, orange=high, yellow=medium, gray=low)
- [ ] Add animations:
  - Page transitions
  - Card hover effects
  - Loading states
  - Toast notifications sliding in
- [ ] Improve UX:
  - Better empty states ("No tasks yet - create one!")
  - Skeleton loaders while data loads
  - Optimistic UI updates
  - Better error messages
  - Form validation feedback
- [ ] Add responsive design:
  - Mobile-friendly layouts
  - Responsive tables (collapse to cards on mobile)
  - Hamburger menu for mobile
- [ ] Accessibility:
  - ARIA labels
  - Keyboard navigation
  - Focus indicators
  - Screen reader support

**Deliverable:** Polished, professional-looking UI

---

#### 5.6 E2E Tests (Optional)
**Owner:** Dev 2
**Duration:** 2 days
**Dependencies:** Phase 5.5

**Tasks:**
- [ ] Set up Playwright or Cypress
- [ ] Write E2E tests for critical flows:
  - Create project → Add team member → Create task → Complete task
  - Time travel through task history
  - Rebuild projection
  - Create snapshot and compare performance
- [ ] Add to CI/CD pipeline (future)

**Deliverable:** E2E test suite for critical user journeys

---

## Technical Setup

### Prerequisites
- .NET 9.0 SDK or later
- Node.js 20+ and npm
- Docker Desktop (for Azurite)
- Visual Studio 2022 / VS Code / Rider
- Git

### Development Environment Setup

```bash
# Clone repository
git clone https://github.com/eriklieben/fa-es.git
cd fa-es/demo

# Restore .NET dependencies
dotnet restore

# Install .NET Aspire workload
dotnet workload install aspire

# Install .NET CLI tool
dotnet tool restore

# Install frontend dependencies
cd webapp
npm install
cd ..

# Run code generator
cd src/TaskFlow.Domain
dotnet tool run faes
cd ../..

# Start Aspire AppHost (starts everything)
dotnet run --project src/TaskFlow.AppHost
```

### Accessing the Application
- **Aspire Dashboard:** `http://localhost:15000`
- **Angular Frontend:** `http://localhost:4200`
- **API:** `http://localhost:5000`
- **Azurite Storage Explorer:** `http://localhost:10000`

---

## Project Structure

```
demo/
├── TaskFlow.sln
├── README.md
├── PRD.md
├── UserStories.md
├── ImplementationPlan.md
│
├── src/
│   ├── TaskFlow.Domain/
│   │   ├── Aggregates/
│   │   │   ├── Project.cs
│   │   │   ├── Project.Generated.cs
│   │   │   ├── Task.cs
│   │   │   └── Task.Generated.cs
│   │   ├── Events/
│   │   │   ├── ProjectEvents.cs
│   │   │   └── TaskEvents.cs
│   │   ├── Projections/
│   │   │   ├── ActiveTasksProjection.cs
│   │   │   ├── ProjectDashboardProjection.cs
│   │   │   ├── UserWorkQueueProjection.cs
│   │   │   └── TaskActivityProjection.cs
│   │   ├── Upcasters/
│   │   │   └── TaskMovedToProjectUpcaster.cs
│   │   ├── Services/
│   │   │   └── ProjectionManager.cs
│   │   ├── DomainExtensions.cs
│   │   └── DomainExtensions.Generated.cs
│   │
│   ├── TaskFlow.Api/
│   │   ├── Program.cs
│   │   ├── Endpoints/
│   │   │   ├── ProjectEndpoints.cs
│   │   │   ├── TaskEndpoints.cs
│   │   │   ├── ProjectionEndpoints.cs
│   │   │   └── AdminEndpoints.cs
│   │   ├── Hubs/
│   │   │   └── TaskFlowHub.cs
│   │   ├── DTOs/
│   │   │   ├── ProjectDtos.cs
│   │   │   ├── TaskDtos.cs
│   │   │   └── ProjectionDtos.cs
│   │   └── Middleware/
│   │       └── ExceptionHandlingMiddleware.cs
│   │
│   ├── TaskFlow.AppHost/
│   │   └── Program.cs
│   │
│   └── TaskFlow.ServiceDefaults/
│       └── Extensions.cs
│
├── webapp/
│   ├── src/
│   │   ├── app/
│   │   │   ├── app.component.ts
│   │   │   ├── app.routes.ts
│   │   │   ├── features/
│   │   │   │   ├── projects/
│   │   │   │   ├── tasks/
│   │   │   │   ├── projections/
│   │   │   │   └── admin/
│   │   │   ├── core/
│   │   │   │   ├── services/
│   │   │   │   └── models/
│   │   │   ├── shared/
│   │   │   │   └── components/
│   │   │   └── layout/
│   │   ├── assets/
│   │   └── styles.scss
│   ├── angular.json
│   ├── package.json
│   └── tsconfig.json
│
├── tests/
│   ├── TaskFlow.Domain.Tests/
│   │   ├── ProjectTests.cs
│   │   ├── TaskTests.cs
│   │   └── ProjectionTests.cs
│   ├── TaskFlow.Api.Tests/
│   │   └── IntegrationTests.cs
│   └── webapp-e2e/
│       └── (Playwright/Cypress tests)
│
└── docs/
    ├── EventSourcingConcepts.md
    ├── AggregatePatterns.md
    ├── ProjectionStrategies.md
    └── APIDesign.md
```

---

## Development Workflow

### Daily Development Cycle

1. **Start Aspire:**
   ```bash
   dotnet run --project src/TaskFlow.AppHost
   ```

2. **Open Aspire Dashboard:**
   - Navigate to `http://localhost:15000`
   - Monitor all services (API, webapp, Azurite)
   - View logs, traces, metrics

3. **Hot Reload:**
   - Backend: Change C# code → automatic reload
   - Frontend: Change TypeScript/HTML → automatic reload

4. **Code Generation:**
   - When adding new events/aggregates: `dotnet tool run faes`
   - Or configure pre-build event to run automatically

5. **Testing:**
   ```bash
   # Unit tests
   dotnet test

   # Integration tests
   dotnet test --filter "Category=Integration"

   # Frontend tests
   cd webapp && npm test
   ```

### Git Workflow

```bash
# Create feature branch
git checkout -b feature/time-travel-ui

# Make changes, commit frequently
git add .
git commit -m "feat: add time travel slider component"

# Push and create PR
git push origin feature/time-travel-ui
```

### Code Review Checklist
- [ ] Code follows established patterns
- [ ] XML documentation added
- [ ] Unit tests included
- [ ] No compiler warnings
- [ ] Event names follow convention
- [ ] Projections update correctly
- [ ] SignalR notifications sent
- [ ] README updated if needed

---

## Testing Strategy

### Unit Testing
- **Framework:** xUnit
- **Scope:** Aggregates, projections, domain logic
- **Coverage Target:** > 90%
- **Pattern:**
  ```csharp
  [Fact]
  public async Task CommandName_Scenario_ExpectedResult()
  {
      // Arrange
      var context = GetContext();
      var aggregate = CreateAggregate(context);

      // Act
      await aggregate.DoSomething();

      // Assert
      context.Assert
          .ShouldHaveObject("Type", "id")
          .WithEventCount(1)
          .WithEventAtPosition(0, new ExpectedEvent(...));
  }
  ```

### Integration Testing
- **Framework:** xUnit + WebApplicationFactory
- **Scope:** API endpoints, projection updates, SignalR
- **Coverage Target:** > 70%
- **Pattern:**
  ```csharp
  [Fact]
  public async Task CreateProject_ValidInput_ReturnsCreated()
  {
      // Arrange
      var request = new CreateProjectRequest { Name = "Test", ... };

      // Act
      var response = await Client.PostAsJsonAsync("/api/projects", request);

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.Created);
      var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
      project.Name.Should().Be("Test");
  }
  ```

### E2E Testing (Optional)
- **Framework:** Playwright or Cypress
- **Scope:** Critical user journeys
- **Coverage:** Happy paths + error scenarios

---

## Deployment Plan

### Local Development (Aspire)
```bash
dotnet run --project src/TaskFlow.AppHost
```
- All services orchestrated by Aspire
- Azurite emulator for storage
- Hot reload for both backend and frontend

### Docker (Alternative to Aspire)
```bash
docker-compose up
```
- Create `docker-compose.yml` with:
  - TaskFlow.Api (ASP.NET)
  - webapp (Nginx serving Angular)
  - Azurite

### Production (Azure)
- **Compute:** Azure App Service (API) + Azure Static Web Apps (Frontend)
- **Storage:** Azure Blob Storage (replace Azurite)
- **Monitoring:** Application Insights
- **CI/CD:** GitHub Actions
- **Configuration:** Swap Azurite connection string for real Azure Storage

---

## Risk Mitigation

### Technical Risks

| Risk | Mitigation |
|------|-----------|
| Aspire learning curve | Provide Docker Compose alternative |
| SignalR connection issues | Implement graceful degradation, polling fallback |
| Complex setup | Automated scripts, comprehensive docs |
| Performance with large event streams | Implement snapshots early, demonstrate benefits |

### Project Risks

| Risk | Mitigation |
|------|-----------|
| Scope creep | Strict MVP definition, "Nice to Have" backlog |
| Timeline slippage | Two-week sprints with clear deliverables |
| Knowledge gaps | Pair programming, knowledge sharing sessions |
| Testing neglect | Test coverage gates, TDD encouraged |

---

## Definition of Done

### For Each Phase

- [ ] All tasks completed
- [ ] Code reviewed and approved
- [ ] Unit tests passing (> 90% coverage for domain)
- [ ] Integration tests passing (if applicable)
- [ ] Documentation updated
- [ ] No compiler warnings
- [ ] Changes merged to main branch

### For Overall Project

- [ ] All 5 phases completed
- [ ] README.md comprehensive and clear
- [ ] Demo seed data working
- [ ] All 4 event sourcing capabilities demonstrated
- [ ] Performance benchmarks documented
- [ ] API endpoints functional
- [ ] Frontend polished and responsive
- [ ] SignalR real-time updates working
- [ ] Admin panel complete
- [ ] Test coverage targets met
- [ ] Can run demo in < 5 minutes
- [ ] Code is production-quality

---

## Appendix

### A. Technology Versions

| Technology | Version | Reason |
|-----------|---------|--------|
| .NET | 9.0 | Latest LTS, AOT support |
| Angular | 19 | Latest, standalone components |
| ErikLieben.FA.ES | 1.3.1+ | Latest stable |
| Aspire | 9.0+ | Latest, best .NET integration |
| SignalR | 9.0+ | Matches .NET version |
| Angular Material | 19+ | Matches Angular version |

### B. Useful Commands

```bash
# Clean solution
dotnet clean

# Rebuild solution
dotnet build

# Run specific project
dotnet run --project src/TaskFlow.Api

# Watch mode (hot reload)
dotnet watch --project src/TaskFlow.Api

# Generate code
dotnet tool run faes

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Frontend dev server
cd webapp && npm start

# Frontend build
cd webapp && npm run build

# Aspire dashboard
dotnet run --project src/TaskFlow.AppHost
```

### C. Learning Resources

- ErikLieben.FA.ES GitHub: https://github.com/eriklieben/fa-es
- Event Sourcing Pattern: https://martinfowler.com/eaaDev/EventSourcing.html
- CQRS Pattern: https://martinfowler.com/bliki/CQRS.html
- .NET Aspire Docs: https://learn.microsoft.com/en-us/dotnet/aspire/
- SignalR Docs: https://learn.microsoft.com/en-us/aspnet/core/signalr/
- Angular Docs: https://angular.dev/

---

**Document Status:** Ready for Execution
**Next Steps:** Begin Phase 1 - Foundation
**Estimated Completion:** 6-8 weeks from start date
