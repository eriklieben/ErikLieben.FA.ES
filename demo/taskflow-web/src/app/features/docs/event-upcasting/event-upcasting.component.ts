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
  selector: 'app-event-upcasting-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './event-upcasting.component.html',
  styleUrl: './event-upcasting.component.css'
})
export class EventUpcastingComponent implements OnInit, OnDestroy {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);
  private intersectionObserver: IntersectionObserver | null = null;
  private visibleSections = new Set<string>();

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'when-to-use', label: 'When to Use', icon: 'help' },
    { id: 'interface', label: 'Interface', icon: 'code' },
    { id: 'basic-structure', label: 'Basic Structure', icon: 'account_tree' },
    { id: 'patterns', label: 'Common Patterns', icon: 'pattern' },
    { id: 'chaining', label: 'Chained Upcasters', icon: 'link' },
    { id: 'registry', label: 'Upcaster Registry', icon: 'list' },
    { id: 'testing', label: 'Testing', icon: 'science' },
    { id: 'comparison', label: 'Migration vs Upcasting', icon: 'compare' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' }
  ];

  activeSection = signal<string>('overview');

  interfaceCodeHtml = signal<SafeHtml>('');
  upcasterCodeHtml = signal<SafeHtml>('');
  registerCodeHtml = signal<SafeHtml>('');
  versionCodeHtml = signal<SafeHtml>('');
  addFieldCodeHtml = signal<SafeHtml>('');
  renameFieldCodeHtml = signal<SafeHtml>('');
  splitEventCodeHtml = signal<SafeHtml>('');
  chainedCodeHtml = signal<SafeHtml>('');
  registryCodeHtml = signal<SafeHtml>('');
  testingCodeHtml = signal<SafeHtml>('');

  private readonly interfaceCode = `public interface IUpcastEvent
{
    /// <summary>
    /// Determines whether this upcaster can handle the specified event.
    /// </summary>
    bool CanUpcast(IEvent @event);

    /// <summary>
    /// Upcasts an event to one or more newer event versions.
    /// </summary>
    IEnumerable<IEvent> UpCast(IEvent @event);
}`;

  private readonly upcasterCode = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Upcasting;

public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event)
    {
        // Match the old event type and version
        return @event.EventType == "Order.Created" && @event.EventVersion == 1;
    }

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        // Deserialize old event format
        var oldEvent = JsonEvent.ToEvent(@event,
            OrderCreatedV1JsonSerializerContext.Default.OrderCreatedV1);
        var data = oldEvent.Data();

        // Create new event with transformed data
        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                data.CustomerId,
                data.CreatedAt,
                CustomerType.Unknown  // New required field with default
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}`;

  private readonly registerCode = `[Aggregate]
[UseUpcaster<OrderCreatedV1ToV2Upcaster>]
public partial class Order : Aggregate
{
    public Order(IEventStream stream) : base(stream) { }

    // Event handlers work with the latest version only
    private void When(OrderCreatedV2 @event)
    {
        CustomerId = @event.CustomerId;
        CustomerType = @event.CustomerType;
    }
}`;

  private readonly versionCode = `// Original event (V1) - keep for deserialization
[EventName("Order.Created")]
[EventVersion(1)]
public record OrderCreatedV1(
    string CustomerId,
    DateTime CreatedAt);

// New event (V2) with additional field
[EventName("Order.Created")]
[EventVersion(2)]
public record OrderCreatedV2(
    string CustomerId,
    DateTime CreatedAt,
    CustomerType CustomerType);

// Even newer (V3)
[EventName("Order.Created")]
[EventVersion(3)]
public record OrderCreatedV3(
    string CustomerId,
    DateTime CreatedAt,
    CustomerType CustomerType,
    string Notes);`;

  private readonly addFieldCode = `public class OrderCreatedAddCustomerType : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                old.CustomerId,
                old.CreatedAt,
                CustomerType.Unknown  // Provide default value
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}`;

  private readonly renameFieldCode = `public class OrderCreatedRenameField : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,
            Data = new OrderCreatedV2(
                old.UserId,      // Was: UserId
                old.CreatedAt    // Now: CustomerId (renamed)
            ),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}`;

  private readonly splitEventCode = `public class ProjectCompletedSplit : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Project.Completed";

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            ProjectCompletedContext.Default.ProjectCompleted).Data();

        // Determine specific outcome from generic completion
        var outcome = old.Outcome?.ToLowerInvariant() ?? "";

        IEvent newEvent = outcome switch
        {
            _ when outcome.Contains("success") =>
                CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(old.Outcome ?? "", old.CompletedBy),
                    @event),

            _ when outcome.Contains("cancel") =>
                CreateEvent("Project.Cancelled",
                    new ProjectCancelled(old.Outcome ?? "", old.CompletedBy),
                    @event),

            _ when outcome.Contains("fail") =>
                CreateEvent("Project.Failed",
                    new ProjectFailed(old.Outcome ?? "", old.CompletedBy),
                    @event),

            _ => CreateEvent("Project.CompletedSuccessfully",
                    new ProjectCompletedSuccessfully(old.Outcome ?? "", old.CompletedBy),
                    @event)
        };

        yield return newEvent;
    }
}`;

  private readonly chainedCode = `// Register multiple upcasters for version chains
