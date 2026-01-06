# Product Requirements Document (PRD)
## Event Sourcing Demo: TaskFlow Project Management System

**Version:** 1.0
**Date:** 2025-11-10
**Author:** ErikLieben.FA.ES Team
**Status:** Draft

---

## 1. Executive Summary

TaskFlow is a demonstration project management application built to showcase the capabilities of the **ErikLieben.FA.ES** event sourcing framework. It provides a practical, real-world example that helps developers and architects understand event sourcing concepts through a familiar domain: project and task management.

### Purpose
Enable developers new to event sourcing to:
- Understand core event sourcing concepts through hands-on exploration
- See production-ready patterns for ASP.NET Core APIs with event sourcing
- Learn how to build event-sourced Angular frontends with real-time updates
- Explore advanced capabilities: projections, time travel, event upcasting, and snapshots

---

## 2. Goals & Objectives

### Primary Goals
1. **Educational Excellence**: Provide the best learning resource for ErikLieben.FA.ES framework adoption
2. **Production Patterns**: Demonstrate real-world architectural patterns and best practices
3. **Feature Showcase**: Highlight all major framework capabilities in practical scenarios
4. **Developer Experience**: Create an intuitive, explorable demo that inspires confidence

### Success Metrics
- Developers can set up and run the demo in under 5 minutes
- All four core event sourcing benefits are clearly demonstrated
- Code is clean, well-documented, and production-quality
- Demo supports both local (Azurite) and Azure Blob Storage without code changes

---

## 3. Target Audience

### Primary Audience
- **.NET developers** exploring event sourcing for the first time
- **Solution architects** evaluating event sourcing for projects
- **Engineering teams** considering ErikLieben.FA.ES adoption

### Secondary Audience
- **Technical decision-makers** assessing event sourcing benefits
- **Workshop facilitators** teaching event sourcing patterns
- **Open-source contributors** learning the framework internals

---

## 4. Product Overview

### The Domain: Project Management
TaskFlow manages projects and tasks with full event history. Users can:
- Create projects with teams and goals
- Create, assign, and update tasks
- Track task lifecycle from creation to completion
- View multiple projections of the same data
- Time-travel to see historical states
- Audit complete change history

### Why This Domain?
- **Universally understood**: All developers relate to project management
- **Rich event model**: Projects and tasks naturally generate diverse events
- **Audit benefits**: Task history and responsibility tracking are crucial
- **Multiple views**: Same data viewed as boards, lists, reports, timelines
- **Long-running**: Projects persist over time (good for snapshot demonstration)

---

## 5. Core Features

### 5.1 Project Management (Write Model)

#### Project Aggregate
**Events:**
- `ProjectInitiated` - A new project was established with goals and ownership
- `ProjectRebranded` - Project identity and name were changed
- `ProjectScopeRefined` - Project description and objectives were revised
- `ProjectCompleted` - Project work concluded and was archived
- `ProjectReactivated` - Previously completed project was reopened
- `MemberJoinedProject` - New team member was added to the project
- `MemberLeftProject` - Team member departed from the project

**Commands:**
- `CreateProject(name, description, ownerId)`
- `RenameProject(newName)`
- `UpdateDescription(newDescription)`
- `ArchiveProject(reason)`
- `RestoreProject()`
- `AddTeamMember(userId, role)`
- `RemoveTeamMember(userId)`

#### Task Aggregate
**Events:**
- `TaskPlanned` - New work item was planned for the project
- `ResponsibilityAssigned` - Task ownership was given to a team member
- `ResponsibilityRelinquished` - Task ownership was released from team member
- `WorkCommenced` - Team member began working on the task
- `WorkCompleted` - Task work was finished successfully
- `TaskRevived` - Previously completed task was reopened for more work
- `TaskReprioritized` - Task urgency level was adjusted (Low/Medium/High/Critical)
- `EffortReestimated` - Expected work effort was reassessed
- `RequirementsRefined` - Task scope and requirements were clarified
- `FeedbackProvided` - Comment or discussion was added to task
- `TaskRelocated` - Task was moved to a different project (demonstrates event evolution)
- `TaskRetagged` - Classification tags were modified
- `DeadlineEstablished` - Target completion date was set
- `DeadlineRemoved` - Previously set due date was cleared

