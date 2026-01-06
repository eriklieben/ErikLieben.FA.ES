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
  selector: 'app-routed-projections-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './routed-projections.component.html',
  styleUrl: './routed-projections.component.css'
})
export class RoutedProjectionsComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'when-to-use', label: 'When to Use', icon: 'help' },
    { id: 'structure', label: 'Basic Structure', icon: 'account_tree' },
    { id: 'core-methods', label: 'Core Methods', icon: 'functions' },
    { id: 'patterns', label: 'Patterns', icon: 'pattern' },
    { id: 'api-integration', label: 'API Integration', icon: 'api' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  routerCodeHtml = signal<SafeHtml>('');
  destinationCodeHtml = signal<SafeHtml>('');
  addDestinationCodeHtml = signal<SafeHtml>('');
  routeToCodeHtml = signal<SafeHtml>('');
  queryCodeHtml = signal<SafeHtml>('');
  pagingPatternCodeHtml = signal<SafeHtml>('');
  tenantPatternCodeHtml = signal<SafeHtml>('');
  regionPatternCodeHtml = signal<SafeHtml>('');
  apiIntegrationCodeHtml = signal<SafeHtml>('');

  private readonly routerCode = `[BlobJsonProjection("projections/kanban.json")]
[ProjectionWithExternalCheckpoint]
public partial class ProjectKanbanBoard : RoutedProjection
{
    // Global state for routing decisions
    public Dictionary<string, ProjectInfo> Projects { get; } = new();
    private readonly Dictionary<string, string> workItemToProject = new();

    // Create destination when project is created
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(ProjectInitiated @event, string projectId)
    {
        Projects[projectId] = new ProjectInfo { Id = projectId, Name = @event.Name };

        // Create destination with metadata for path resolution
        AddDestination<ProjectKanbanDestination>(
            projectId,
            new Dictionary<string, string> { ["projectId"] = projectId });
    }

    // Route events to appropriate destination
    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        workItemToProject[workItemId] = @event.ProjectId;
        RouteToDestination(@event.ProjectId);
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemCompleted @event, string workItemId)
    {
        if (workItemToProject.TryGetValue(workItemId, out var projectId))
        {
            RouteToDestination(projectId);
        }
    }
}`;

  private readonly destinationCode = `[BlobJsonProjection("projections/kanban/project-{projectId}.json")]
public partial class ProjectKanbanDestination : Projection
{
    public Dictionary<string, KanbanWorkItem> WorkItems { get; } = new();

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemPlanned @event, string workItemId)
    {
        WorkItems[workItemId] = new KanbanWorkItem
        {
            Id = workItemId,
            Title = @event.Title,
            Status = WorkItemStatus.Planned
        };
    }

    [WhenParameterValueFactory<ObjectIdWhenValueFactory>]
    private void When(WorkItemCompleted @event, string workItemId)
    {
        if (WorkItems.TryGetValue(workItemId, out var item))
        {
            item.Status = WorkItemStatus.Completed;
        }
    }
}`;

  private readonly addDestinationCode = `// Simple destination
AddDestination<MyDestination>(destinationKey);

// With metadata for path resolution
AddDestination<MyDestination>(destinationKey, new Dictionary<string, string>
{
    ["projectId"] = projectId,
    ["region"] = region
});

// The metadata is used in path templates like:
// "projections/{projectId}/{region}.json"`;

  private readonly routeToCode = `// Route current event
RouteToDestination(destinationKey);

// Route a custom event
RouteToDestination(destinationKey, customEvent);

// Route with execution context
RouteToDestination(destinationKey, executionContext);

// Route to multiple destinations
RouteToDestinations("dest1", "dest2", "dest3");`;

  private readonly queryCode = `// Get all destination keys
var keys = projection.GetDestinationKeys();

foreach (var key in keys)
{
    Console.WriteLine($"Destination: {key}");
}

// Get specific destination
if (projection.TryGetDestination<ProjectKanbanDestination>(projectId, out var destination))
{
    var workItems = destination.WorkItems.Values;
    // Use destination data...
}

// Clear all destinations
projection.ClearDestinations();`;

  private readonly pagingPatternCode = `public partial class UserProfiles : RoutedProjection
{
    private const int UsersPerPage = 100;

    public int TotalUsers { get; set; }
    public int TotalPages { get; set; }

    private void When(UserCreated @event, string userId)
    {
        int pageNumber = (TotalUsers / UsersPerPage) + 1;
        var pageKey = $"page-{pageNumber}";

        AddDestination<UserProfilePage>(
            pageKey,
            new Dictionary<string, string> { ["pageNumber"] = pageNumber.ToString() });

        TotalUsers++;
        TotalPages = Math.Max(TotalPages, pageNumber);

        RouteToDestination(pageKey);
    }
}`;

  private readonly tenantPatternCode = `public partial class TenantData : RoutedProjection
{
    private readonly Dictionary<string, string> entityToTenant = new();

    private void When(TenantCreated @event, string tenantId)
    {
        AddDestination<TenantDataDestination>(
            tenantId,
            new Dictionary<string, string> { ["tenantId"] = tenantId });
    }

    private void When(EntityCreated @event, string entityId)
    {
        entityToTenant[entityId] = @event.TenantId;
        RouteToDestination(@event.TenantId);
    }
}`;

  private readonly regionPatternCode = `public partial class RegionalData : RoutedProjection
{
    private void When(OrderCreated @event, string orderId)
    {
        var region = DetermineRegion(@event.ShippingAddress);

        // Ensure destination exists
        if (!Registry.Destinations.ContainsKey(region))
        {
            AddDestination<RegionalOrdersDestination>(
                region,
                new Dictionary<string, string> { ["region"] = region });
        }

        RouteToDestination(region);
    }

    private static string DetermineRegion(Address address)
    {
        return address.Country switch
        {
            "US" or "CA" or "MX" => "north-america",
            "GB" or "DE" or "FR" => "europe",
            _ => "other"
        };
    }
}`;

  private readonly apiIntegrationCode = `app.MapGet("/projects/{projectId}/kanban", async (
    string projectId,
    [Projection] ProjectKanbanBoard kanban) =>
{
    if (kanban.TryGetDestination<ProjectKanbanDestination>(projectId, out var dest))
    {
        return Results.Ok(dest.WorkItems.Values);
    }
    return Results.NotFound();
});

app.MapGet("/kanban/all-projects", async (
    [Projection] ProjectKanbanBoard kanban) =>
{
    var allProjects = kanban.GetDestinationKeys()
        .Select(key =>
        {
            kanban.TryGetDestination<ProjectKanbanDestination>(key, out var dest);
            return new { ProjectId = key, WorkItemCount = dest?.WorkItems.Count ?? 0 };
        });

    return Results.Ok(allProjects);
});`;

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
      router, destination, addDest, routeTo, query,
      paging, tenant, region, api
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.routerCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.destinationCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.addDestinationCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.routeToCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.queryCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.pagingPatternCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tenantPatternCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.regionPatternCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.apiIntegrationCode, { language: 'csharp' })
    ]);

    this.routerCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(router));
    this.destinationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(destination));
    this.addDestinationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(addDest));
    this.routeToCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(routeTo));
    this.queryCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(query));
    this.pagingPatternCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(paging));
    this.tenantPatternCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tenant));
    this.regionPatternCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(region));
    this.apiIntegrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(api));
  }
}
