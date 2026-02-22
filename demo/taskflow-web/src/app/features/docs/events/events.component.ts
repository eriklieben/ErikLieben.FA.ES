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
  selector: 'app-events-docs',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './events.component.html',
  styleUrl: './events.component.css'
})
export class EventsComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'defining', label: 'Defining Events', icon: 'add_circle' },
    { id: 'naming', label: 'Event Naming', icon: 'label' },
    { id: 'design', label: 'Event Design', icon: 'design_services' },
    { id: 'versioning', label: 'Versioning', icon: 'history' },
    { id: 'serialization', label: 'Serialization', icon: 'transform' },
    { id: 'storage', label: 'Storage', icon: 'storage' },
    { id: 'best-practices', label: 'Best Practices', icon: 'verified' },
    { id: 'complete-example', label: 'Complete Example', icon: 'integration_instructions' }
  ];

  activeSection = signal<string>('overview');

  basicEventCodeHtml = signal<SafeHtml>('');
  eventNameCodeHtml = signal<SafeHtml>('');
  namingExamplesCodeHtml = signal<SafeHtml>('');
  selfContainedCodeHtml = signal<SafeHtml>('');
  strongTypesCodeHtml = signal<SafeHtml>('');
  optionalPropsCodeHtml = signal<SafeHtml>('');
  upcastingCodeHtml = signal<SafeHtml>('');
  serializerCodeHtml = signal<SafeHtml>('');
  completeExampleCodeHtml = signal<SafeHtml>('');

  private readonly basicEventCode = `using ErikLieben.FA.ES;

// Events are immutable records describing what happened
[EventName("Customer.Registered")]
public record CustomerRegistered(
    string CustomerId,
    string Name,
    string Email,
    DateTime RegisteredAt);

// Events can be simple
[EventName("Customer.Deactivated")]
public record CustomerDeactivated(
    string Reason,
    DateTime DeactivatedAt);`;

  private readonly eventNameCode = `// Without [EventName], the CLR type name is used in storage
public record CustomerRegistered(...);  // Stored as "CustomerRegistered"

// With [EventName], you control the storage name
[EventName("Customer.Registered")]
public record CustomerRegistered(...);  // Stored as "Customer.Registered"

// Now you can safely rename the class:
[EventName("Customer.Registered")]  // Keep the same name!
public record CustomerRegistrationCompleted(...);

// Events in storage still work because they reference "Customer.Registered"`;

  private readonly namingExamplesCode = `// Good event names - clear, past tense, domain-focused
[EventName("Order.Created")]
[EventName("Order.ItemAdded")]
[EventName("Order.Shipped")]
[EventName("Customer.AddressChanged")]
[EventName("Payment.Received")]
[EventName("Invoice.Generated")]

// Bad event names - avoid these patterns
[EventName("CreateOrder")]        // Command, not event (use past tense)
[EventName("OrderData")]          // Too vague
[EventName("Updated")]            // What was updated?
[EventName("OrderChangedEvent")]  // Redundant "Event" suffix
[EventName("order_shipped")]      // Inconsistent casing`;

  private readonly selfContainedCode = `// Good: Self-contained event with all relevant data
[EventName("Order.Shipped")]
public record OrderShipped(
    string OrderId,
    string Carrier,
    string TrackingNumber,
    DateTime ShippedAt,
    string ShippingAddress);  // Include address at time of shipping

// Bad: Event that requires looking up other data
[EventName("Order.Shipped")]
public record OrderShipped(
    string OrderId,
    DateTime ShippedAt);  // Missing carrier, tracking, address`;

  private readonly strongTypesCode = `// Using primitives (okay for simple cases)
[EventName("Order.Created")]
public record OrderCreated(
    string OrderId,      // Could be any string
    string CustomerId);  // Could be confused with OrderId

// Using strong types (better clarity and type safety)
[EventName("Order.Created")]
public record OrderCreated(
    OrderId OrderId,
    CustomerId CustomerId);

// Supporting types
public readonly record struct OrderId(Guid Value);
public readonly record struct CustomerId(Guid Value);`;

  private readonly optionalPropsCode = `// Original event
[EventName("Customer.Registered")]
public record CustomerRegistered(
    string Name,
    string Email);

// Later: Add optional property (non-breaking change)
[EventName("Customer.Registered")]
public record CustomerRegistered(
    string Name,
    string Email,
    string? PhoneNumber = null);  // Old events deserialize with null

// Old events in storage:  { "name": "John", "email": "..." }
// Deserializes to:        CustomerRegistered("John", "...", null)`;

  private readonly upcastingCode = `// Version 1 of the event (default schema version is 1)
[EventName("Customer.Registered")]
public record CustomerRegisteredV1(string FullName, string Email);

// Version 2 with breaking change (split name into first/last)
[EventName("Customer.Registered")]
[EventVersion(2)]
public record CustomerRegisteredV2(string FirstName, string LastName, string Email);

// Both versions are registered automatically by the CLI tool:
// - CustomerRegisteredV1 with schemaVersion: 1
// - CustomerRegisteredV2 with schemaVersion: 2

// The system deserializes to the correct type based on schemaVersion.
// You can handle both versions in your When methods:
private void When(CustomerRegisteredV1 e) => Name = e.FullName;
private void When(CustomerRegisteredV2 e) => Name = $"{e.FirstName} {e.LastName}";

// OPTIONAL: Register an upcaster to auto-convert V1 to V2 during read
options.RegisterUpcaster<CustomerRegisteredV1, CustomerRegisteredV2>(
    eventName: "Customer.Registered",
    fromVersion: 1,
    toVersion: 2,
    upcast: v1 =>
    {
        var parts = v1.FullName.Split(' ', 2);
        return new CustomerRegisteredV2(
            parts[0],
            parts.Length > 1 ? parts[1] : "",
            v1.Email);
    });

// The schemaVersion is stored separately in the event JSON:
// { "type": "Customer.Registered", "schemaVersion": 1, "payload": {...} }`;

  private readonly serializerCode = `// Generated by CLI tool (in .Generated.cs)
[JsonSerializable(typeof(CustomerRegistered))]
[JsonSerializable(typeof(CustomerEmailUpdated))]
[JsonSerializable(typeof(CustomerDeactivated))]
internal partial class CustomerEventSerializerContext : JsonSerializerContext
{
}

// Events are registered with their serializer
stream.RegisterEvent<CustomerRegistered>(
    "Customer.Registered",
    CustomerEventSerializerContext.Default.CustomerRegistered);`;

  private readonly completeExampleCode = `using ErikLieben.FA.ES;

namespace MyApp.Domain.Orders.Events;

// Order lifecycle events
[EventName("Order.Created")]
public record OrderCreated(
    string CustomerId,
    DateTime CreatedAt);

[EventName("Order.ItemAdded")]
public record OrderItemAdded(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    DateTime AddedAt);

[EventName("Order.ItemRemoved")]
public record OrderItemRemoved(
    string ProductId,
    int Quantity,
    DateTime RemovedAt);

[EventName("Order.ShippingAddressSet")]
public record OrderShippingAddressSet(
    string Street,
    string City,
    string PostalCode,
    string Country,
    DateTime SetAt);

[EventName("Order.Submitted")]
public record OrderSubmitted(
    decimal TotalAmount,
    DateTime SubmittedAt);

[EventName("Order.Paid")]
public record OrderPaid(
    string PaymentId,
    string PaymentMethod,
    decimal Amount,
    DateTime PaidAt);

[EventName("Order.Shipped")]
public record OrderShipped(
    string Carrier,
    string TrackingNumber,
    DateTime EstimatedDelivery,
    DateTime ShippedAt);

[EventName("Order.Delivered")]
public record OrderDelivered(
    DateTime DeliveredAt,
    string? ReceivedBy);

[EventName("Order.Cancelled")]
public record OrderCancelled(
    string Reason,
    DateTime CancelledAt);`;

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
    const [basic, name, naming, self, strong, optional, upcast, serial, complete] = await Promise.all([
      this.codeHighlighter.highlight(this.basicEventCode, { language: 'csharp', highlightLines: [4, 5] }),
      this.codeHighlighter.highlight(this.eventNameCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.namingExamplesCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.selfContainedCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.strongTypesCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.optionalPropsCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.upcastingCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.serializerCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.completeExampleCode, { language: 'csharp' })
    ]);

    this.basicEventCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(basic));
    this.eventNameCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(name));
    this.namingExamplesCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(naming));
    this.selfContainedCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(self));
    this.strongTypesCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(strong));
    this.optionalPropsCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(optional));
    this.upcastingCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(upcast));
    this.serializerCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(serial));
    this.completeExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(complete));
  }
}