**Commands:**
- `CreateTask(projectId, title, description, priority)`
- `AssignTask(userId)`
- `UnassignTask()`
- `StartTask()`
- `CompleteTask(completionNotes)`
- `ReopenTask(reason)`
- `ChangePriority(newPriority)`
- `UpdateEstimate(hours)`
- `UpdateDescription(newDescription)`
- `AddComment(text, authorId)`
- `MoveToProject(newProjectId)`
- `UpdateTags(tags[])`
- `SetDueDate(date)`
- `ClearDueDate()`

### 5.2 Projections (Read Models)

#### Active Tasks View (Simple Projection)
**Purpose:** Fast query of active tasks by project
**Updated by:** `TaskPlanned`, `WorkCompleted`, `ProjectCompleted`
**Schema:**
```json
{
  "projectId": "string",
  "taskId": "string",
  "title": "string",
  "assignedTo": "string?",
  "priority": "enum",
  "status": "enum",
  "createdAt": "datetime"
}
```

#### Task Activity Timeline (Audit Projection)
**Purpose:** Complete audit trail for compliance
**Updated by:** All task events
**Schema:**
```json
{
  "taskId": "string",
  "eventType": "string",
  "occurredAt": "datetime",
  "performedBy": "string",
  "changes": "object",
  "metadata": "object"
}
```

#### Project Dashboard (Complex Projection)
**Purpose:** Metrics and KPIs per project
**Updated by:** Task and project events
**Schema:**
```json
{
  "projectId": "string",
  "totalTasks": "int",
  "completedTasks": "int",
  "activeTasks": "int",
  "tasksPerPriority": "object",
  "tasksPerAssignee": "object",
  "averageCompletionTime": "timespan?",
  "lastActivityAt": "datetime"
}
```

#### User Work Queue (Personal View)
**Purpose:** Tasks assigned to specific user
**Updated by:** `ResponsibilityAssigned`, `ResponsibilityRelinquished`, `WorkCompleted`
**Schema:**
```json
{
  "userId": "string",
  "assignedTasks": [{
    "taskId": "string",
    "projectName": "string",
    "title": "string",
    "priority": "enum",
    "dueDate": "datetime?"
  }]
}
```

### 5.3 Event Sourcing Capabilities Showcase

#### Time Travel & Audit History
**Feature:** View any aggregate state at any point in time
**Demo Scenarios:**
- "Show me what this task looked like on October 15th"
- "Who changed the task priority and when?"
- "Replay the entire task lifecycle"
- "View project team composition at project start"

**UI Components:**
- Timeline slider to navigate history
- Event log viewer with expandable details
- State comparison (before/after)
- "Replay" button to animate state changes

#### Multiple Projections from Same Events
**Feature:** Different read models from single event stream
**Demo Scenarios:**
- Same task events feed 4 different projections
- Add new projection without changing write model
- Show projection checkpoints advancing
- Rebuild projection from event history

**UI Components:**
- Projection health dashboard
- Projection lag indicator (how far behind write model)
- Rebuild projection button
- Projection schema viewer

#### Event Upcasting (Schema Evolution)
**Feature:** Migrate old events to new schema without data loss
**Demo Scenarios:**
- Introduce `TaskRelocated` event (replaces old pattern)
- Upcast legacy `TaskProjectChanged` to new format
- Show old events still readable
- Demonstrate backward compatibility

**Code Examples:**
```csharp
public class TaskRelocatedUpcaster : IEventUpcaster
{
    public bool CanUpcast(IEvent @event)
        => @event.EventName == "Task.ProjectChanged";

    public IEnumerable<IEvent> UpCast(IEvent oldEvent)
    {
        var data = JsonSerializer.Deserialize<OldFormat>(oldEvent.Payload);
        yield return new TaskRelocated(
            data.NewProjectId,
            data.MovedBy,
            data.Reason ?? "Task relocated to different project");
    }
}
```

#### Snapshots for Performance
**Feature:** Optimize long event streams with snapshots
**Demo Scenarios:**
- Task with 1000+ events (many comments/updates)
- Show fold time without snapshot
- Create snapshot at event 500
- Show improved fold time with snapshot
- Automatic snapshot creation threshold

