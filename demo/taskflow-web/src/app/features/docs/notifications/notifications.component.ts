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
  selector: 'app-notifications',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css'
})
export class NotificationsComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'interfaces', label: 'Notification Types', icon: 'category' },
    { id: 'document-updated', label: 'Document Updated', icon: 'update' },
    { id: 'chunk-updated', label: 'Chunk Updated', icon: 'extension' },
    { id: 'chunk-closed', label: 'Chunk Closed', icon: 'lock' },
    { id: 'registration', label: 'Registration', icon: 'app_registration' },
    { id: 'usecases', label: 'Use Cases', icon: 'lightbulb' }
  ];

  activeSection = signal<string>('overview');

  documentUpdatedCodeHtml = signal<SafeHtml>('');
  chunkUpdatedCodeHtml = signal<SafeHtml>('');
  chunkClosedCodeHtml = signal<SafeHtml>('');
  registrationCodeHtml = signal<SafeHtml>('');
  cacheInvalidationCodeHtml = signal<SafeHtml>('');
  webhookCodeHtml = signal<SafeHtml>('');

  private readonly documentUpdatedCodeSample = `public class CacheInvalidationNotification : IStreamDocumentUpdatedNotification
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationNotification> _logger;

    public CacheInvalidationNotification(
        IDistributedCache cache,
        ILogger<CacheInvalidationNotification> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Action DocumentUpdated() => () =>
    {
        // Invalidate cache entries when the stream document changes
        _logger.LogInformation("Stream document updated, invalidating cache");
        _cache.Remove("stream-document-cache-key");
    };
}`;

  private readonly chunkUpdatedCodeSample = `public class ReplicationNotification : IStreamDocumentChunkUpdatedNotification
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ReplicationNotification> _logger;

    public ReplicationNotification(
        IEventBus eventBus,
        ILogger<ReplicationNotification> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public Action StreamDocumentChunkUpdated() => () =>
    {
        // Notify subscribers that new events are available
        _logger.LogInformation("Stream chunk updated, publishing notification");
        _eventBus.Publish(new StreamChunkUpdatedEvent());
    };
}`;

  private readonly chunkClosedCodeSample = `public class ArchiveNotification : IStreamDocumentChunkClosedNotification
{
    private readonly IArchiveService _archiveService;
    private readonly ILogger<ArchiveNotification> _logger;

    public ArchiveNotification(
        IArchiveService archiveService,
        ILogger<ArchiveNotification> logger)
    {
        _archiveService = archiveService;
        _logger = logger;
    }

    public Func<IEventStream, int, Task> StreamDocumentChunkClosed() =>
        async (eventStream, chunkIndex) =>
        {
            // Archive the closed chunk for long-term storage
            _logger.LogInformation(
                "Chunk {ChunkIndex} closed, archiving...", chunkIndex);

            await _archiveService.ArchiveChunkAsync(
                eventStream.ObjectId,
                chunkIndex);
        };
}`;

  private readonly registrationCodeSample = `// Register notifications via dependency injection
services.AddSingleton<INotification, CacheInvalidationNotification>();
services.AddSingleton<INotification, ReplicationNotification>();
services.AddSingleton<INotification, ArchiveNotification>();

// Or register specific notification types
services.AddSingleton<IStreamDocumentUpdatedNotification, CacheInvalidationNotification>();
services.AddSingleton<IStreamDocumentChunkUpdatedNotification, ReplicationNotification>();
services.AddSingleton<IStreamDocumentChunkClosedNotification, ArchiveNotification>();`;

  private readonly cacheInvalidationCodeSample = `public class ProjectionCacheNotification : IStreamDocumentChunkUpdatedNotification
{
    private readonly IProjectionCache _projectionCache;
    private readonly string _objectName;

    public ProjectionCacheNotification(
        IProjectionCache projectionCache,
        string objectName)
    {
        _projectionCache = projectionCache;
        _objectName = objectName;
    }

    public Action StreamDocumentChunkUpdated() => () =>
    {
        // Invalidate projection caches when source events change
        _projectionCache.InvalidateForObject(_objectName);
    };
}`;

  private readonly webhookCodeSample = `public class WebhookNotification : IStreamDocumentUpdatedNotification
{
    private readonly HttpClient _httpClient;
    private readonly WebhookSettings _settings;

    public WebhookNotification(
        HttpClient httpClient,
        IOptions<WebhookSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public Action DocumentUpdated() => async () =>
    {
        // Send webhook notification to external systems
        var payload = new { EventType = "StreamUpdated", Timestamp = DateTime.UtcNow };
        await _httpClient.PostAsJsonAsync(_settings.WebhookUrl, payload);
    };
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
    const [
      documentUpdatedHtml,
      chunkUpdatedHtml,
      chunkClosedHtml,
      registrationHtml,
      cacheInvalidationHtml,
      webhookHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.documentUpdatedCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.chunkUpdatedCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.chunkClosedCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.registrationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.cacheInvalidationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.webhookCodeSample, { language: 'csharp' })
    ]);

    this.documentUpdatedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(documentUpdatedHtml));
    this.chunkUpdatedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(chunkUpdatedHtml));
    this.chunkClosedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(chunkClosedHtml));
    this.registrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(registrationHtml));
    this.cacheInvalidationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cacheInvalidationHtml));
    this.webhookCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(webhookHtml));
  }
}
