import { Component, inject, signal, OnInit, OnDestroy, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CodeHighlighterService } from '../../../core/services/code-highlighter.service';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  id: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-azure-functions-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './azure-functions.component.html',
  styleUrl: './azure-functions.component.css'
})
export class AzureFunctionsComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'installation', label: 'Installation', icon: 'download' },
    { id: 'setup', label: 'Setup', icon: 'settings' },
    { id: 'event-stream-input', label: 'EventStreamInput', icon: 'input' },
    { id: 'projection-input', label: 'ProjectionInput', icon: 'visibility' },
    { id: 'projection-output', label: 'ProjectionOutput', icon: 'output' },
    { id: 'complete-example', label: 'Complete Example', icon: 'code' },
    { id: 'troubleshooting', label: 'Troubleshooting', icon: 'bug_report' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  installCodeHtml = signal<SafeHtml>('');
  setupCodeHtml = signal<SafeHtml>('');
  factoryRegCodeHtml = signal<SafeHtml>('');
  getAggregateCodeHtml = signal<SafeHtml>('');
  modifyAggregateCodeHtml = signal<SafeHtml>('');
  createAggregateCodeHtml = signal<SafeHtml>('');
  projectionInputCodeHtml = signal<SafeHtml>('');
  projectionOutputCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');
  troubleshootingCodeHtml = signal<SafeHtml>('');

  private readonly installCode = `dotnet add package ErikLieben.FA.ES.Azure.Functions.Worker.Extensions`;

  private readonly setupCode = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Azure Blob Storage
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(connectionString)
        .WithName("Store");
});

// Configure Event Store services
builder.Services.ConfigureBlobEventStore(
    new EventStreamBlobSettings("Store", autoCreateContainer: true));
builder.Services.ConfigureEventStore(
    new EventStreamDefaultTypeSettings("blob"));

// Configure Azure Functions bindings
builder.ConfigureEventStoreBindings();

// Register your domain factories
builder.Services.ConfigureMyDomainFactory();

await builder.Build().RunAsync();`;

  private readonly factoryRegCode = `// Register projection factories for [ProjectionInput] binding
builder.Services.AddSingleton<ProjectDashboardFactory>();

// As typed factory
builder.Services.AddSingleton<IProjectionFactory<ProjectDashboard>>(
    sp => sp.GetRequiredService<ProjectDashboardFactory>());

// As generic factory (required for binding resolution)
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<ProjectDashboardFactory>());`;

  private readonly getAggregateCode = `[Function("GetWorkItem")]
public async Task<HttpResponseData> GetWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get",
        Route = "workitems/{id}")] HttpRequestData req,
    string id,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    if (workItem.Metadata?.Id == null)
    {
        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
        await notFound.WriteAsJsonAsync(new { error = "Work item not found" });
        return notFound;
    }

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
        id = workItem.Metadata.Id.Value,
        title = workItem.Title,
        status = workItem.Status.ToString()
    });

    return response;
}`;

  private readonly modifyAggregateCode = `[Function("AssignWorkItem")]
public async Task<HttpResponseData> AssignWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post",
        Route = "workitems/{id}/assign")] HttpRequestData req,
    string id,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    var request = await req.ReadFromJsonAsync<AssignRequest>();

    // The aggregate is loaded - call domain methods
    await workItem.AssignResponsibility(request.MemberId);

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { success = true });
    return response;
}`;

  private readonly createAggregateCode = `[Function("CreateWorkItem")]
public async Task<HttpResponseData> CreateWorkItem(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post",
        Route = "workitems")] HttpRequestData req,
    [FromServices] IWorkItemFactory workItemFactory)
{
    var request = await req.ReadFromJsonAsync<CreateRequest>();
    var id = WorkItemId.New();

    var workItem = await workItemFactory.CreateAsync(id.Value.ToString());
    await workItem.Plan(request.Title, request.Description, request.ProjectId);

    var response = req.CreateResponse(HttpStatusCode.Created);
    response.Headers.Add("Location", $"/api/workitems/{id.Value}");
    await response.WriteAsJsonAsync(new { id = id.Value });
    return response;
}`;

  private readonly projectionInputCode = `[Function("GetKanbanBoard")]
public async Task<HttpResponseData> GetKanbanBoard(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get",
        Route = "projections/kanban")] HttpRequestData req,
    [ProjectionInput] ProjectKanbanBoard kanbanBoard)
{
    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
        projectCount = kanbanBoard.Projects?.Count ?? 0,
        projects = kanbanBoard.Projects?.Select(p => new
        {
            projectId = p.Key,
            projectName = p.Value.Name
        }),
        checkpointFingerprint = kanbanBoard.CheckpointFingerprint
    });

    return response;
}

// Works with non-HTTP triggers too
[Function("ProcessProjectionUpdate")]
public async Task ProcessProjectionUpdate(
    [QueueTrigger("projection-updates")] ProjectionUpdateMessage message,
    [ProjectionInput] ProjectKanbanBoard kanbanBoard)
{
    logger.LogInformation(
        "Processing update for {ProjectId}",
        message.ProjectId);
}`;

  private readonly projectionOutputCode = `[Function("RefreshProjections")]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectKanbanBoard>]