**Metrics Displayed:**
- Event count per aggregate
- Fold time (with/without snapshot)
- Snapshot age
- Snapshot size
- Automatic snapshot trigger threshold (e.g., every 100 events)

---

## 6. Technical Architecture

### 6.1 Architecture Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                      │
│  ┌────────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │  Angular SPA   │  │  ASP.NET API │  │  Azurite       │ │
│  │  (Nginx)       │  │  (Minimal)   │  │  (Emulator)    │ │
│  │  Port: 4200    │  │  Port: 5000  │  │  Port: 10000   │ │
│  └────────────────┘  └──────────────┘  └────────────────┘ │
└─────────────────────────────────────────────────────────────┘
           │                    │                    │
           │                    │                    │
           └──── HTTP ──────────┤                    │
                                 │                    │
                            SignalR Hub               │
                                 │                    │
                         ErikLieben.FA.ES            │
                                 │                    │
                         EventStream Layer           │
                                 │                    │
                         Storage Provider ───────────┘
```

### 6.2 Technology Stack

#### Backend
- **Framework:** .NET 9.0
- **API:** ASP.NET Core Minimal APIs
- **Event Sourcing:** ErikLieben.FA.ES 1.3.1+
- **Storage:** Azure Blob Storage (via Azurite emulator locally)
- **Real-time:** SignalR for push notifications
- **Hosting:** .NET Aspire orchestration

#### Frontend
- **Framework:** Angular 19+ (Standalone components)
- **UI Library:** Angular Material or PrimeNG
- **State Management:** NgRx Signal Store (CQRS-aligned)
- **Real-time:** SignalR TypeScript client
- **Charts:** Chart.js or ApexCharts for dashboards

#### DevOps
- **Orchestration:** .NET Aspire AppHost
- **Emulator:** Azurite (Azure Storage emulator)
- **Hot Reload:** Both Angular and ASP.NET support live reload
- **Package Manager:** npm/pnpm (frontend), NuGet (backend)

### 6.3 Project Structure
```
demo/
├── TaskFlow.sln
├── README.md
├── PRD.md (this file)
├── UserStories.md
├── ImplementationPlan.md
│
├── src/
│   ├── TaskFlow.Domain/              # Event sourcing domain model
│   │   ├── Aggregates/
│   │   │   ├── Project.cs
│   │   │   └── Task.cs
│   │   ├── Events/
│   │   │   ├── ProjectEvents.cs
│   │   │   └── TaskEvents.cs
│   │   ├── Projections/
│   │   │   ├── ActiveTasksProjection.cs
│   │   │   ├── TaskActivityProjection.cs
│   │   │   ├── ProjectDashboardProjection.cs
│   │   │   └── UserWorkQueueProjection.cs
│   │   ├── Upcasters/
│   │   │   └── TaskMovedToProjectUpcaster.cs
│   │   └── DomainExtensions.cs
│   │
│   ├── TaskFlow.Api/                 # ASP.NET Minimal API
│   │   ├── Program.cs
│   │   ├── Endpoints/
│   │   │   ├── ProjectEndpoints.cs
│   │   │   ├── TaskEndpoints.cs
│   │   │   ├── ProjectionEndpoints.cs
│   │   │   └── AdminEndpoints.cs
│   │   ├── Hubs/
│   │   │   └── TaskFlowHub.cs        # SignalR hub
│   │   └── Middleware/
│   │       └── ExceptionHandlingMiddleware.cs
│   │
│   ├── TaskFlow.AppHost/             # .NET Aspire orchestration
│   │   └── Program.cs
│   │
│   └── TaskFlow.ServiceDefaults/     # Aspire shared config
│       └── Extensions.cs
│
├── webapp/                           # Angular frontend
│   ├── angular.json
│   ├── package.json
│   ├── src/
│   │   ├── app/
│   │   │   ├── features/
│   │   │   │   ├── projects/
│   │   │   │   │   ├── project-list.component.ts
│   │   │   │   │   ├── project-detail.component.ts
│   │   │   │   │   └── project-form.component.ts
│   │   │   │   ├── tasks/
│   │   │   │   │   ├── task-list.component.ts
│   │   │   │   │   ├── task-detail.component.ts
│   │   │   │   │   ├── task-timeline.component.ts
│   │   │   │   │   └── task-form.component.ts
│   │   │   │   ├── projections/
│   │   │   │   │   ├── projection-dashboard.component.ts
│   │   │   │   │   └── projection-health.component.ts
│   │   │   │   └── admin/
│   │   │   │       ├── event-explorer.component.ts
│   │   │   │       ├── time-travel.component.ts
│   │   │   │       └── snapshot-manager.component.ts
│   │   │   ├── core/
│   │   │   │   ├── services/
│   │   │   │   │   ├── signalr.service.ts
│   │   │   │   │   ├── project.service.ts
│   │   │   │   │   └── task.service.ts
│   │   │   │   └── models/
│   │   │   │       ├── project.model.ts
│   │   │   │       ├── task.model.ts
│   │   │   │       └── event.model.ts
│   │   │   └── shared/
│   │   │       └── components/
│   │   │           ├── event-timeline.component.ts
│   │   │           └── state-diff-viewer.component.ts
│   │   └── assets/
│   └── public/
│
└── tests/
    ├── TaskFlow.Domain.Tests/        # Domain unit tests
    ├── TaskFlow.Api.Tests/           # API integration tests
    └── webapp-e2e/                   # Angular E2E tests
