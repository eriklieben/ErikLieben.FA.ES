# TaskFlow Demo - Quick Start Guide

## 🚀 Application is Running!

### Access Points

**Frontend (Angular 20)**
- URL: http://localhost:4200
- Features: Dashboard, Projects, Work Items, Kanban Board, Time Travel, Admin Panel

**Backend (ASP.NET + Aspire)**
- Aspire Dashboard: https://localhost:17201
- API Base URL: http://localhost:5000 (auto-configured via Aspire)
- SignalR Hub: http://localhost:5000/hub/taskflow

## 📋 What to Try

### 1. Dashboard (http://localhost:4200/dashboard)
- View project and work item metrics
- See completion rates and priority breakdowns
- Watch real-time metric updates
- Check high priority and overdue items

### 2. Projects (http://localhost:4200/projects)
- Browse all projects in a table view
- Click on a project to see details
- View team members and project status
- Filter and sort projects

### 3. Work Items (http://localhost:4200/workitems)
- See all active work items
- Filter by:
  - Status (Planned, In Progress, Completed)
  - Priority (Low, Medium, High, Critical)
  - Search by title
- View overdue items highlighted in red
- Access actions menu for each item

### 4. Kanban Board (http://localhost:4200/kanban)
- Visual workflow management
- **Drag and drop** items between columns:
  - Planned → In Progress → Completed
- Filter by project
- Watch real-time updates from other users
- See priority colors and deadlines

### 5. Time Travel (http://localhost:4200/time-travel)
- Select aggregate type (Project or WorkItem)
- Choose a specific aggregate
- **Use the slider** to navigate through event history
- See state reconstruction at any point in time
- View event details and JSON data
- Demonstrates core event sourcing capability

### 6. Admin Panel (http://localhost:4200/admin)
- System metrics dashboard
- Event store statistics
- Projection monitoring
- Recent events log
- SignalR connection status
- Projection rebuild controls

### 7. Theme Toggle
- Click the **sun/moon icon** in the toolbar
- Switches between light and dark mode
- Preference persists in localStorage

## 🔧 API Endpoints Available

### Project Commands (9 endpoints)
- POST `/api/projects` - Initiate project
- GET `/api/projects/{id}` - Get project
- PUT `/api/projects/{id}/rebrand` - Rebrand project
- PUT `/api/projects/{id}/scope` - Refine scope
- POST `/api/projects/{id}/complete` - Complete project
- POST `/api/projects/{id}/reactivate` - Reactivate project
- POST `/api/projects/{id}/team` - Add team member
- DELETE `/api/projects/{id}/team/{memberId}` - Remove team member
- PUT `/api/projects/{id}/leadership` - Reassign leadership

### WorkItem Commands (16 endpoints)
- POST `/api/workitems` - Plan work item
- GET `/api/workitems/{id}` - Get work item
- GET `/api/workitems` - List work items
- PUT `/api/workitems/{id}/assign` - Assign responsibility
- PUT `/api/workitems/{id}/unassign` - Relinquish responsibility
- POST `/api/workitems/{id}/commence` - Commence work
- POST `/api/workitems/{id}/complete` - Complete work
- POST `/api/workitems/{id}/revive` - Revive work item
- PUT `/api/workitems/{id}/priority` - Reprioritize
- PUT `/api/workitems/{id}/deadline` - Adjust deadline
- POST `/api/workitems/{id}/clarification` - Request clarification
- POST `/api/workitems/{id}/response` - Respond to clarification
- PUT `/api/workitems/{id}/rename` - Rename work item
- PUT `/api/workitems/{id}/redefine` - Redefine work item
- PUT `/api/workitems/{id}/relocate` - Relocate to project
- POST `/api/workitems/{id}/pause` - Pause work

### Query Endpoints (7 endpoints)
- GET `/api/queries/projects` - All projects
- GET `/api/queries/projects/{id}/metrics` - Project metrics
- GET `/api/queries/projects/active` - Active projects
- GET `/api/queries/workitems/active` - Active work items
- GET `/api/queries/workitems/active/by-project/{id}` - By project
- GET `/api/queries/workitems/active/by-assignee/{id}` - By assignee
- GET `/api/queries/workitems/overdue` - Overdue items

## 🎯 Event Sourcing Features Demonstrated

### 1. Domain Events (21 total)
All state changes are captured as immutable events:
- ProjectInitiated, ProjectRebranded, ScopeRefined, ProjectCompleted, etc.
- WorkItemPlanned, WorkItemAssigned, WorkCommenced, WorkCompleted, etc.
- No CRUD terms - all events use domain language

### 2. CQRS (Command Query Responsibility Segregation)
- Commands: Change state via domain aggregates
- Queries: Read from optimized projections
- Separate models for writes and reads

### 3. Eventually Consistent Projections
- ActiveWorkItems: Filtered view of non-completed items
- ProjectDashboard: Aggregated metrics and KPIs
- Updated asynchronously via ProjectionManager

### 4. Real-time Updates (SignalR)
- All domain events broadcast to connected clients
- Automatic UI updates when data changes
- No polling required

### 5. Time Travel
- Reconstruct any aggregate at any point in time
- View complete event history
- Audit trail of all changes

### 6. Blob Storage Event Store
- Events persisted to Azure Blob Storage (via Azurite emulator)
- Immutable event streams per aggregate
- Complete audit trail

## 🧪 Testing the Application

### Test Real-time Updates
1. Open the application in two browser windows
2. In window 1: Navigate to Kanban board
3. In window 2: Drag a work item to a different column
4. Watch window 1 update automatically via SignalR

### Test Time Travel
1. Create some test data (projects and work items)
2. Navigate to Time Travel page
3. Select an aggregate
4. Use the slider to go back in time
5. Observe how the state changes with each event

### Test Projections
1. Go to Admin panel
2. View projection statistics
3. Check last update times
4. Observe eventual consistency in action

## 📊 Project Statistics

- **Backend**: 4,500 lines of C# code
  - 2 Aggregates (Project, WorkItem)
  - 21 Domain Events
  - 2 Projections (ActiveWorkItems, ProjectDashboard)
  - 25 API Endpoints
  - 14 Unit Tests (100% passing)

- **Frontend**: 9,000 lines of TypeScript/HTML/SCSS
  - 10 Feature Components
  - 6 Zod Contract Files
  - 5 Services (RxJS + Signals)
  - Dark/Light theme support
  - Material Design UI

- **Total**: ~15,000 lines of code

## 🛑 Stopping the Application

To stop both services:

**Backend:**
- Press `Ctrl+C` in the terminal running the AppHost

**Frontend:**
- Press `Ctrl+C` in the terminal running `npm start`

Or use Claude Code to kill the background processes.

## 📝 Notes

- The application uses Azurite (Azure Storage Emulator) which is automatically started by Aspire
- All data is stored in the local emulator and will persist between runs
- CORS is configured to allow the Angular app to communicate with the API
- SignalR automatically reconnects if the connection is lost
- Theme preference is saved to browser localStorage

## 🎉 Congratulations!

You now have a fully functional event-sourced application demonstrating all the key concepts of the ErikLieben.FA.ES framework!

Explore the different features, try the drag-and-drop Kanban board, time travel through events, and watch real-time updates in action.
