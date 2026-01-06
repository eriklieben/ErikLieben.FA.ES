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
  selector: 'app-aggregates',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './aggregates.component.html',
  styleUrl: './aggregates.component.css'
})
export class AggregatesComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  // Navigation items
  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'base-class', label: 'Base Class', icon: 'account_tree' },
    { id: 'object-name', label: 'Object Naming', icon: 'label' },
    { id: 'commands', label: 'Commands', icon: 'edit' },
    { id: 'when-methods', label: 'When Methods', icon: 'sync_alt' },
    { id: 'fold', label: 'Fold Method', icon: 'layers' },
    { id: 'factory', label: 'Factories', icon: 'factory' },
    { id: 'snapshots', label: 'Snapshots', icon: 'photo_camera' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' },
    { id: 'complete-example', label: 'Complete Example', icon: 'integration_instructions' }
  ];

  activeSection = signal<string>('overview');
  hoveredStep = signal<number | null>(null);

  // Highlighted code HTML
  basicAggregateCodeHtml = signal<SafeHtml>('');
  objectNameCodeHtml = signal<SafeHtml>('');
  commandCodeHtml = signal<SafeHtml>('');
  whenMethodCodeHtml = signal<SafeHtml>('');
  whenAttributeCodeHtml = signal<SafeHtml>('');
  foldCodeHtml = signal<SafeHtml>('');
  factoryCodeHtml = signal<SafeHtml>('');
  factoryUsageCodeHtml = signal<SafeHtml>('');
  snapshotCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');

  // Code samples
  private readonly basicAggregateCode = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Processors;

// Aggregates MUST be partial - CLI generates companion code
[ObjectName("Customer")]
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
    // State properties - only modified by When methods
    public string? Name { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    // Command methods, When methods, etc.
}`;

  private readonly objectNameCode = `// The ObjectName is used in storage, not the class name
[ObjectName("Customer")]
public partial class Customer : Aggregate { }

// Later, you can safely rename the class:
[ObjectName("Customer")]  // Keep the same ObjectName!
public partial class CustomerAggregate : Aggregate { }

// Events in storage still reference "Customer" and work correctly`;

  private readonly commandCode = `public async Task Register(string name, string email)
{
    // Step 1: Validate business rules
    if (IsActive)
        throw new InvalidOperationException("Customer already registered");

    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name is required");

    // Step 2: Open a session for atomic operations
    await Stream.Session(context =>
        // Step 3: Fold wraps Append - event is stored AND applied to state
        Fold(context.Append(new CustomerRegistered(name, email, DateTime.UtcNow))));
}`;

  private readonly whenMethodCode = `// Method signature pattern - event data is available
private void When(CustomerRegistered @event)
{
    Name = @event.Name;
    Email = @event.Email;
    IsActive = true;
}

private void When(CustomerEmailUpdated @event)
{
    Email = @event.NewEmail;
}`;

  private readonly whenAttributeCode = `// [When<T>] pattern - cleaner when event data isn't needed
[When<CustomerDeactivated>]
private void OnDeactivated()
{
    IsActive = false;
}

[When<CustomerReactivated>]
private void OnReactivated()
{
    IsActive = true;
}`;

  private readonly foldCode = `// When loading an aggregate, Fold() replays all events:
var customer = await customerFactory.GetAsync(customerId);
// Internally this calls:
// 1. stream.ReadAsync() - gets all events
// 2. For each event: Fold(event) - dispatches to When method

// When executing a command, Fold wraps Append:
await Stream.Session(context =>
    Fold(context.Append(new CustomerRegistered(...))));
// This:
// 1. Appends the event to the stream
// 2. Immediately calls the When method to update state

// Generated Fold method (in .Generated.cs):
protected override void Fold(IEvent @event)
{
    switch (@event)
    {
        case CustomerRegistered e: When(e); break;
        case CustomerEmailUpdated e: When(e); break;
        case CustomerDeactivated: OnDeactivated(); break;
        // ... other events
    }
}`;

  private readonly factoryCode = `// Generated interface (in Customer.Generated.cs)
public interface ICustomerFactory
{
    // Create new aggregate with specific ID
    Task<Customer> CreateAsync(CustomerId id);

    // Load existing aggregate by replaying events
    Task<Customer> GetAsync(CustomerId id);
}

// Generated implementation
internal class CustomerFactory : ICustomerFactory
{
    private readonly IEventStreamFactory _streamFactory;

    public async Task<Customer> CreateAsync(CustomerId id)
    {
        var stream = await _streamFactory.CreateAsync("Customer", id.Value);
        return new Customer(stream);
    }

    public async Task<Customer> GetAsync(CustomerId id)
    {
        var stream = await _streamFactory.GetAsync("Customer", id.Value);
        var customer = new Customer(stream);
        await customer.Fold(); // Replay all events
        return customer;
    }
}`;

  private readonly factoryUsageCode = `// Inject the factory
public class CustomerService(ICustomerFactory customerFactory)
{
    public async Task<Customer> CreateCustomer(string name, string email)
    {
        // Create with a new ID
        var id = new CustomerId(Guid.NewGuid());
        var customer = await customerFactory.CreateAsync(id);

        // Execute command
        await customer.Register(name, email);

        return customer;
    }

    public async Task<Customer> GetCustomer(CustomerId id)
    {
        // Load by replaying events
        return await customerFactory.GetAsync(id);
    }
}`;

  private readonly snapshotCode = `// Snapshots are created from aggregate state
public partial class Customer : Aggregate
{
    // Define what state to snapshot
    public record CustomerSnapshot(string? Name, string? Email, bool IsActive);

    // Called by generated code when creating snapshot
    public CustomerSnapshot CreateSnapshot()
    {
        return new CustomerSnapshot(Name, Email, IsActive);
    }

    // Called when loading from snapshot
    protected override void ProcessSnapshot(object snapshot)
    {
        if (snapshot is CustomerSnapshot s)
        {
            Name = s.Name;
            Email = s.Email;
            IsActive = s.IsActive;
        }
    }
}

// Create a snapshot (typically done periodically or after N events)
await stream.Snapshot<CustomerSnapshot>(customer.CreateSnapshot());`;

  private readonly completeExampleCode = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Processors;