```

### 6.4 API Design

#### Command Endpoints (Write)
```
POST   /api/projects                    - Create project
PUT    /api/projects/{id}/name          - Rename project
PUT    /api/projects/{id}/description   - Update description
POST   /api/projects/{id}/archive       - Archive project
POST   /api/projects/{id}/restore       - Restore project
POST   /api/projects/{id}/team          - Add team member
DELETE /api/projects/{id}/team/{userId} - Remove team member

POST   /api/tasks                       - Create task
PUT    /api/tasks/{id}/assign           - Assign task
PUT    /api/tasks/{id}/unassign         - Unassign task
POST   /api/tasks/{id}/start            - Start task
POST   /api/tasks/{id}/complete         - Complete task
POST   /api/tasks/{id}/reopen           - Reopen task
PUT    /api/tasks/{id}/priority         - Change priority
PUT    /api/tasks/{id}/estimate         - Update estimate
POST   /api/tasks/{id}/comments         - Add comment
PUT    /api/tasks/{id}/project          - Move to project
PUT    /api/tasks/{id}/tags             - Update tags
```

#### Query Endpoints (Read)
```
GET    /api/projects                    - List all projects
GET    /api/projects/{id}               - Get project details
GET    /api/projects/{id}/dashboard     - Get project metrics

GET    /api/tasks                       - List all tasks (filterable)
GET    /api/tasks/{id}                  - Get task details
GET    /api/tasks/{id}/timeline         - Get task audit history
GET    /api/tasks/my-queue              - Get user's assigned tasks

GET    /api/projections                 - List all projections with health
GET    /api/projections/{name}/status   - Get projection checkpoint
POST   /api/projections/{name}/rebuild  - Rebuild projection
```

#### Admin/Demo Endpoints
```
GET    /api/admin/events/project/{id}   - Get raw event stream
GET    /api/admin/events/task/{id}      - Get raw event stream
GET    /api/admin/tasks/{id}/version/{v} - Time-travel: Get task at version
POST   /api/admin/tasks/{id}/snapshot   - Create snapshot
GET    /api/admin/snapshots             - List all snapshots
POST   /api/admin/demo/seed             - Seed demo data
POST   /api/admin/demo/chaos            - Generate chaos (many events)
```

#### SignalR Hub Events
```
Client → Server:
  - JoinProject(projectId)
  - LeaveProject(projectId)

Server → Client:
  - TaskPlanned(taskDto)
  - TaskChanged(taskDto)
  - WorkCompleted(taskDto)
  - ProjectionAdvanced(projectionName, checkpoint)
  - EventOccurred(aggregateId, eventType)
