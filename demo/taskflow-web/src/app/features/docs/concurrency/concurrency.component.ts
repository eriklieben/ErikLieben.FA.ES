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
  selector: 'app-concurrency',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './concurrency.component.html',
  styleUrl: './concurrency.component.css'
})
export class ConcurrencyComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'constraints', label: 'Constraint Types', icon: 'category' },
    { id: 'loose', label: 'Loose Constraint', icon: 'all_inclusive' },
    { id: 'new', label: 'New Constraint', icon: 'add_circle' },
    { id: 'existing', label: 'Existing Constraint', icon: 'edit' },
    { id: 'optimistic', label: 'Optimistic Concurrency', icon: 'sync_alt' },
    { id: 'examples', label: 'Examples', icon: 'code' }
  ];

  activeSection = signal<string>('overview');

  looseCodeHtml = signal<SafeHtml>('');
  newCodeHtml = signal<SafeHtml>('');
  existingCodeHtml = signal<SafeHtml>('');
  optimisticCodeHtml = signal<SafeHtml>('');
  conflictCodeHtml = signal<SafeHtml>('');
  commandHandlerCodeHtml = signal<SafeHtml>('');

  private readonly looseCodeSample = `// Loose constraint - no version checking
// Use when you don't care if the stream is new or existing

var session = await eventStream.OpenSessionAsync(Constraint.Loose);

// This will work whether the stream exists or not
session.Append(new OrderCreated { OrderId = orderId });

await session.CommitAsync();`;

  private readonly newCodeSample = `// New constraint - only works if stream doesn't exist
// Use for creating new aggregates to prevent duplicates

var session = await eventStream.OpenSessionAsync(Constraint.New);

// This will throw if the stream already exists
session.Append(new CustomerCreated
{
    CustomerId = customerId,
    Name = name,
    Email = email
});

await session.CommitAsync();

// Throws ConcurrencyException if stream already exists`;

  private readonly existingCodeSample = `// Existing constraint - only works if stream exists
// Use when updating aggregates that must already exist

var session = await eventStream.OpenSessionAsync(Constraint.Existing);

// This will throw if the stream doesn't exist
session.Append(new OrderShipped
{
    OrderId = orderId,
    ShippedAt = DateTimeOffset.UtcNow
});

await session.CommitAsync();

// Throws ConcurrencyException if stream doesn't exist`;

  private readonly optimisticCodeSample = `// Optimistic concurrency with version tracking
// The system tracks the expected version automatically

var session = await eventStream.OpenSessionAsync(Constraint.Existing);

// Read current state (tracks version internally)
var order = await session.GetAsync<Order>();

// Make changes based on current state
if (order.Status == OrderStatus.Pending)
{
    session.Append(new OrderConfirmed { OrderId = order.Id });
}

// Commit will fail if another process modified the stream
await session.CommitAsync();`;

  private readonly conflictCodeSample = `// Handling concurrency conflicts with retry logic
public async Task AppendWithRetryAsync<TEvent>(
    IEventStream eventStream,
    TEvent @event,
    int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            var session = await eventStream.OpenSessionAsync(Constraint.Existing);
            session.Append(@event);
            await session.CommitAsync();
            return; // Success
        }
        catch (ConcurrencyException)
        {
            if (attempt == maxRetries - 1)
                throw; // Rethrow on final attempt

            // Exponential backoff
            await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
        }
    }
}`;

  private readonly commandHandlerCodeSample = `public class OrderCommandHandler
{
    private readonly IEventStreamFactory _eventStreamFactory;

    public async Task Handle(CreateOrderCommand command)
    {
        var stream = _eventStreamFactory.GetStream(command.OrderId);

        // Use New constraint to ensure order doesn't already exist
        var session = await stream.OpenSessionAsync(Constraint.New);

        session.Append(new OrderCreated
        {
            OrderId = command.OrderId,
            CustomerId = command.CustomerId,
            Items = command.Items
        });

        await session.CommitAsync();
    }

    public async Task Handle(ShipOrderCommand command)
    {
        var stream = _eventStreamFactory.GetStream(command.OrderId);

        // Use Existing constraint to ensure order exists
        var session = await stream.OpenSessionAsync(Constraint.Existing);

        var order = await session.GetAsync<Order>();

        if (order.Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Order must be confirmed before shipping");

        session.Append(new OrderShipped
        {
            OrderId = command.OrderId,
            ShippedAt = DateTimeOffset.UtcNow
        });

        await session.CommitAsync();
    }
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
      looseHtml,
      newHtml,
      existingHtml,
      optimisticHtml,
      conflictHtml,
      commandHandlerHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.looseCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.newCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.existingCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.optimisticCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.conflictCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.commandHandlerCodeSample, { language: 'csharp' })
    ]);

    this.looseCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(looseHtml));
    this.newCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(newHtml));
    this.existingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(existingHtml));
    this.optimisticCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(optimisticHtml));
    this.conflictCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(conflictHtml));
    this.commandHandlerCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(commandHandlerHtml));
  }
}
