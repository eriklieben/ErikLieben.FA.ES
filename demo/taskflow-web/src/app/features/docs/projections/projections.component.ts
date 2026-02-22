import { Component, inject, signal, OnInit, effect, HostListener } from '@angular/core';
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
  selector: 'app-projections-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './projections.component.html',
  styleUrl: './projections.component.css'
})
export class ProjectionsDocsComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'types', label: 'Projection Types', icon: 'category' },
    { id: 'basic', label: 'Basic Projection', icon: 'view_list' },
    { id: 'routed', label: 'Routed Projection', icon: 'account_tree' },
    { id: 'when-methods', label: 'When Methods', icon: 'sync_alt' },
    { id: 'updating', label: 'Updating', icon: 'refresh' },
    { id: 'factory', label: 'Factories', icon: 'factory' },
    { id: 'checkpoints', label: 'Checkpoints', icon: 'bookmark' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' },
    { id: 'complete-example', label: 'Complete Example', icon: 'integration_instructions' }
  ];

  activeSection = signal<string>('overview');

  basicProjectionCodeHtml = signal<SafeHtml>('');
  routedProjectionCodeHtml = signal<SafeHtml>('');
  whenMethodsCodeHtml = signal<SafeHtml>('');
  syncUpdateCodeHtml = signal<SafeHtml>('');
  asyncUpdateCodeHtml = signal<SafeHtml>('');
  onDemandUpdateCodeHtml = signal<SafeHtml>('');
  factoryCodeHtml = signal<SafeHtml>('');
  checkpointCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');

  private readonly basicProjectionCode = `using ErikLieben.FA.ES.Projections;

// A projection for displaying active work items
public partial class ActiveWorkItems : Projection
{
    // State - the data we're building
    public List<WorkItemSummary> Items { get; set; } = new();
    public int TotalCount => Items.Count;

    // When methods update state based on events
    private void When(WorkItemCreated @event)
    {
        Items.Add(new WorkItemSummary
        {
            Id = @event.WorkItemId,
            Title = @event.Title,
            Status = "Open",
            CreatedAt = @event.CreatedAt
        });
    }

    private void When(WorkItemCompleted @event)
    {
        Items.RemoveAll(i => i.Id == @event.WorkItemId);
    }

    private void When(WorkItemTitleChanged @event)
    {
        var item = Items.FirstOrDefault(i => i.Id == @event.WorkItemId);
        if (item != null)
            item.Title = @event.NewTitle;
    }
}

public class WorkItemSummary
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}`;

  private readonly routedProjectionCode = `using ErikLieben.FA.ES.Projections;

// Routes orders to per-customer summary documents
[RoutedProjection("customer-{0}")]  // Blob name pattern
public partial class CustomerOrderSummary : RoutedProjection<OrderSummaryDocument>
{
    // When methods route events to destinations
    private void When(OrderCreated @event)
    {
        // Get or create destination for this customer
        var destination = GetOrAddDestination(@event.CustomerId);

        // Update the destination document
        RouteToDestination(destination, doc =>
        {
            doc.TotalOrders++;
            doc.Orders.Add(new OrderInfo
            {
                OrderId = @event.OrderId,
                CreatedAt = @event.CreatedAt
            });
        });
    }

    private void When(OrderShipped @event)
    {
        // Route to existing destination
        var destination = GetDestination(@event.CustomerId);
        if (destination != null)
        {
            RouteToDestination(destination, doc =>
            {
                var order = doc.Orders.FirstOrDefault(o => o.OrderId == @event.OrderId);
                if (order != null)
                    order.ShippedAt = @event.ShippedAt;
            });
        }
    }
}

public class OrderSummaryDocument
{
    public int TotalOrders { get; set; }
    public List<OrderInfo> Orders { get; set; } = new();
}`;

  private readonly whenMethodsCode = `// Standard When method with event parameter
private void When(OrderCreated @event)
{
    Orders.Add(new Order
    {
        Id = @event.OrderId,
        CustomerId = @event.CustomerId,
        Total = @event.InitialAmount
    });
}

// [When<T>] attribute when event data isn't needed
[When<OrderCancelled>]
private void OnOrderCancelled()
{
    // Just increment counter, don't need event details
    CancelledCount++;
}

// Multiple events can update the same state
private void When(OrderItemAdded @event)
{
    var order = Orders.FirstOrDefault(o => o.Id == @event.OrderId);
    if (order != null)
        order.Total += @event.Price * @event.Quantity;
}`;

  private readonly syncUpdateCode = `// Update after command
await workItem.Create(...);
var projection = await factory.GetAsync();
await projection.UpdateToLatestVersion();`;

  private readonly asyncUpdateCode = `// Background service
while (!cancellationToken.IsCancellationRequested)
{
    await projection.UpdateToLatestVersion();
    await Task.Delay(TimeSpan.FromSeconds(30));
}`;

  private readonly onDemandUpdateCode = `// Update on request
app.MapGet("/projections/active", async (
    IProjectionFactory<ActiveWorkItems> factory) =>
{
    var p = await factory.GetAsync();
    await p.UpdateToLatestVersion();
    return p;
});`;

  private readonly factoryCode = `// Inject the projection factory
public class DashboardService(
    IProjectionFactory<ActiveWorkItems> activeItemsFactory,
    IProjectionFactory<ProjectKanbanBoard> kanbanFactory)
{
    public async Task<DashboardViewModel> GetDashboard()
    {
        // Load projections
        var activeItems = await activeItemsFactory.GetAsync();
        var kanban = await kanbanFactory.GetAsync();

        // Optionally update to latest
        await Task.WhenAll(
            activeItems.UpdateToLatestVersion(),
            kanban.UpdateToLatestVersion());

        return new DashboardViewModel
        {
            ActiveCount = activeItems.TotalCount,
            Projects = kanban.Projects,
            Fingerprint = activeItems.CheckpointFingerprint
        };
    }
}`;

  private readonly checkpointCode = `// Checkpoint tracks position in each stream
public class Projection
{
    // Dictionary of ObjectIdentifier -> VersionIdentifier
    public Checkpoint Checkpoint { get; }

    // SHA-256 hash of checkpoint - use as ETag
    public string CheckpointFingerprint { get; }
}

// Use fingerprint for HTTP caching
app.MapGet("/dashboard", async (
    IProjectionFactory<Dashboard> factory,
    HttpContext context) =>
{
    var projection = await factory.GetAsync();

    // Check If-None-Match header
    var etag = context.Request.Headers.IfNoneMatch.FirstOrDefault();
    if (etag == projection.CheckpointFingerprint)
        return Results.StatusCode(304); // Not Modified

    // Return with ETag
    context.Response.Headers.ETag = projection.CheckpointFingerprint;
    return Results.Ok(projection);
});`;

  private readonly completeExampleCode = `// Basic projection for dashboard
public partial class DashboardStats : Projection
{
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ActiveCustomers { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();

    private void When(OrderCreated @event)
    {
        TotalOrders++;
        OrdersByStatus["Created"] = OrdersByStatus.GetValueOrDefault("Created") + 1;
    }

    private void When(OrderPaid @event)
    {
        TotalRevenue += @event.Amount;
        OrdersByStatus["Created"]--;
        OrdersByStatus["Paid"] = OrdersByStatus.GetValueOrDefault("Paid") + 1;
    }

    private void When(CustomerRegistered @event)
    {
        ActiveCustomers++;
    }
}

// Routed projection for per-customer views
[RoutedProjection("customer-orders-{0}")]
public partial class CustomerOrders : RoutedProjection<CustomerOrderHistory>
{
    private void When(OrderCreated @event)
    {
        var dest = GetOrAddDestination(@event.CustomerId);
        RouteToDestination(dest, doc =>
        {
            doc.Orders.Add(new OrderEntry
            {
                OrderId = @event.OrderId,
                CreatedAt = @event.CreatedAt,
                Status = "Created"
            });
        });
    }

    private void When(OrderShipped @event)
    {
        var dest = GetDestination(@event.CustomerId);
        if (dest == null) return;

        RouteToDestination(dest, doc =>
        {
            var order = doc.Orders.FirstOrDefault(o => o.OrderId == @event.OrderId);
            if (order != null)
            {
                order.Status = "Shipped";
                order.ShippedAt = @event.ShippedAt;
            }
        });
    }
}

public class CustomerOrderHistory
{
    public List<OrderEntry> Orders { get; set; } = new();
    public int TotalOrders => Orders.Count;
}

public class OrderEntry
{
    public string OrderId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string Status { get; set; } = "";
}`;

  constructor() {
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

    this.activeSection.set('overview');
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
    const [basic, routed, when, sync, async_, demand, factory, checkpoint, complete] = await Promise.all([
      this.codeHighlighter.highlight(this.basicProjectionCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.routedProjectionCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.whenMethodsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.syncUpdateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.asyncUpdateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.onDemandUpdateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.factoryCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.checkpointCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.completeExampleCode, { language: 'csharp' })
    ]);

    this.basicProjectionCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basic));
    this.routedProjectionCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(routed));
    this.whenMethodsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(when));
    this.syncUpdateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(sync));
    this.asyncUpdateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(async_));
    this.onDemandUpdateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(demand));
    this.factoryCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(factory));
    this.checkpointCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(checkpoint));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(complete));
  }
}