[Aggregate]
[UseUpcaster<OrderCreatedV1ToV2Upcaster>]  // v1 -> v2
[UseUpcaster<OrderCreatedV2ToV3Upcaster>]  // v2 -> v3
public partial class Order : Aggregate
{
    // Handler only needs the latest version
    private void When(OrderCreatedV3 @event) { }
}

// Each upcaster handles one version jump
public class OrderCreatedV1ToV2Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 1;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV1Context.Default.OrderCreatedV1).Data();

        yield return new Event<OrderCreatedV2>
        {
            EventType = "Order.Created",
            EventVersion = 2,  // Upcast to v2
            Data = new OrderCreatedV2(old.CustomerId, old.CreatedAt, CustomerType.Unknown),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}

public class OrderCreatedV2ToV3Upcaster : IUpcastEvent
{
    public bool CanUpcast(IEvent @event) =>
        @event.EventType == "Order.Created" && @event.EventVersion == 2;

    public IEnumerable<IEvent> UpCast(IEvent @event)
    {
        var old = JsonEvent.ToEvent(@event,
            OrderCreatedV2Context.Default.OrderCreatedV2).Data();

        yield return new Event<OrderCreatedV3>
        {
            EventType = "Order.Created",
            EventVersion = 3,  // Upcast to v3
            Data = new OrderCreatedV3(old.CustomerId, old.CreatedAt, old.CustomerType, ""),
            ActionMetadata = @event.ActionMetadata ?? new ActionMetadata(),
            Metadata = @event.Metadata
        };
    }
}`;

  private readonly registryCode = `var registry = new EventUpcasterRegistry();

// Register upcasters with typed delegates
registry.Add<OrderCreatedV1, OrderCreatedV2>(
    "Order.Created",
    fromVersion: 1,
    toVersion: 2,
    upcast: v1 => new OrderCreatedV2(v1.CustomerId, v1.CreatedAt, CustomerType.Unknown));

registry.Add<OrderCreatedV2, OrderCreatedV3>(
    "Order.Created",
    fromVersion: 2,
    toVersion: 3,
    upcast: v2 => new OrderCreatedV3(v2.CustomerId, v2.CreatedAt, v2.CustomerType, ""));

// Freeze for optimized lookups
registry.Freeze();

// Upcast event data through multiple versions
var (data, finalVersion) = registry.UpcastToVersion(
    "Order.Created",
    currentVersion: 1,
    targetVersion: 3,
    eventData: oldEventData);`;

  private readonly testingCode = `[Fact]
public void Upcaster_ShouldTransformV1ToV2()
{
    // Arrange
    var upcaster = new OrderCreatedV1ToV2Upcaster();
    var v1Event = new JsonEvent
    {
        EventType = "Order.Created",
        EventVersion = 1,
        Payload = JsonSerializer.Serialize(
            new OrderCreatedV1("customer-1", DateTime.UtcNow))
    };

    // Act
    var result = upcaster.UpCast(v1Event).ToList();

    // Assert
    Assert.Single(result);
    var upcastedEvent = result[0];
    Assert.Equal("Order.Created", upcastedEvent.EventType);
    Assert.Equal(2, upcastedEvent.EventVersion);

    var data = (OrderCreatedV2)((Event<OrderCreatedV2>)upcastedEvent).Data;
    Assert.Equal("customer-1", data.CustomerId);
    Assert.Equal(CustomerType.Unknown, data.CustomerType);
}

[Fact]
public async Task Aggregate_ShouldLoadWithUpcastedEvents()
{
    var context = TestSetup.GetContext();

    // Given v1 events in the stream
    await AggregateTestBuilder.For<Order>("order-1", context)
        .Given(new JsonEvent
        {
            EventType = "Order.Created",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(
                new OrderCreatedV1("customer-1", DateTime.UtcNow))
        })
        .Then(order =>
        {
            // State should reflect upcasted event
            Assert.Equal("customer-1", order.CustomerId);
            Assert.Equal(CustomerType.Unknown, order.CustomerType);
        });
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
      iface, upcaster, register, version, addField,
      renameField, splitEvent, chained, registry, testing
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.interfaceCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.upcasterCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.registerCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.versionCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.addFieldCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.renameFieldCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.splitEventCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.chainedCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.registryCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.testingCode, { language: 'csharp' })
    ]);

    this.interfaceCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(iface));
    this.upcasterCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(upcaster));
    this.registerCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(register));
    this.versionCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(version));
    this.addFieldCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(addField));
    this.renameFieldCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(renameField));
    this.splitEventCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(splitEvent));
    this.chainedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(chained));
    this.registryCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(registry));
    this.testingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(testing));
  }
}
