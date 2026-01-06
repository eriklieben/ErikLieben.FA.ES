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
  selector: 'app-configuration-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './configuration.component.html',
  styleUrl: './configuration.component.css'
})
export class ConfigurationComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'basic-setup', label: 'Basic Setup', icon: 'play_arrow' },
    { id: 'blob-settings', label: 'Blob Storage', icon: 'cloud' },
    { id: 'table-settings', label: 'Table Storage', icon: 'table_chart' },
    { id: 'cosmos-settings', label: 'Cosmos DB', icon: 'database' },
    { id: 'per-aggregate', label: 'Per-Aggregate', icon: 'tune' },
    { id: 'projections', label: 'Projections', icon: 'dashboard' },
    { id: 'multi-provider', label: 'Multi-Provider', icon: 'hub' },
    { id: 'performance', label: 'Performance', icon: 'speed' }
  ];

  activeSection = signal<string>('overview');

  basicSetupCodeHtml = signal<SafeHtml>('');
  defaultSettingsCodeHtml = signal<SafeHtml>('');
  blobSettingsCodeHtml = signal<SafeHtml>('');
  tableSettingsCodeHtml = signal<SafeHtml>('');
  cosmosSettingsCodeHtml = signal<SafeHtml>('');
  throughputCodeHtml = signal<SafeHtml>('');
  perAggregateCodeHtml = signal<SafeHtml>('');
  projectionCodeHtml = signal<SafeHtml>('');
  multiProviderCodeHtml = signal<SafeHtml>('');
  connectionStringsCodeHtml = signal<SafeHtml>('');
  performanceCodeHtml = signal<SafeHtml>('');

  private readonly basicSetupCode = `// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Register Azure clients
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(connectionString).WithName("Store");
});

// 2. Configure storage provider (choose one)
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Store"));
// OR
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Store"));
// OR
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());

// 3. Configure event store defaults
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// 4. Register your domain (generated)
builder.Services.ConfigureMyDomainFactory();`;

  private readonly defaultSettingsCode = `// Simple: same type for everything
services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Advanced: different types per component
services.ConfigureEventStore(new EventStreamDefaultTypeSettings(
    streamType: "blob",           // Event streams
    documentType: "blob",         // Object documents
    documentTagType: "table",     // Document tags
    eventStreamTagType: "table",  // Stream tags
    documentRefType: "blob"       // Document references
));`;

  private readonly blobSettingsCode = `services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    defaultDataStore: "Store",              // Required: Named BlobServiceClient
    defaultDocumentStore: null,             // Falls back to defaultDataStore
    defaultSnapShotStore: null,             // Falls back to defaultDataStore
    defaultDocumentTagStore: null,          // Falls back to defaultDataStore
    autoCreateContainer: true,              // Auto-create containers
    enableStreamChunks: false,              // Enable event chunking
    defaultChunkSize: 1000,                 // Events per chunk
    defaultDocumentContainerName: "object-document-store"
));`;

  private readonly tableSettingsCode = `services.ConfigureTableEventStore(new EventStreamTableSettings(
    defaultDataStore: "Store",
    autoCreateTable: true,
    enableStreamChunks: false,
    defaultChunkSize: 1000,
    defaultDocumentTableName: "objectdocumentstore",
    defaultEventTableName: "eventstream",
    defaultSnapshotTableName: "snapshots",
    defaultDocumentTagTableName: "documenttags",
    defaultStreamTagTableName: "streamtags",
    defaultStreamChunkTableName: "streamchunks",
    defaultDocumentSnapShotTableName: "documentsnapshots",
    defaultTerminatedStreamTableName: "terminatedstreams"
));`;

  private readonly cosmosSettingsCode = `services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    DatabaseName = "eventstore",
    DocumentsContainerName = "documents",
    EventsContainerName = "events",
    SnapshotsContainerName = "snapshots",
    TagsContainerName = "tags",
    ProjectionsContainerName = "projections",
    AutoCreateContainers = true,
    EnableBulkExecution = false,
    MaxBatchSize = 100,
    UseOptimisticConcurrency = true,
    DefaultTimeToLiveSeconds = -1,
    EventsThroughput = new ThroughputSettings
    {
        AutoscaleMaxThroughput = 4000
    },
    DatabaseThroughput = new ThroughputSettings
    {
        AutoscaleMaxThroughput = 4000
    }
});`;

  private readonly throughputCode = `// Autoscale (recommended)
new ThroughputSettings { AutoscaleMaxThroughput = 4000 }

// Manual throughput
new ThroughputSettings { ManualThroughput = 400 }

// Use shared database throughput
null  // Set container throughput to null`;

  private readonly perAggregateCode = `// Override storage type per aggregate
[Aggregate]
[EventStreamType("cosmosdb", "cosmosdb")]  // Stream and document types
public partial class Sprint : Aggregate { }

[Aggregate]
[EventStreamType("table", "table")]
public partial class Epic : Aggregate { }

[Aggregate]
[EventStreamBlobSettings("CustomConnection")]  // Custom blob connection
public partial class Order : Aggregate { }`;

  private readonly projectionCode = `// Blob storage
[BlobJsonProjection("projections", Connection = "BlobStorage")]
public partial class Dashboard : Projection { }

// Table storage (via generated code)
[TableProjection("projections")]
public partial class Dashboard : Projection { }

// Cosmos DB
[CosmosDbJsonProjection("projections", Connection = "cosmosdb")]
public partial class Dashboard : Projection { }

// Store checkpoint externally (separate file)
[BlobJsonProjection("projections")]
[ProjectionWithExternalCheckpoint]
public partial class Dashboard : Projection { }`;

  private readonly multiProviderCode = `// Configure all providers
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(blobConnection).WithName("Blob");
    clients.AddTableServiceClient(tableConnection).WithName("Table");
    clients.AddCosmosClient(cosmosConnection).WithName("Cosmos");
});

// Register all storage types
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("Blob"));
builder.Services.ConfigureTableEventStore(new EventStreamTableSettings("Table"));
builder.Services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings());

// Set default
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Then per-aggregate attributes override defaults:
// [EventStreamType("cosmosdb", "cosmosdb")] on specific aggregates`;

  private readonly connectionStringsCode = `// Azure Storage
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;EndpointSuffix=core.windows.net"
  }
}

// Cosmos DB
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://xxx.documents.azure.com:443/;AccountKey=xxx;"
  }
}

// Local Development (Azurite)
{
  "ConnectionStrings": {
    "Storage": "UseDevelopmentStorage=true"
  }
}`;

  private readonly performanceCode = `// High-Volume Writes: Enable bulk execution for Cosmos DB
services.ConfigureCosmosDbEventStore(new EventStreamCosmosDbSettings
{
    EnableBulkExecution = true,
    MaxBatchSize = 100,
    EventsThroughput = new ThroughputSettings
    {
        AutoscaleMaxThroughput = 10000
    }
});

// High-Volume Writes: Enable chunking for blob storage
services.ConfigureBlobEventStore(new EventStreamBlobSettings(
    "Store",
    enableStreamChunks: true,
    defaultChunkSize: 500));

// Read Optimization: Use Table Storage for fast point reads
services.ConfigureTableEventStore(new EventStreamTableSettings(
    "Store",
    autoCreateTable: true));`;

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
      basicSetup, defaultSettings, blobSettings, tableSettings,
      cosmosSettings, throughput, perAggregate, projection,
      multiProvider, connectionStrings, performance
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.basicSetupCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.defaultSettingsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.blobSettingsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tableSettingsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cosmosSettingsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.throughputCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.perAggregateCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.projectionCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.multiProviderCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.connectionStringsCode, { language: 'json' }),
      this.codeHighlighter.highlight(this.performanceCode, { language: 'csharp' })
    ]);

    this.basicSetupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basicSetup));
    this.defaultSettingsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(defaultSettings));
    this.blobSettingsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(blobSettings));
    this.tableSettingsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tableSettings));
    this.cosmosSettingsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cosmosSettings));
    this.throughputCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(throughput));
    this.perAggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(perAggregate));
    this.projectionCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(projection));
    this.multiProviderCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(multiProvider));
    this.connectionStringsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(connectionStrings));
    this.performanceCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(performance));
  }
}