```

---

## 7. User Experience

### 7.1 Key User Flows

#### Happy Path: Create and Complete Task
1. User navigates to project detail page
2. Clicks "New Task" button
3. Fills form: title, description, priority
4. Task appears in list immediately
5. SignalR pushes update to other connected users
6. User assigns task to team member
7. Team member starts task
8. Team member marks task complete
9. Task moves to "Completed" section
10. All watchers notified in real-time

#### Power User: Time Travel Investigation
1. User opens task detail page
2. Clicks "View History" tab
3. Timeline shows all 47 events for this task
4. User drags timeline slider to 2 weeks ago
5. Task state rehydrates to that point in time
6. User sees who reassigned the task back then
7. User compares "before" vs "after" state
8. User exports audit report as JSON

#### Admin: Projection Management
1. Admin opens projection dashboard
2. Sees 4 projections with health indicators
3. One projection is 15 seconds behind (yellow)
4. Admin clicks projection name
5. Views detailed checkpoint information
6. Clicks "Rebuild" to reset projection
7. Progress bar shows rebuild status
8. Projection catches up to current state

### 7.2 UI/UX Principles
- **Clarity over complexity**: Event sourcing concepts explained inline
- **Progressive disclosure**: Basic features upfront, advanced in tabs
- **Live updates**: Real-time via SignalR without page refresh
- **Visual feedback**: Loading states, optimistic updates, toast notifications
- **Educational tooltips**: Help icons explain event sourcing concepts
- **Developer-friendly**: JSON viewers, raw event inspection, debug info

---

## 8. Event Sourcing Benefits Demonstrated

### 8.1 Complete Audit Trail
**Problem Solved:** "Who changed what and when?"
**Demo Shows:**
- Every task change recorded as immutable event
- Task timeline shows complete history
- Each event includes timestamp, user, metadata
- Perfect for compliance and debugging

### 8.2 Temporal Queries (Time Travel)
**Problem Solved:** "What did this look like last month?"
**Demo Shows:**
- Replay aggregate to any point in time
- Compare states across versions
- Reconstruct deleted or modified data
- Debug production issues by replaying

### 8.3 Multiple Projections (CQRS)
**Problem Solved:** "Same data, different views"
**Demo Shows:**
- Four read models from one event stream
- Each projection optimized for specific query
- Add new projection without touching write model
- Projections can be rebuilt from history

### 8.4 Event Schema Evolution
**Problem Solved:** "How do we change events safely?"
**Demo Shows:**
- Old events still readable after schema change
- Upcasters transform legacy events transparently
- No data migration required
- Backward compatibility maintained

---

## 9. Success Criteria

### Must Have
- ✅ All aggregates implement full event sourcing lifecycle
- ✅ At least 4 projections with different purposes
- ✅ Time-travel UI to navigate aggregate history
- ✅ Event upcasting example with working upcaster
- ✅ Snapshot creation and performance comparison
- ✅ Aspire orchestration with Azurite emulator
- ✅ SignalR real-time updates
- ✅ Clean, documented, production-quality code
- ✅ README with 5-minute quickstart
- ✅ Seed data for immediate exploration

### Should Have
- ⚠️ Unit tests for aggregates and projections
- ⚠️ Integration tests for API endpoints
- ⚠️ Performance benchmarks (with/without snapshots)
- ⚠️ Admin panel for projection management
- ⚠️ Export events as JSON
- ⚠️ Dark mode support

### Nice to Have
- 💡 E2E tests for critical flows
- 💡 Docker Compose alternative to Aspire
- 💡 Swagger/OpenAPI documentation
- 💡 GraphQL endpoint for queries
- 💡 Event visualization graph (event flow diagram)
- 💡 Multi-language support (i18n)

---

## 10. Out of Scope

### Explicitly NOT Included
- **Authentication/Authorization**: Demo uses mock users to focus on ES concepts
- **Production hosting**: Deployment guides for Azure/AWS
- **Advanced Azure features**: Cosmos DB, Table Storage providers
- **Saga/Process Managers**: Out of scope for basic demo
- **Event versioning strategies**: Beyond basic upcasting
- **Multi-tenancy**: Single tenant only
- **Email notifications**: SignalR only
- **File attachments**: Keep domain simple
- **Advanced search**: Basic filtering only

### Future Extensions (Post v1.0)
- WebJobs/Azure Functions integration example
- Azure Functions bindings demonstration
- Distributed tracing with Application Insights
- Event forwarding to external systems
- Read model caching strategies
- Blue-green deployment of projections

---

## 11. Risk & Mitigation

### Technical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Aspire learning curve for users | Medium | Medium | Provide both Aspire and Docker Compose options |
| SignalR connection issues | Low | Medium | Graceful degradation, polling fallback |
| Azurite differences from real Azure | Low | Low | Document known differences, provide Azure switch |
| Angular version compatibility | Low | Low | Use LTS version, document requirements |
| Complex setup process | High | Medium | Automated scripts, clear documentation |

### Non-Technical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Too complex for beginners | High | Medium | Progressive examples, good docs, video walkthrough |
| Too simple for architects | Medium | Low | Include advanced patterns, extensibility points |
| Maintenance burden | Medium | Medium | Comprehensive tests, clear contribution guide |
| Outdated dependencies | Low | High | Dependabot, regular updates, LTS versions |

---

## 12. Timeline & Milestones

### Phase 1: Foundation (Week 1-2)
- Project structure setup
- Domain model (aggregates, events)
- Basic API with Aspire orchestration
- Azurite integration

### Phase 2: Core Features (Week 2-3)
- All command endpoints
- Basic query endpoints
- Two projections (ActiveTasks, TaskActivity)
- Unit tests

### Phase 3: Frontend (Week 3-4)
- Angular app structure
- Project and task components
- SignalR integration
- Basic UI

### Phase 4: Advanced Features (Week 4-5)
- Time-travel UI
- Event upcasting example
- Snapshot management
- Admin panel

### Phase 5: Polish & Documentation (Week 5-6)
- Comprehensive README
- Code documentation
- Demo seed data
- Video walkthrough
- Integration tests

---

## 13. Open Questions

1. **Angular version**: Use Angular 19 (latest) or 18 LTS?
   - Recommendation: Angular 18 LTS for stability

2. **UI library**: Angular Material vs PrimeNG vs Tailwind + custom?
   - Recommendation: Angular Material for consistency

3. **State management**: NgRx Signal Store vs RxAngular vs simple signals?
   - Recommendation: NgRx Signal Store (CQRS-aligned)

4. **Demo data**: How many projects/tasks in seed data?
   - Recommendation: 3 projects, 30 tasks, varied event counts

5. **Video walkthrough**: Required for v1.0 or post-release?
   - Recommendation: Post-release, link in README

6. **Localization**: English only or multi-language from start?
   - Recommendation: English only, i18n structure ready

---

## 14. Appendix

### A. Event Naming Conventions
- Format: `{Aggregate}.{Action}` (e.g., `Task.Created`, `Project.Archived`)
- Past tense (fact already happened)
- Domain language, not technical terms
- Versioned: `Task.Moved.v2` for breaking changes

### B. Command/Query Separation
- Commands: Return `Task<Result<string>>` (aggregate ID or error)
- Queries: Return `Task<Result<TDto>>` (projection data)
- No business logic in query handlers
- Commands validate, queries just read

### C. Projection Checkpoint Format
```json
{
  "projectionName": "ActiveTasksProjection",
  "checkpoints": {
    "Project__proj-001__stream-001": "00000000000000000027",
    "Task__task-001__stream-001": "00000000000000000143",
    "Task__task-002__stream-001": "00000000000000000089"
  },
  "fingerprint": "sha256:abc123...",
  "lastUpdated": "2025-11-10T14:32:00Z"
}
```

### D. Useful Resources
- ErikLieben.FA.ES Docs: [GitHub README](https://github.com/eriklieben/fa-es)
- .NET Aspire Docs: https://learn.microsoft.com/en-us/dotnet/aspire/
- Event Sourcing Patterns: https://martinfowler.com/eaaDev/EventSourcing.html
- SignalR TypeScript: https://learn.microsoft.com/en-us/aspnet/core/signalr/typescript-client

---

**Document Status:** Ready for Review
**Next Steps:** Create User Stories document, then Implementation Plan
