import { Component, inject, signal, OnInit, effect, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { FunctionsApiService } from '../../core/services/functions-api.service';
import { CodeHighlighterService } from '../../core/services/code-highlighter.service';
import { ThemeService } from '../../core/services/theme.service';
import {
  FunctionsWorkItemResponse,
  KanbanBoardResponse,
  ActiveWorkItemsResponse,
  UserProfilesResponse
} from '../../core/contracts/functions.contracts';

interface NavItem {
  id: string;
  label: string;
  icon: string;
}

interface ProjectionState {
  checkpoint: string | null;
  projectCount: number;
  timestamp: Date;
  eventsProcessed: number;
}

@Component({
  selector: 'app-functions-demo',
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatDividerModule,
    MatSnackBarModule
  ],
  templateUrl: './functions-demo.component.html',
  styleUrl: './functions-demo.component.css'
})
export class FunctionsDemoComponent implements OnInit {
  private readonly functionsApi = inject(FunctionsApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  // Navigation items
  readonly navItems: NavItem[] = [
    { id: 'installation', label: 'Installation', icon: 'download' },
    { id: 'setup', label: 'Setup', icon: 'settings' },
    { id: 'eventstream', label: 'EventStream Binding', icon: 'assignment' },
    { id: 'projection', label: 'Projection Binding', icon: 'view_quilt' },
    { id: 'output', label: 'Projection Output', icon: 'cloud_upload' },
    { id: 'complete', label: 'Complete Example', icon: 'integration_instructions' }
  ];

  // Active section tracking
  activeSection = signal<string>('installation');

  // Setup step hover state
  hoveredStep = signal<number | null>(null);
  hoveredEventStreamStep = signal<number | null>(null);
  hoveredProjectionStep = signal<number | null>(null);
  hoveredProjectionOutputStep = signal<number | null>(null);

  // Health check
  checking = signal(false);
  healthStatus = signal<boolean | null>(null);

  // Work Item
  workItemId = '';
  loadingWorkItem = signal(false);
  workItemResult = signal<FunctionsWorkItemResponse | null>(null);
  workItemError = signal<string | null>(null);

  // Projections
  loadingKanban = signal(false);
  loadingActiveItems = signal(false);
  loadingUserProfiles = signal(false);
  kanbanResult = signal<KanbanBoardResponse | null>(null);
  activeItemsResult = signal<ActiveWorkItemsResponse | null>(null);
  userProfilesResult = signal<UserProfilesResponse | null>(null);

  // Projection Output Demo
  refreshingProjections = signal(false);
  projectionBeforeState = signal<ProjectionState | null>(null);
  projectionAfterState = signal<ProjectionState | null>(null);

  // Highlighted code HTML
  installCodeHtml = signal<SafeHtml>('');
  setupCodeHtml = signal<SafeHtml>('');
  eventStreamCodeHtml = signal<SafeHtml>('');
  projectionCodeHtml = signal<SafeHtml>('');
  projectionOutputCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');

  // Code samples
  private readonly installCodeSample = `# Add the Azure Functions Worker Extensions package
dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions

# Also need the core package and a storage provider
dotnet add package ErikLieben.FA.ES
dotnet add package ErikLieben.FA.ES.AzureStorage`;

  private readonly setupCodeSample = `var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Step 1: Setup a storage client with a name
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(
                context.Configuration.GetConnectionString("Storage"))
                .WithName("EventStore");
        });

        // Step 2: Configure the Blob Event Store
        services.ConfigureBlobEventStore(new EventStreamBlobSettings("EventStore"));

        // Step 3: Set the default storage provider
        services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

        // Step 4: Register generated code from aggregates, projections, etc.
        services.ConfigureTaskFlowDomainFactory();
    })
    .Build();

host.Run();`;

  private readonly eventStreamCodeSample = `[Function(nameof(UpdateWorkItemStatus))]
public async Task<HttpResponseData> UpdateWorkItemStatus(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workitems/{id}/status")] HttpRequestData req,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    var command = await req.ReadFromJsonAsync<UpdateStatusCommand>();

    // Validate the command
    if (command is null || string.IsNullOrEmpty(command.NewStatus))
    {
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    // Execute command on the aggregate (generates events and saves)
    await workItem.UpdateStatus(command.NewStatus);

    // Return the updated aggregate state
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new {
        id = workItem.Metadata?.Id?.Value.ToString(),
        title = workItem.Title,
        status = workItem.Status.ToString()
    });
    return response;
}`;

  private readonly projectionCodeSample = `[Function(nameof(GetKanbanBoard))]
public async Task<HttpResponseData> GetKanbanBoard(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projections/kanban")] HttpRequestData req,
    [ProjectionInput] ProjectKanbanBoard kanbanBoard)
{
    // kanbanBoard is automatically loaded from blob storage
    // via IProjectionFactory<ProjectKanbanBoard>

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new {
        projectCount = kanbanBoard.Projects?.Count ?? 0,
        checkpointFingerprint = kanbanBoard.CheckpointFingerprint
    });
    return response;
}`;

  private readonly projectionOutputCodeSample = `// Timer-triggered: scheduled projection updates
[Function(nameof(DailyProjectionUpdate))]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectKanbanBoard>]
public async Task DailyProjectionUpdate(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timer,  // Runs daily at 2:00 AM
    ILogger logger)
{
    logger.LogInformation("Daily projection update started at {Time}", DateTime.UtcNow);
}

// HTTP-triggered: on-demand projection updates
[Function(nameof(RefreshProjections))]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectKanbanBoard>]
public async Task<HttpResponseData> RefreshProjections(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "projections/refresh")] HttpRequestData req,
    ILogger logger)
{
    logger.LogInformation("Manual projection refresh triggered");

    // The function just needs to complete successfully
    // The middleware handles the actual projection updates
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { success = true, message = "Projection refresh triggered" });
    return response;
}`;

  private readonly completeExampleCodeSample = `using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

public class WorkItemFunctions
{
    // GET: Load aggregate using [EventStreamInput]
    [Function(nameof(GetWorkItem))]
    public async Task<HttpResponseData> GetWorkItem(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workitems/{id}")] HttpRequestData req,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { workItem.Title, workItem.Status });
        return response;
    }

    // GET: Load projection using [ProjectionInput]
    [Function(nameof(GetActiveItems))]
    public async Task<HttpResponseData> GetActiveItems(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workitems/active")] HttpRequestData req,
        [ProjectionInput] ActiveWorkItems projection)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(projection.Items);
        return response;
    }

    // POST: Update projections using [ProjectionOutput]
    [Function(nameof(RefreshProjections))]
    [ProjectionOutput<ActiveWorkItems>]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> RefreshProjections(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "projections/refresh")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true });
        return response;
    }
}`;

  constructor() {
    // Re-highlight code when theme changes
    effect(() => {
      this.themeService.theme();
      this.highlightCodeSamples();
    });
  }

  ngOnInit(): void {
    this.highlightCodeSamples();
    this.updateActiveSection();
  }

  @HostListener('window:scroll')
  onScroll(): void {
    this.updateActiveSection();
  }

  private updateActiveSection(): void {
    const sections = this.navItems.map(item => ({
      id: item.id,
      element: document.getElementById(item.id)
    }));

    const scrollPosition = window.scrollY + 100;

    for (let i = sections.length - 1; i >= 0; i--) {
      const section = sections[i];
      if (section.element && section.element.offsetTop <= scrollPosition) {
        this.activeSection.set(section.id);
        return;
      }
    }

    this.activeSection.set('installation');
  }

  onCodeHover(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const line = target.closest('.step-line');
    if (line) {
      if (line.classList.contains('step-1')) this.hoveredStep.set(1);
      else if (line.classList.contains('step-2')) this.hoveredStep.set(2);
      else if (line.classList.contains('step-3')) this.hoveredStep.set(3);
      else if (line.classList.contains('step-4')) this.hoveredStep.set(4);
    } else {
      this.hoveredStep.set(null);
    }
  }

  onCodeLeave(): void {
    this.hoveredStep.set(null);
  }

  onEventStreamCodeHover(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const line = target.closest('.step-line');
    if (line) {
      if (line.classList.contains('step-1')) this.hoveredEventStreamStep.set(1);
      else if (line.classList.contains('step-2')) this.hoveredEventStreamStep.set(2);
      else if (line.classList.contains('step-3')) this.hoveredEventStreamStep.set(3);
    } else {
      this.hoveredEventStreamStep.set(null);
    }
  }

  onProjectionCodeHover(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const line = target.closest('.step-line');
    if (line) {
      if (line.classList.contains('step-1')) this.hoveredProjectionStep.set(1);
      else if (line.classList.contains('step-2')) this.hoveredProjectionStep.set(2);
      else if (line.classList.contains('step-3')) this.hoveredProjectionStep.set(3);
    } else {
      this.hoveredProjectionStep.set(null);
    }
  }

  onProjectionOutputCodeHover(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const line = target.closest('.step-line');
    if (line) {
      if (line.classList.contains('step-1')) this.hoveredProjectionOutputStep.set(1);
      else if (line.classList.contains('step-2')) this.hoveredProjectionOutputStep.set(2);
      else if (line.classList.contains('step-3')) this.hoveredProjectionOutputStep.set(3);
    } else {
      this.hoveredProjectionOutputStep.set(null);
    }
  }

  scrollToSection(event: Event, sectionId: string): void {
    event.preventDefault();
    const element = document.getElementById(sectionId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.activeSection.set(sectionId);
    }
  }

  private async highlightCodeSamples(): Promise<void> {
    const [
      installHtml,
      setupHtml,
      eventStreamHtml,
      projectionHtml,
      projectionOutputHtml,
      completeExampleHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.installCodeSample, { language: 'bash' }),
      this.codeHighlighter.highlight(this.setupCodeSample, {
        language: 'csharp',
        stepHighlights: [
          { lines: [5, 6, 7, 8, 9, 10, 11], step: 1 },
          { lines: [13, 14], step: 2 },
          { lines: [16, 17], step: 3 },
          { lines: [19, 20], step: 4 }
        ]
      }),
      this.codeHighlighter.highlight(this.eventStreamCodeSample, {
        language: 'csharp',
        stepHighlights: [
          { lines: [3, 4], step: 1 },
          { lines: [14, 15], step: 2 },
          { lines: [17, 18, 19, 20, 21, 22, 23], step: 3 }
        ]
      }),
      this.codeHighlighter.highlight(this.projectionCodeSample, {
        language: 'csharp',
        stepHighlights: [
          { lines: [3, 4], step: 1 },
          { lines: [6, 7, 11, 12], step: 2 },
          { lines: [9, 10, 13, 14], step: 3 }
        ]
      }),
      this.codeHighlighter.highlight(this.projectionOutputCodeSample, {
        language: 'csharp',
        stepHighlights: [
          { lines: [3, 4, 14, 15], step: 1 },
          { lines: [5, 6, 7, 16, 17, 18], step: 2 },
          { lines: [22, 23], step: 3 }
        ]
      }),
      this.codeHighlighter.highlight(this.completeExampleCodeSample, {
        language: 'csharp',
        highlightLines: [12, 22, 32, 33]
      })
    ]);

