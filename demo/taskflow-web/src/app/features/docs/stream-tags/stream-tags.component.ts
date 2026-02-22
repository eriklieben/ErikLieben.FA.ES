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
  selector: 'app-stream-tags-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './stream-tags.component.html',
  styleUrl: './stream-tags.component.css'
})
export class StreamTagsComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'interface', label: 'Interface', icon: 'code' },
    { id: 'configuration', label: 'Configuration', icon: 'settings' },
    { id: 'patterns', label: 'Patterns', icon: 'pattern' },
    { id: 'querying', label: 'Querying', icon: 'search' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  interfaceCodeHtml = signal<SafeHtml>('');
  usageCodeHtml = signal<SafeHtml>('');
  blobConfigCodeHtml = signal<SafeHtml>('');
  tableConfigCodeHtml = signal<SafeHtml>('');
  statusTaggingCodeHtml = signal<SafeHtml>('');
  categoryTaggingCodeHtml = signal<SafeHtml>('');
  tenantTaggingCodeHtml = signal<SafeHtml>('');
  queryingCodeHtml = signal<SafeHtml>('');
  apiIntegrationCodeHtml = signal<SafeHtml>('');

  private readonly interfaceCode = `public interface IDocumentTagStore
{
    /// <summary>
    /// Associates the specified tag with the given document.
    /// </summary>
    Task SetAsync(IObjectDocument document, string tag);

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag.
    /// </summary>
    Task<IEnumerable<string>> GetAsync(string objectName, string tag);

    /// <summary>
    /// Removes the specified tag from the given document.
    /// </summary>
    Task RemoveAsync(IObjectDocument document, string tag);
}`;

  private readonly usageCode = `public class OrderService
{
    private readonly IDocumentTagStore _tagStore;
    private readonly IAggregateFactory<Order> _orderFactory;

    public async Task MarkOrderPriority(string orderId)
    {
        var order = await _orderFactory.GetAsync(orderId);

        // Add tag to the document
        await _tagStore.SetAsync(order.Stream.Document, "priority");
    }

    public async Task<IEnumerable<string>> GetPriorityOrders()
    {
        // Query all orders with the "priority" tag
        return await _tagStore.GetAsync("order", "priority");
    }

    public async Task ClearPriority(string orderId)
    {
        var order = await _orderFactory.GetAsync(orderId);

        // Remove tag from document
        await _tagStore.RemoveAsync(order.Stream.Document, "priority");
    }
}`;

  private readonly blobConfigCode = `// Automatic registration with ConfigureBlobEventStore
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",
    defaultDocumentTagStore: "Store"  // Can use separate connection
));`;

  private readonly tableConfigCode = `services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    defaultDocumentTagStore: "Store",
    defaultDocumentTagTableName: "documenttags",
    defaultStreamTagTableName: "streamtags"
));`;

  private readonly statusTaggingCode = `[Aggregate]
public partial class Order : Aggregate
{
    private readonly IDocumentTagStore _tagStore;

    public Order(IEventStream stream, IDocumentTagStore tagStore) : base(stream)
    {
        _tagStore = tagStore;
    }

    public async Task Ship(string trackingNumber)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderShipped(trackingNumber, DateTime.UtcNow))));

        // Update tags to reflect status
        await _tagStore.RemoveAsync(Stream.Document, "pending");
        await _tagStore.SetAsync(Stream.Document, "shipped");
    }

    public async Task Complete()
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCompleted(DateTime.UtcNow))));

        await _tagStore.RemoveAsync(Stream.Document, "shipped");
        await _tagStore.SetAsync(Stream.Document, "completed");
    }
}`;

  private readonly categoryTaggingCode = `public async Task CategorizeOrder(string orderId, string category)
{
    var order = await orderFactory.GetAsync(orderId);
    await _tagStore.SetAsync(order.Stream.Document, $"category:{category}");
}

public async Task<IEnumerable<string>> GetOrdersByCategory(string category)
{
    return await _tagStore.GetAsync("order", $"category:{category}");
}`;

  private readonly tenantTaggingCode = `public async Task TagForTenant(string orderId, string tenantId)
{
    var order = await orderFactory.GetAsync(orderId);
    await _tagStore.SetAsync(order.Stream.Document, $"tenant:{tenantId}");
}

public async Task<IEnumerable<string>> GetTenantOrders(string tenantId)
{
    return await _tagStore.GetAsync("order", $"tenant:{tenantId}");
}`;

  private readonly queryingCode = `// Single Tag Query
var activeOrders = await _tagStore.GetAsync("order", "status:active");

// Multiple Tags (AND)
var priorityActive = (await _tagStore.GetAsync("order", "priority:1"))
    .Intersect(await _tagStore.GetAsync("order", "status:active"));

// Multiple Tags (OR)
var priority1Or2 = (await _tagStore.GetAsync("order", "priority:1"))
    .Union(await _tagStore.GetAsync("order", "priority:2"));`;

  private readonly apiIntegrationCode = `app.MapGet("/orders/by-tag/{tag}", async (
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var orderIds = await tagStore.GetAsync("order", tag);
    var orders = new List<OrderSummary>();

    foreach (var id in orderIds)
    {
        var order = await orderFactory.GetAsync(id);
        orders.Add(new OrderSummary(order.Id, order.Status));
    }

    return Results.Ok(orders);
});

app.MapPost("/orders/{id}/tags/{tag}", async (
    string id,
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var order = await orderFactory.GetAsync(id);
    await tagStore.SetAsync(order.Stream.Document, tag);
    return Results.NoContent();
});

app.MapDelete("/orders/{id}/tags/{tag}", async (
    string id,
    string tag,
    IDocumentTagStore tagStore,
    IAggregateFactory<Order> orderFactory) =>
{
    var order = await orderFactory.GetAsync(id);
    await tagStore.RemoveAsync(order.Stream.Document, tag);
    return Results.NoContent();
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
      iface, usage, blobConfig, tableConfig,
      status, category, tenant, querying, api
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.interfaceCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.usageCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.blobConfigCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tableConfigCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.statusTaggingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.categoryTaggingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tenantTaggingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.queryingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.apiIntegrationCode, { language: 'csharp' })
    ]);

    this.interfaceCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(iface));
    this.usageCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(usage));
    this.blobConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(blobConfig));
    this.tableConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tableConfig));
    this.statusTaggingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(status));
    this.categoryTaggingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(category));
    this.tenantTaggingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tenant));
    this.queryingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(querying));
    this.apiIntegrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(api));
  }
}
