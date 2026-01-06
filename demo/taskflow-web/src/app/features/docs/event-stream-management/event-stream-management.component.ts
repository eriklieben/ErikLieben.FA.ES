import { Component, inject, signal, OnInit, effect, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CodeHighlighterService } from '../../../core/services/code-highlighter.service';
import { ThemeService } from '../../../core/services/theme.service';

interface NavItem {
  id: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-event-stream-management',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    RouterLink
  ],
  templateUrl: './event-stream-management.component.html',
  styleUrl: './event-stream-management.component.css'
})
export class EventStreamManagementComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'installation', label: 'Installation', icon: 'download' },
    { id: 'quickstart', label: 'Quick Start', icon: 'rocket_launch' },
    { id: 'architecture', label: 'Architecture', icon: 'architecture' },
    { id: 'livemigration', label: 'Live Migration', icon: 'sync_alt' },
    { id: 'transformation', label: 'Transformation', icon: 'transform' },
    { id: 'distributed', label: 'Distributed Coordination', icon: 'hub' },
    { id: 'aot', label: 'AOT Compatibility', icon: 'speed' }
  ];

  activeSection = signal<string>('overview');

  basicMigrationCodeHtml = signal<SafeHtml>('');
  transformationCodeHtml = signal<SafeHtml>('');
  backupCodeHtml = signal<SafeHtml>('');
  distributedCodeHtml = signal<SafeHtml>('');
  customTransformerCodeHtml = signal<SafeHtml>('');
  aotCodeHtml = signal<SafeHtml>('');
  liveMigrationCodeHtml = signal<SafeHtml>('');
  liveMigrationContextCodeHtml = signal<SafeHtml>('');
  liveMigrationProgressCodeHtml = signal<SafeHtml>('');

  private readonly basicMigrationCodeSample = `// Create migration service with Azure providers
var migrationService = new EventStreamMigrationService(
    new BlobLeaseDistributedLockProvider(blobServiceClient, loggerFactory),
    loggerFactory);

// Execute a simple migration
var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("object-123")
    .WithTargetStream("object-123-v2")
    .ExecuteAsync();

if (result.IsSuccess)
{
    Console.WriteLine($"Migration completed: {result.Statistics.TotalEvents} events");
}`;

  private readonly transformationCodeSample = `// Define a transformer for event upcasting
var transformer = new FunctionTransformer(
    canTransform: (name, version) => name == "OrderCreated" && version == 1,
    transform: async (evt, ct) =>
    {
        // Transform v1 OrderCreated to v2
        return new TransformedEvent(
            evt.EventType,
            version: 2,
            TransformPayload(evt.Payload)
        );
    });

var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("orders-123")
    .WithTargetStream("orders-123-v2")
    .WithTransformer(transformer)
    .ExecuteAsync();`;

  private readonly backupCodeSample = `var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("account-456")
    .WithTargetStream("account-456-v2")
    .WithBackup(backup => backup
        .WithProvider(new AzureBlobBackupProvider(blobServiceClient, logger))
        .IncludeObjectDocument()
        .WithCompression())
    .WithProgress(progress => progress
        .WithInterval(TimeSpan.FromSeconds(5))
        .OnProgress(p => Console.WriteLine($"Progress: {p.PercentComplete:F1}%"))
        .OnCompleted(p => Console.WriteLine("Migration completed!")))
    .ExecuteAsync();`;

  private readonly distributedCodeSample = `var result = await migrationService
    .ForDocument(objectDocument)
    .WithSourceStream("critical-789")
    .WithTargetStream("critical-789-v2")
    .WithDistributedLock(lock => lock
        .WithTimeout(TimeSpan.FromMinutes(30))
        .WithHeartbeatInterval(TimeSpan.FromSeconds(10)))
    .WithRollbackSupport()
    .ExecuteAsync();`;

  private readonly customTransformerCodeSample = `public class MyEventTransformer : IEventTransformer
{
    public bool CanTransform(string eventName, int version)
    {
        return eventName == "LegacyEvent" && version < 3;
    }

    public async Task<IEvent> TransformAsync(
        IEvent sourceEvent,
        CancellationToken ct)
    {
        // Your transformation logic here
        return new TransformedEvent(
            sourceEvent.EventType,
            version: 3,
            UpgradePayload(sourceEvent.Payload));
    }
}`;

  private readonly aotCodeSample = `[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OrderCreated))]
[JsonSerializable(typeof(OrderShipped))]
[JsonSerializable(typeof(OrderCancelled))]
public partial class MyEventJsonContext : JsonSerializerContext
{
}`;

  private readonly liveMigrationCodeSample = `// Create a live migration context
var context = new LiveMigrationContext
{
    MigrationId = Guid.NewGuid(),
    SourceDocument = sourceDocument,
    SourceStreamId = "orders-v1",
    TargetDocument = targetDocument,
    TargetStreamId = "orders-v2",
    DataStore = dataStore,
    DocumentStore = documentStore,
    Options = new LiveMigrationOptions()
};

// Execute the live migration
var executor = new LiveMigrationExecutor(context, loggerFactory);
var result = await executor.ExecuteAsync();

// Check the result
if (result.Success)
{
    Console.WriteLine($"Migration completed!");
    Console.WriteLine($"Total events copied: {result.TotalEventsCopied}");
    Console.WriteLine($"Iterations required: {result.Iterations}");
}`;

  private readonly liveMigrationContextCodeSample = `// Configure live migration with transformation
var options = new LiveMigrationOptions();

// Add progress callback
options.OnCatchUpProgress(progress =>
{
    Console.WriteLine($"Iteration {progress.Iteration}:");
    Console.WriteLine($"  Events copied: {progress.EventsCopied}");
    Console.WriteLine($"  Source version: {progress.SourceVersion}");
    Console.WriteLine($"  Target version: {progress.TargetVersion}");
});

// Add transformer for event upcasting
options.WithTransformer(new FunctionTransformer(
    canTransform: (name, version) => name == "OrderCreated" && version == 1,
    transform: async (evt, ct) => new TransformedEvent(
        evt.EventType,
        version: 2,
        UpgradePayload(evt.Payload)
    )
));

var context = new LiveMigrationContext
{
    Options = options,
    // ... other properties
};`;

  private readonly liveMigrationProgressCodeSample = `// The catch-up loop handles events arriving during migration
// Iteration 1: Copy all existing events (e.g., 100 events)
// Iteration 2: Copy events that arrived during iteration 1 (e.g., 5 events)
// Iteration 3: Copy events that arrived during iteration 2 (e.g., 1 event)
// Iteration 4: No new events - ready for cutover!

// Progress callback shows each iteration
options.OnCatchUpProgress(progress =>
{
    if (progress.EventsCopied == 0)
    {
        Console.WriteLine("Catch-up complete - ready for cutover!");
    }
    else
    {
        Console.WriteLine($"Caught up {progress.EventsCopied} events");
    }
});`;

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
    const [
      basicHtml,
      transformHtml,
      backupHtml,
      distributedHtml,
      customTransformerHtml,
      aotHtml,
      liveMigrationHtml,
      liveMigrationContextHtml,
      liveMigrationProgressHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.basicMigrationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.transformationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.backupCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.distributedCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.customTransformerCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.aotCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.liveMigrationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.liveMigrationContextCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.liveMigrationProgressCodeSample, { language: 'csharp' })
    ]);

    this.basicMigrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basicHtml));
    this.transformationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(transformHtml));
    this.backupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(backupHtml));
    this.distributedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(distributedHtml));
    this.customTransformerCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(customTransformerHtml));
    this.aotCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(aotHtml));
    this.liveMigrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(liveMigrationHtml));
    this.liveMigrationContextCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(liveMigrationContextHtml));
    this.liveMigrationProgressCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(liveMigrationProgressHtml));
  }
}