    this.installCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(installHtml));
    this.setupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(setupHtml));
    this.eventStreamCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(eventStreamHtml));
    this.projectionCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionHtml));
    this.projectionOutputCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionOutputHtml));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(completeExampleHtml));
  }

  checkHealth(): void {
    this.checking.set(true);
    this.healthStatus.set(null);

    this.functionsApi.checkHealth().subscribe({
      next: (healthy) => {
        this.healthStatus.set(healthy);
        this.checking.set(false);
        this.snackBar.open(
          healthy ? 'Azure Functions is available!' : 'Azure Functions is not available',
          'Close',
          { duration: 3000 }
        );
      },
      error: () => {
        this.healthStatus.set(false);
        this.checking.set(false);
      }
    });
  }

  getWorkItem(): void {
    if (!this.workItemId.trim()) {
      this.snackBar.open('Please enter a work item ID', 'Close', { duration: 2000 });
      return;
    }

    this.loadingWorkItem.set(true);
    this.workItemResult.set(null);
    this.workItemError.set(null);

    this.functionsApi.getWorkItem(this.workItemId).subscribe({
      next: (result) => {
        this.loadingWorkItem.set(false);
        if (result) {
          this.workItemResult.set(result);
        } else {
          this.workItemError.set('Work item not found or error occurred');
        }
      },
      error: (err) => {
        this.loadingWorkItem.set(false);
        this.workItemError.set(err.message || 'An error occurred');
      }
    });
  }

  getKanbanBoard(): void {
    this.loadingKanban.set(true);
    this.kanbanResult.set(null);

    this.functionsApi.getKanbanBoard().subscribe({
      next: (result) => {
        this.loadingKanban.set(false);
        this.kanbanResult.set(result);
        if (!result) {
          this.snackBar.open('Failed to load Kanban board', 'Close', { duration: 2000 });
        }
      }
    });
  }

  getActiveWorkItems(): void {
    this.loadingActiveItems.set(true);
    this.activeItemsResult.set(null);

    this.functionsApi.getActiveWorkItems().subscribe({
      next: (result) => {
        this.loadingActiveItems.set(false);
        this.activeItemsResult.set(result);
        if (!result) {
          this.snackBar.open('Failed to load active work items', 'Close', { duration: 2000 });
        }
      }
    });
  }

  getUserProfiles(): void {
    this.loadingUserProfiles.set(true);
    this.userProfilesResult.set(null);

    this.functionsApi.getUserProfiles().subscribe({
      next: (result) => {
        this.loadingUserProfiles.set(false);
        this.userProfilesResult.set(result);
        if (!result) {
          this.snackBar.open('Failed to load user profiles', 'Close', { duration: 2000 });
        }
      }
    });
  }

  refreshProjections(): void {
    this.refreshingProjections.set(true);
    this.projectionAfterState.set(null);

    // First, capture the current state (before)
    this.functionsApi.getKanbanBoard().subscribe({
      next: (beforeResult) => {
        if (beforeResult) {
          this.projectionBeforeState.set({
            checkpoint: beforeResult.checkpointFingerprint?.substring(0, 16) + '...' || null,
            projectCount: beforeResult.projectCount ?? 0,
            timestamp: new Date(),
            eventsProcessed: 0
          });
        }

        // Then trigger the refresh
        this.functionsApi.refreshProjections().subscribe({
          next: () => {
            // After refresh, get the updated state
            this.functionsApi.getKanbanBoard().subscribe({
              next: (afterResult) => {
                this.refreshingProjections.set(false);

                if (afterResult) {
                  const beforeCheckpoint = beforeResult?.checkpointFingerprint;
                  const afterCheckpoint = afterResult.checkpointFingerprint;
                  const checksumChanged = beforeCheckpoint !== afterCheckpoint;

                  this.projectionAfterState.set({
                    checkpoint: afterResult.checkpointFingerprint?.substring(0, 16) + '...' || null,
                    projectCount: afterResult.projectCount ?? 0,
                    timestamp: new Date(),
                    eventsProcessed: checksumChanged ? 1 : 0
                  });

                  this.snackBar.open(
                    checksumChanged ? 'Projections updated with new events!' : 'Projections already up to date',
                    'Close',
                    { duration: 3000 }
                  );
                }
              },
              error: () => {
                this.refreshingProjections.set(false);
                this.snackBar.open('Failed to get updated projection state', 'Close', { duration: 2000 });
              }
            });
          },
          error: () => {
            this.refreshingProjections.set(false);
            this.snackBar.open('Failed to refresh projections', 'Close', { duration: 2000 });
          }
        });
      },
      error: () => {
        this.refreshingProjections.set(false);
        this.snackBar.open('Failed to get current projection state', 'Close', { duration: 2000 });
      }
    });
  }
}