public async Task<HttpResponseData> RefreshProjections(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post",
        Route = "projections/refresh")] HttpRequestData req)
{
    // Projection updates happen in middleware after function returns

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new
    {
        success = true,
        message = "Projection refresh triggered",
        projections = new[] { nameof(ActiveWorkItems), nameof(ProjectKanbanBoard) }
    });

    return response;
}

// Combine with aggregate modification
[Function("CompleteWorkItem")]
[ProjectionOutput<ActiveWorkItems>]
[ProjectionOutput<ProjectKanbanBoard>]
public async Task<HttpResponseData> CompleteWorkItem(
    [HttpTrigger(...)] HttpRequestData req,
    string id,
    [EventStreamInput("{id}")] WorkItem workItem)
{
    await workItem.Complete("user-123");

    // All projections update after this returns successfully

    var response = req.CreateResponse(HttpStatusCode.OK);
    await response.WriteAsJsonAsync(new { success = true });
    return response;
}`;

  private readonly completeExampleCode = `public class WorkItemFunctions
{
    private readonly ILogger<WorkItemFunctions> _logger;

    public WorkItemFunctions(ILogger<WorkItemFunctions> logger)
    {
        _logger = logger;
    }

    [Function("GetWorkItem")]
    public async Task<HttpResponseData> GetWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "workitems/{id}")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        _logger.LogInformation("Getting work item {WorkItemId}", id);

        if (workItem.Metadata?.Id == null)
        {
            return await NotFound(req, "Work item not found");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            id = workItem.Metadata.Id.Value,
            title = workItem.Title,
            description = workItem.Description,
            status = workItem.Status.ToString(),
            priority = workItem.Priority.ToString(),
            assignedTo = workItem.AssignedTo
        });

        return response;
    }

    [Function("CompleteWorkItem")]
    [ProjectionOutput<ActiveWorkItems>]
    [ProjectionOutput<ProjectKanbanBoard>]
    public async Task<HttpResponseData> CompleteWorkItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post",
            Route = "workitems/{id}/complete")] HttpRequestData req,
        string id,
        [EventStreamInput("{id}")] WorkItem workItem)
    {
        _logger.LogInformation("Completing work item {WorkItemId}", id);

        if (workItem.Metadata?.Id == null)
        {
            return await NotFound(req, "Work item not found");
        }

        await workItem.Complete("user-123");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            message = "Work item completed"
        });
        return response;
    }

    private static async Task<HttpResponseData> NotFound(
        HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}`;

  private readonly troubleshootingCode = `// "No factory found for projection type"
// Solution: Register the projection factory
builder.Services.AddSingleton<YourProjectionFactory>();
builder.Services.AddSingleton<IProjectionFactory<YourProjection>>(
    sp => sp.GetRequiredService<YourProjectionFactory>());
builder.Services.AddSingleton<IProjectionFactory>(
    sp => sp.GetRequiredService<YourProjectionFactory>());

// "Cannot bind parameter"
// Solution: Ensure bindings are configured
builder.ConfigureEventStoreBindings();

// "Aggregate not found"
// Option 1: Handle null in function
if (workItem.Metadata?.Id == null)
{
    return NotFound(req, "Not found");
}

// Option 2: Create empty if not exists
[EventStreamInput("{id}", CreateEmptyObjectWhenNonExistent = true)]`;

  constructor() {
    effect(() => {
      this.themeService.theme();
      this.highlightCodeSamples();
    });
  }

  ngOnInit(): void {
    this.highlightCodeSamples();
    this.setupIntersectionObserver();
  }

  ngOnDestroy(): void {
    if (this.intersectionObserver) {
      this.intersectionObserver.disconnect();
    }
  }

  private setupIntersectionObserver(): void {
    this.intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          const sectionId = entry.target.id;
          if (entry.isIntersecting) {
            this.visibleSections.add(sectionId);
          } else {
            this.visibleSections.delete(sectionId);
          }
        });
        this.updateActiveSection();
      },
      { threshold: 0, rootMargin: '-80px 0px -50% 0px' }
    );

    this.navItems.forEach(item => {
      const element = document.getElementById(item.id);
      if (element) {
        this.intersectionObserver!.observe(element);
      }
    });
  }

  private updateActiveSection(): void {
    for (const item of this.navItems) {
      if (this.visibleSections.has(item.id)) {
        this.activeSection.set(item.id);
        return;
      }
    }
    if (this.visibleSections.size === 0) {
      this.activeSection.set('overview');
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
      install, setup, factoryReg, getAggregate, modifyAggregate,
      createAggregate, projectionInput, projectionOutput, complete, troubleshooting
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.installCode, { language: 'bash' }),
      this.codeHighlighter.highlight(this.setupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.factoryRegCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.getAggregateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.modifyAggregateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.createAggregateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionInputCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionOutputCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.completeExampleCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.troubleshootingCode, { language: 'csharp' })
    ]);

    this.installCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(install));
    this.setupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(setup));
    this.factoryRegCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(factoryReg));
    this.getAggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(getAggregate));
    this.modifyAggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(modifyAggregate));
    this.createAggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(createAggregate));
    this.projectionInputCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionInput));
    this.projectionOutputCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projectionOutput));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(complete));
    this.troubleshootingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(troubleshooting));
  }
}
