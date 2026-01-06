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
  selector: 'app-snapshots-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './snapshots.component.html',
  styleUrl: './snapshots.component.css'
})
export class SnapshotsComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'when-to-use', label: 'When to Use', icon: 'help' },
    { id: 'creating', label: 'Creating Snapshots', icon: 'add_photo_alternate' },
    { id: 'loading', label: 'Loading', icon: 'download' },
    { id: 'storage', label: 'Storage Config', icon: 'storage' },
    { id: 'strategies', label: 'Strategies', icon: 'psychology' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  basicSnapshotCodeHtml = signal<SafeHtml>('');
  namedSnapshotCodeHtml = signal<SafeHtml>('');
  periodicSnapshotCodeHtml = signal<SafeHtml>('');
  loadingCodeHtml = signal<SafeHtml>('');
  retrieveCodeHtml = signal<SafeHtml>('');
  blobConfigCodeHtml = signal<SafeHtml>('');
  tableConfigCodeHtml = signal<SafeHtml>('');
  cosmosConfigCodeHtml = signal<SafeHtml>('');
  eventCountStrategyCodeHtml = signal<SafeHtml>('');
  timeBasedStrategyCodeHtml = signal<SafeHtml>('');
  milestoneStrategyCodeHtml = signal<SafeHtml>('');

  private readonly basicSnapshotCode = `[Aggregate]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    public int EventCount { get; private set; }

    public async Task Complete(string completedBy)
    {
        await Stream.Session(context =>
            Fold(context.Append(new OrderCompleted(completedBy, DateTime.UtcNow))));

        // Create snapshot if event count exceeds threshold
        if (EventCount > 100)
        {
            await Stream.Snapshot<Order>(Stream.CurrentVersion);
        }
    }

    private void When(OrderCompleted @event)
    {
        Status = OrderStatus.Completed;
        EventCount++;
    }
}`;

  private readonly namedSnapshotCode = `// Create a named snapshot for specific milestones
await Stream.Snapshot<Order>(Stream.CurrentVersion, "pre-migration");
await Stream.Snapshot<Order>(Stream.CurrentVersion, "end-of-month");

// Useful for:
// - Pre-migration checkpoints
// - End-of-period snapshots
// - Before major state changes`;

  private readonly periodicSnapshotCode = `public class SnapshotService
{
    private readonly IAggregateFactory<Order> _factory;
    private const int SnapshotThreshold = 500;

    public async Task CreateSnapshotIfNeeded(string orderId)
    {
        var order = await _factory.GetAsync(orderId);

        // Check if snapshot needed
        var lastSnapshotVersion = order.Stream.Document.Active.SnapShots
            .OrderByDescending(s => s.UntilVersion)
            .FirstOrDefault()?.UntilVersion ?? 0;

        var eventsSinceSnapshot = order.Stream.CurrentVersion - lastSnapshotVersion;

        if (eventsSinceSnapshot >= SnapshotThreshold)
        {
            await order.Stream.Snapshot<Order>(order.Stream.CurrentVersion);
        }
    }
}`;

  private readonly loadingCode = `// Snapshots are used automatically when loading aggregates
// The system:
// 1. Checks for the latest snapshot
// 2. Loads the snapshot state
// 3. Replays events after the snapshot version

var order = await orderFactory.GetAsync(orderId);
// If a snapshot exists at version 500 and current version is 550,
// only 50 events are replayed instead of 550`;

  private readonly retrieveCode = `// Get snapshot at specific version
var snapshot = await stream.GetSnapShot(version: 100);

// Get named snapshot
var preReleaseSnapshot = await stream.GetSnapShot(
    version: 500,
    name: "pre-release");

// Access snapshot metadata
var snapshots = order.Stream.Document.Active.SnapShots;
foreach (var snapshot in snapshots)
{
    Console.WriteLine($"Version: {snapshot.UntilVersion}, Name: {snapshot.Name}");
}`;

  private readonly blobConfigCode = `services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",
    defaultSnapShotStore: "Store"  // Can use separate connection
));`;

  private readonly tableConfigCode = `services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    defaultSnapShotStore: "Store",
    defaultSnapshotTableName: "snapshots"
));`;

  private readonly cosmosConfigCode = `services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    SnapshotsContainerName = "snapshots"
});`;

  private readonly eventCountStrategyCode = `private const int SnapshotInterval = 100;

public async Task ProcessEvent()
{
    // ... process event ...

    if (Stream.CurrentVersion % SnapshotInterval == 0)
    {
        await Stream.Snapshot<MyAggregate>(Stream.CurrentVersion);
    }
}`;

  private readonly timeBasedStrategyCode = `public async Task DailySnapshotJob()
{
    var aggregateIds = await GetActiveAggregateIds();

    foreach (var id in aggregateIds)
    {
        var aggregate = await factory.GetAsync(id);
        await aggregate.Stream.Snapshot<MyAggregate>(
            aggregate.Stream.CurrentVersion,
            $"daily-{DateTime.UtcNow:yyyy-MM-dd}");
    }
}`;

  private readonly milestoneStrategyCode = `public async Task CompletePhase(string phase)
{
    await Stream.Session(context =>
        Fold(context.Append(new PhaseCompleted(phase))));

    // Create milestone snapshot
    await Stream.Snapshot<Project>(Stream.CurrentVersion, $"phase-{phase}");
}`;

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
      basic, named, periodic, loading, retrieve,
      blobConfig, tableConfig, cosmosConfig,
      eventCount, timeBased, milestone
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.basicSnapshotCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.namedSnapshotCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.periodicSnapshotCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.loadingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.retrieveCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.blobConfigCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tableConfigCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cosmosConfigCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.eventCountStrategyCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.timeBasedStrategyCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.milestoneStrategyCode, { language: 'csharp' })
    ]);

    this.basicSnapshotCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basic));
    this.namedSnapshotCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(named));
    this.periodicSnapshotCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(periodic));
    this.loadingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(loading));
    this.retrieveCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(retrieve));
    this.blobConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(blobConfig));
    this.tableConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tableConfig));
    this.cosmosConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cosmosConfig));
    this.eventCountStrategyCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(eventCount));
    this.timeBasedStrategyCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(timeBased));
    this.milestoneStrategyCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(milestone));
  }
}
