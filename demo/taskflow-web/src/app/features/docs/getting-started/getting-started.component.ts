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
  selector: 'app-getting-started',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    RouterLink
  ],
  templateUrl: './getting-started.component.html',
  styleUrl: './getting-started.component.css'
})
export class GettingStartedComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  // Navigation items
  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'installation', label: 'Installation', icon: 'download' },
    { id: 'quickstart', label: 'Quick Start', icon: 'rocket_launch' },
    { id: 'usage', label: 'Usage', icon: 'play_circle' },
    { id: 'concepts', label: 'Core Concepts', icon: 'school' },
    { id: 'frameworks', label: 'Frameworks', icon: 'hub' },
    { id: 'packages', label: 'Packages', icon: 'inventory_2' }
  ];

  // Active section tracking
  activeSection = signal<string>('overview');

  // Highlighted code HTML
  installCodeHtml = signal<SafeHtml>('');
  cliInstallCodeHtml = signal<SafeHtml>('');
  eventCodeHtml = signal<SafeHtml>('');
  aggregateCodeHtml = signal<SafeHtml>('');
  generateCodeHtml = signal<SafeHtml>('');
  configCodeHtml = signal<SafeHtml>('');
  usageCodeHtml = signal<SafeHtml>('');

  // Code samples
  private readonly installCodeSample = `# Core framework
dotnet add package ErikLieben.FA.ES

# Azure Blob Storage provider
dotnet add package ErikLieben.FA.ES.AzureStorage

# Roslyn analyzers (recommended)
dotnet add package ErikLieben.FA.ES.Analyzers`;

  private readonly cliInstallCodeSample = `# Create tool manifest (if not exists)
dotnet new tool-manifest

# Install the CLI tool
dotnet tool install ErikLieben.FA.ES.CLI

# Run code generation
dotnet tool run faes generate`;

  private readonly eventCodeSample = `using ErikLieben.FA.ES;

// Events describe what happened - use past tense
[EventName("Customer.Registered")]
public record CustomerRegistered(
    string CustomerId,
    string Name,
    string Email,
    DateTime RegisteredAt);

[EventName("Customer.EmailUpdated")]
public record CustomerEmailUpdated(
    string NewEmail,
    DateTime UpdatedAt);`;

  private readonly aggregateCodeSample = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Processors;

// Aggregates must be partial - CLI generates companion code
[ObjectName("Customer")]
public partial class Customer(IEventStream stream) : Aggregate(stream)
{
    // State properties
    public string? Name { get; private set; }
    public string? Email { get; private set; }
    public DateTime? RegisteredAt { get; private set; }

    // Command method - validates and emits events
    public async Task Register(string customerId, string name, string email)
    {
        if (RegisteredAt.HasValue)
            throw new InvalidOperationException("Customer already registered");

        await Stream.Session(context =>
            Fold(context.Append(new CustomerRegistered(
                customerId, name, email, DateTime.UtcNow))));
    }

    // When method - applies event to state (called during Fold)
    private void When(CustomerRegistered @event)
    {
        Name = @event.Name;
        Email = @event.Email;
        RegisteredAt = @event.RegisteredAt;
    }

    // Alternative: [When<T>] attribute when event data isn't needed
    [When<CustomerEmailUpdated>]
    private void OnEmailUpdated()
    {
        // Email is updated in the other When method
    }
}`;

  private readonly generateCodeSample = `# Navigate to your solution directory
cd /path/to/your/solution

# Run code generation
dotnet tool run faes generate

# This generates:
# - Customer.Generated.cs (factory, serializer, fold mapping)
# - YourProject.DomainExtensions.Generated.cs (DI registration)`;

  private readonly configCodeSample = `using ErikLieben.FA.ES;
using ErikLieben.FA.ES.AzureStorage;

var builder = WebApplication.CreateBuilder(args);

// Register Azure Blob Storage client
builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(
        builder.Configuration.GetConnectionString("Storage"))
        .WithName("EventStore");
});

// Configure the event store
builder.Services.ConfigureBlobEventStore(new EventStreamBlobSettings("EventStore"));
builder.Services.ConfigureEventStore(new EventStreamDefaultTypeSettings("blob"));

// Register generated factories and serializers
builder.Services.ConfigureYourProjectDomainFactory();

var app = builder.Build();`;

  private readonly usageCodeSample = `// Inject the generated factory
public class CustomerService(ICustomerFactory customerFactory)
{
    public async Task<string> RegisterCustomer(string name, string email)
    {
        // Create a new customer with generated ID
        var customerId = Guid.NewGuid().ToString();
        var customer = await customerFactory.CreateAsync(new CustomerId(customerId));

        // Execute the command
        await customer.Register(customerId, name, email);

        return customerId;
    }

    public async Task<Customer> GetCustomer(string customerId)
    {
        // Load aggregate by replaying events
        var customer = await customerFactory.GetAsync(new CustomerId(customerId));
        return customer;
    }
}`;

  constructor() {
    // Re-highlight code when theme changes
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
      installHtml,
      cliInstallHtml,
      eventHtml,
      aggregateHtml,
      generateHtml,
      configHtml,
      usageHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.installCodeSample, { language: 'bash' }),
      this.codeHighlighter.highlight(this.cliInstallCodeSample, { language: 'bash' }),
      this.codeHighlighter.highlight(this.eventCodeSample, {
        language: 'csharp',
        highlightLines: [4, 5, 11, 12]
      }),
      this.codeHighlighter.highlight(this.aggregateCodeSample, {
        language: 'csharp',
        highlightLines: [6, 7, 17, 18, 19, 23, 24, 31]
      }),
      this.codeHighlighter.highlight(this.generateCodeSample, { language: 'bash' }),
      this.codeHighlighter.highlight(this.configCodeSample, {
        language: 'csharp',
        highlightLines: [12, 13, 16]
      }),
      this.codeHighlighter.highlight(this.usageCodeSample, {
        language: 'csharp',
        highlightLines: [7, 8, 11, 18]
      })
    ]);

    this.installCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(installHtml));
    this.cliInstallCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(cliInstallHtml));
    this.eventCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(eventHtml));
    this.aggregateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(aggregateHtml));
    this.generateCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(generateHtml));
    this.configCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(configHtml));
    this.usageCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(usageHtml));
  }
}
