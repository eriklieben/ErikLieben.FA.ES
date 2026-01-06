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
  selector: 'app-version-tokens-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './version-tokens.component.html',
  styleUrl: './version-tokens.component.css'
})
export class VersionTokensComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'creating', label: 'Creating Tokens', icon: 'add_circle' },
    { id: 'properties', label: 'Properties', icon: 'list' },
    { id: 'use-cases', label: 'Use Cases', icon: 'lightbulb' },
    { id: 'concurrency', label: 'Concurrency', icon: 'sync' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  createFromEventCodeHtml = signal<SafeHtml>('');
  createFromPartsCodeHtml = signal<SafeHtml>('');
  optimisticConcurrencyCodeHtml = signal<SafeHtml>('');
  checkpointCodeHtml = signal<SafeHtml>('');
  correlationCodeHtml = signal<SafeHtml>('');
  apiResponseCodeHtml = signal<SafeHtml>('');
  metadataCodeHtml = signal<SafeHtml>('');
  constraintsCodeHtml = signal<SafeHtml>('');

  private readonly createFromEventCode = `// Created automatically when appending events
var versionToken = new VersionToken(@event, document);`;

  private readonly createFromPartsCode = `// From explicit values
var token = new VersionToken(
    objectName: "order",
    objectId: "order-123",
    streamIdentifier: "order",
    version: 42);

// From string
var token = new VersionToken("order__order-123__order__00000000000000000042");

// From identifier parts
var objectId = new ObjectIdentifier("order", "order-123");
var versionId = new VersionIdentifier("order", 42);
var token = new VersionToken(objectId, versionId);`;

  private readonly optimisticConcurrencyCode = `public async Task UpdateOrder(
    string orderId,
    VersionToken expectedToken,
    UpdateRequest request)
{
    var order = await orderFactory.GetAsync(orderId);

    // Check version hasn't changed
    var currentToken = order.Stream.GetCurrentVersionToken();
    if (currentToken.Version != expectedToken.Version)
    {
        throw new ConcurrencyException(
            $"Order modified. Expected version {expectedToken.Version}, " +
            $"current is {currentToken.Version}");
    }

    await order.Update(request.Field, request.Value);
}`;

  private readonly checkpointCode = `// Store checkpoint in projection
projection.Checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;

// Check if event already processed
var key = versionToken.ObjectIdentifier;
if (projection.Checkpoint.TryGetValue(key, out var lastProcessed))
{
    if (lastProcessed.Version >= versionToken.Version)
    {
        return; // Already processed
    }
}`;

  private readonly correlationCode = `public async Task ShipOrder(string orderId, VersionToken userToken)
{
    var order = await orderFactory.GetAsync(orderId);

    await order.Stream.Session(context =>
        order.Fold(context.Append(
            new OrderShipped(DateTime.UtcNow),
            new ActionMetadata
            {
                EventOccuredAt = DateTime.UtcNow,
                OriginatedFromUser = userToken  // Track who triggered this
            })));
}`;

  private readonly apiResponseCode = `[HttpPost("orders/{id}/items")]
public async Task<IActionResult> AddItem(
    string id,
    [FromBody] AddItemRequest request,
    [FromHeader(Name = "If-Match")] string? etag)
{
    var order = await orderFactory.GetAsync(id);

    // Validate ETag if provided
    if (!string.IsNullOrEmpty(etag))
    {
        var expectedToken = new VersionToken(etag);
        if (order.Metadata.Version != expectedToken.Version)
        {
            return StatusCode(412, "Precondition Failed");
        }
    }

    await order.AddItem(request.ProductId, request.Quantity);

    // Return new version in ETag header
    var newToken = order.Stream.GetCurrentVersionToken();
    Response.Headers.ETag = newToken.Value;

    return Ok(new { versionToken = newToken.Value });
}`;

  private readonly metadataCode = `await Stream.Session(context =>
    Fold(context.Append(
        new OrderCreated(customerId),
        new ActionMetadata
        {
            EventOccuredAt = DateTime.UtcNow,
            OriginatedFromUser = userVersionToken,    // User context
            CorrelationId = correlationToken.Value    // Request correlation
        })));`;

  private readonly constraintsCode = `// Session constraints for existence checks
await Stream.Session(context => ..., Constraint.Loose);    // Default, no check
await Stream.Session(context => ..., Constraint.Existing); // Must exist
await Stream.Session(context => ..., Constraint.New);      // Must not exist`;

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
      fromEvent, fromParts, optimistic, checkpoint,
      correlation, apiResponse, metadata, constraints
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.createFromEventCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.createFromPartsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.optimisticConcurrencyCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.checkpointCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.correlationCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.apiResponseCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.metadataCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.constraintsCode, { language: 'csharp' })
    ]);

    this.createFromEventCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(fromEvent));
    this.createFromPartsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(fromParts));
    this.optimisticConcurrencyCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(optimistic));
    this.checkpointCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(checkpoint));
    this.correlationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(correlation));
    this.apiResponseCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(apiResponse));
    this.metadataCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(metadata));
    this.constraintsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(constraints));
  }
}