// Events
[EventName("Order.Created")]
public record OrderCreated(string CustomerId, DateTime CreatedAt);

[EventName("Order.ItemAdded")]
public record OrderItemAdded(string ProductId, int Quantity, decimal Price);

[EventName("Order.Shipped")]
public record OrderShipped(string Carrier, string TrackingNumber, DateTime ShippedAt);

// Aggregate
[ObjectName("Order")]
public partial class Order(IEventStream stream) : Aggregate(stream)
{
    // State
    public string? CustomerId { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);

    // Commands
    public async Task Create(string customerId)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Order already created");

        await Stream.Session(ctx =>
            Fold(ctx.Append(new OrderCreated(customerId, DateTime.UtcNow))));
    }

    public async Task AddItem(string productId, int quantity, decimal price)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Cannot add items to this order");

        await Stream.Session(ctx =>
            Fold(ctx.Append(new OrderItemAdded(productId, quantity, price))));
    }

    public async Task Ship(string carrier, string trackingNumber)
    {
        if (Status != OrderStatus.Created)
            throw new InvalidOperationException("Order cannot be shipped");

        await Stream.Session(ctx =>
            Fold(ctx.Append(new OrderShipped(carrier, trackingNumber, DateTime.UtcNow))));
    }

    // When methods
    private void When(OrderCreated @event)
    {
        CustomerId = @event.CustomerId;
        Status = OrderStatus.Created;
    }

    private void When(OrderItemAdded @event)
    {
        Items.Add(new OrderItem(@event.ProductId, @event.Quantity, @event.Price));
    }

    [When<OrderShipped>]
    private void OnShipped() => Status = OrderStatus.Shipped;
}

public record OrderItem(string ProductId, int Quantity, decimal Price);
public enum OrderStatus { Draft, Created, Shipped, Delivered }`;

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

  onCodeHover(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const line = target.closest('.step-line');
    if (line) {
      if (line.classList.contains('step-1')) this.hoveredStep.set(1);
      else if (line.classList.contains('step-2')) this.hoveredStep.set(2);
      else if (line.classList.contains('step-3')) this.hoveredStep.set(3);
    } else {
      this.hoveredStep.set(null);
    }
  }

  private async highlightCodeSamples(): Promise<void> {
    const [
      basicAggregate,
      objectName,
      command,
      whenMethod,
      whenAttribute,
      fold,
      factory,
      factoryUsage,
      snapshot,
      complete
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.basicAggregateCode, {
        language: 'csharp',
        highlightLines: [5, 6]
      }),
      this.codeHighlighter.highlight(this.objectNameCode, {
        language: 'csharp',
        highlightLines: [2, 7]
      }),
      this.codeHighlighter.highlight(this.commandCode, {
        language: 'csharp',
        stepHighlights: [
          { step: 1, lines: [3, 4, 5, 6, 7, 8] },
          { step: 2, lines: [10, 11] },
          { step: 3, lines: [12, 13] }
        ]
      }),
      this.codeHighlighter.highlight(this.whenMethodCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.whenAttributeCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.foldCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.factoryCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.factoryUsageCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.snapshotCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.completeExampleCode, { language: 'csharp' })
    ]);

    this.basicAggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basicAggregate));
    this.objectNameCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(objectName));
    this.commandCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(command));
    this.whenMethodCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(whenMethod));
    this.whenAttributeCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(whenAttribute));
    this.foldCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(fold));
    this.factoryCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(factory));
    this.factoryUsageCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(factoryUsage));
    this.snapshotCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(snapshot));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(complete));
  }
}
