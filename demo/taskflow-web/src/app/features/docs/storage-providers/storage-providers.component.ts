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
  selector: 'app-storage-providers',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './storage-providers.component.html',
  styleUrl: './storage-providers.component.css'
})
export class StorageProvidersComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'blob-storage', label: 'Azure Blob Storage', icon: 'cloud' },
    { id: 'table-storage', label: 'Azure Table Storage', icon: 'table_chart' },
    { id: 'cosmosdb', label: 'Azure Cosmos DB', icon: 'public' }
  ];

  activeSection = signal<string>('overview');

  blobSetupCodeHtml = signal<SafeHtml>('');
  blobConfigCodeHtml = signal<SafeHtml>('');
  tableSetupCodeHtml = signal<SafeHtml>('');
  tableConfigCodeHtml = signal<SafeHtml>('');
  cosmosSetupCodeHtml = signal<SafeHtml>('');
  cosmosConfigCodeHtml = signal<SafeHtml>('');

  private readonly blobSetupCodeSample = `// Program.cs or Startup.cs
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Blob Storage event sourcing
builder.Services.AddEventSourcingWithBlobStorage(options =>
{
    options.ConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    options.ContainerName = "eventstreams";
});

// Register your aggregates
builder.Services.AddAggregate<Order, OrderState>();
builder.Services.AddAggregate<Customer, CustomerState>();`;

  private readonly blobConfigCodeSample = `// appsettings.json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "eventstreams",
    "ProjectionsContainer": "projections",
    "SnapshotsContainer": "snapshots"
  }
}

// Or using EventStreamBlobSettings
services.Configure<EventStreamBlobSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.ContainerName = "eventstreams";
    options.ChunkSize = 1000; // Events per chunk
    options.EnableCompression = true;
});`;

  private readonly tableSetupCodeSample = `// Program.cs or Startup.cs
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Table Storage event sourcing
builder.Services.AddEventSourcingWithTableStorage(options =>
{
    options.ConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
    options.TableName = "EventStreams";
});

// The factory creates event streams for your aggregates
builder.Services.AddSingleton<IEventStreamFactory, TableEventStreamFactory>();`;

  private readonly tableConfigCodeSample = `// appsettings.json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "TableName": "EventStreams",
    "ProjectionsTable": "Projections",
    "SnapshotsTable": "Snapshots"
  }
}

// Or using EventStreamTableSettings
services.Configure<EventStreamTableSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "EventStreams";
    options.BatchSize = 100; // Batch operations
});`;

  private readonly cosmosSetupCodeSample = `// Program.cs or Startup.cs
using ErikLieben.FA.ES.CosmosDb;

var builder = WebApplication.CreateBuilder(args);

// Add Cosmos DB event sourcing
builder.Services.AddEventSourcingWithCosmosDb(options =>
{
    options.ConnectionString = builder.Configuration["CosmosDb:ConnectionString"];
    options.DatabaseName = "EventStore";
    options.ContainerName = "Events";
});

// Configure the Cosmos client for performance
builder.Services.AddSingleton(sp =>
{
    var cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        },
        ConnectionMode = ConnectionMode.Direct
    });
    return cosmosClient;
});`;

  private readonly cosmosConfigCodeSample = `// appsettings.json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=https://...;AccountKey=...",
    "DatabaseName": "EventStore",
    "ContainerName": "Events",
    "PartitionKeyPath": "/objectId"
  }
}

// Or using EventStreamCosmosDbSettings
services.Configure<EventStreamCosmosDbSettings>(options =>
{
    options.ConnectionString = connectionString;
    options.DatabaseName = "EventStore";
    options.ContainerName = "Events";
    options.PartitionKeyPath = "/objectId";
    options.ThroughputMode = ThroughputMode.Autoscale;
    options.MaxAutoscaleThroughput = 4000;
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
      blobSetupHtml,
      blobConfigHtml,
      tableSetupHtml,
      tableConfigHtml,
      cosmosSetupHtml,
      cosmosConfigHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.blobSetupCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.blobConfigCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tableSetupCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.tableConfigCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cosmosSetupCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cosmosConfigCodeSample, { language: 'csharp' })
    ]);

    this.blobSetupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(blobSetupHtml));
    this.blobConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(blobConfigHtml));
    this.tableSetupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tableSetupHtml));
    this.tableConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(tableConfigHtml));
    this.cosmosSetupCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cosmosSetupHtml));
    this.cosmosConfigCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cosmosConfigHtml));
  }
}
