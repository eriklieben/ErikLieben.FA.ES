import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/welcome',
    pathMatch: 'full'
  },
  {
    path: 'welcome',
    loadComponent: () => import('./features/welcome/welcome.component').then(m => m.WelcomeComponent)
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'projects',
    loadComponent: () => import('./features/projects/project-list.component').then(m => m.ProjectListComponent)
  },
  {
    path: 'projects/:id',
    loadComponent: () => import('./features/projects/project-detail.component').then(m => m.ProjectDetailComponent)
  },
  {
    path: 'workitems',
    loadComponent: () => import('./features/workitems/workitem-list-full.component').then(m => m.WorkItemListFullComponent)
  },
  {
    path: 'workitems/:id',
    loadComponent: () => import('./features/workitems/workitem-detail.component').then(m => m.WorkItemDetailComponent)
  },
  {
    path: 'kanban',
    loadComponent: () => import('./features/kanban/kanban-board-full.component').then(m => m.KanbanBoardFullComponent)
  },
  {
    path: 'demo-data',
    loadComponent: () => import('./features/demo-data/demo-data.component').then(m => m.DemoDataComponent)
  },
  {
    path: 'time-travel',
    loadComponent: () => import('./features/time-travel/time-travel-full.component').then(m => m.TimeTravelFullComponent)
  },
  {
    path: 'projections',
    loadComponent: () => import('./features/projections/projections.component').then(m => m.ProjectionsComponent)
  },
  {
    path: 'event-upcasting',
    loadComponent: () => import('./features/event-upcasting/event-upcasting.component').then(m => m.EventUpcastingComponent)
  },
  {
    path: 'stream-migration',
    loadComponent: () => import('./features/stream-migration/stream-migration.component').then(m => m.StreamMigrationComponent)
  },
  {
    path: 'benchmarks',
    loadComponent: () => import('./features/benchmarks/benchmarks.component').then(m => m.BenchmarksComponent)
  },
  {
    path: 'event-versioning',
    loadComponent: () => import('./features/event-versioning/event-versioning.component').then(m => m.EventVersioningComponent)
  },
  {
    path: 'usage/azure-functions',
    loadComponent: () => import('./features/functions-demo/functions-demo.component').then(m => m.FunctionsDemoComponent)
  },
  {
    path: 'usage/minimal-apis',
    loadComponent: () => import('./features/minimal-api/minimal-api.component').then(m => m.MinimalApiComponent)
  },
  {
    path: 'docs/getting-started',
    loadComponent: () => import('./features/docs/getting-started/getting-started.component').then(m => m.GettingStartedComponent)
  },
  {
    path: 'docs/aggregates',
    loadComponent: () => import('./features/docs/aggregates/aggregates.component').then(m => m.AggregatesComponent)
  },
  {
    path: 'docs/events',
    loadComponent: () => import('./features/docs/events/events.component').then(m => m.EventsComponent)
  },
  {
    path: 'docs/projections',
    loadComponent: () => import('./features/docs/projections/projections.component').then(m => m.ProjectionsDocsComponent)
  },
  {
    path: 'docs/cli',
    loadComponent: () => import('./features/docs/cli/cli.component').then(m => m.CliComponent)
  },
  {
    path: 'docs/testing',
    loadComponent: () => import('./features/docs/testing/testing.component').then(m => m.TestingComponent)
  },
  {
    path: 'docs/event-stream-management',
    loadComponent: () => import('./features/docs/event-stream-management/event-stream-management.component').then(m => m.EventStreamManagementComponent)
  },
  {
    path: 'docs/stream-actions',
    loadComponent: () => import('./features/docs/stream-actions/stream-actions.component').then(m => m.StreamActionsComponent)
  },
  {
    path: 'docs/notifications',
    loadComponent: () => import('./features/docs/notifications/notifications.component').then(m => m.NotificationsComponent)
  },
  {
    path: 'docs/concurrency',
    loadComponent: () => import('./features/docs/concurrency/concurrency.component').then(m => m.ConcurrencyComponent)
  },
  {
    path: 'docs/storage-providers',
    loadComponent: () => import('./features/docs/storage-providers/storage-providers.component').then(m => m.StorageProvidersComponent)
  },
  {
    path: 'docs/analyzers',
    loadComponent: () => import('./features/docs/analyzers/analyzers.component').then(m => m.AnalyzersComponent)
  },
  {
    path: 'docs/snapshots',
    loadComponent: () => import('./features/docs/snapshots/snapshots.component').then(m => m.SnapshotsComponent)
  },
  {
    path: 'docs/version-tokens',
    loadComponent: () => import('./features/docs/version-tokens/version-tokens.component').then(m => m.VersionTokensComponent)
  },
  {
    path: 'docs/routed-projections',
    loadComponent: () => import('./features/docs/routed-projections/routed-projections.component').then(m => m.RoutedProjectionsComponent)
  },
  {
    path: 'docs/stream-tags',
    loadComponent: () => import('./features/docs/stream-tags/stream-tags.component').then(m => m.StreamTagsComponent)
  },
  {
    path: 'docs/event-upcasting',
    loadComponent: () => import('./features/docs/event-upcasting/event-upcasting.component').then(m => m.EventUpcastingComponent)
  },
  {
    path: 'docs/azure-functions',
    loadComponent: () => import('./features/docs/azure-functions/azure-functions.component').then(m => m.AzureFunctionsComponent)
  },
  {
    path: 'docs/backup-restore',
    loadComponent: () => import('./features/docs/backup-restore/backup-restore.component').then(m => m.BackupRestoreComponent)
  },
  {
    path: 'docs/configuration',
    loadComponent: () => import('./features/docs/configuration/configuration.component').then(m => m.ConfigurationComponent)
  },
  {
    path: 'functions-demo',
    redirectTo: '/usage/azure-functions',
    pathMatch: 'full'
  },
  {
    path: 'users',
    loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent)
  },
  {
    path: 'epics',
    loadComponent: () => import('./features/epics/epic-list.component').then(m => m.EpicListComponent)
  },
  {
    path: 'epics/:id',
    loadComponent: () => import('./features/epics/epic-detail.component').then(m => m.EpicDetailComponent)
  },
  {
    path: 'sprints',
    loadComponent: () => import('./features/sprints/sprint-list.component').then(m => m.SprintListComponent)
  },
  {
    path: 'releases',
    loadComponent: () => import('./features/releases/release-list.component').then(m => m.ReleaseListComponent)
  },
  {
    path: 'connections',
    loadComponent: () => import('./features/admin/admin-full.component').then(m => m.AdminFullComponent)
  },
  {
    path: 'admin',
    redirectTo: '/connections',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/dashboard'
  }
];
