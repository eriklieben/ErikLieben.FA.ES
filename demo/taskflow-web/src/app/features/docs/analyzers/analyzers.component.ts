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

interface AnalyzerRule {
  id: string;
  title: string;
  severity: 'error' | 'warning' | 'info';
  category: string;
  description: string;
  fix: string;
  example?: string;
}

@Component({
  selector: 'app-analyzers',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './analyzers.component.html',
  styleUrl: './analyzers.component.css'
})
export class AnalyzersComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'faes0002', label: 'FAES0002', icon: 'warning' },
    { id: 'faes0003', label: 'FAES0003', icon: 'warning' },
    { id: 'faes0005', label: 'FAES0005', icon: 'warning' },
    { id: 'faes0006', label: 'FAES0006', icon: 'warning' },
    { id: 'faes0007', label: 'FAES0007', icon: 'warning' },
    { id: 'configuration', label: 'Configuration', icon: 'settings' }
  ];

  readonly rules: AnalyzerRule[] = [
    {
      id: 'FAES0002',
      title: 'Appended event is not applied to active state',
      severity: 'warning',
      category: 'Usage',
      description: 'Within a Stream.Session in an Aggregate, appending an event should be applied to the aggregate\'s active state using Fold(context.Append(...)).',
      fix: 'Wrap the Append call with Fold or When to apply the event to state.',
      example: `// Bad - Event not applied
context.Append(new OrderCreated { ... });

// Good - Event applied to state
Fold(context.Append(new OrderCreated { ... }));
// or
When(context.Append(new OrderCreated { ... }));`
    },
    {
      id: 'FAES0003',
      title: 'Aggregate-derived class should be partial',
      severity: 'warning',
      category: 'Usage',
      description: 'Classes that inherit from ErikLieben.FA.ES.Processors.Aggregate must be declared partial so that the CLI tool can extend them.',
      fix: 'Add the \'partial\' keyword to the class declaration.',
      example: `// Bad
public class OrderAggregate : Aggregate<OrderState> { ... }

// Good
public partial class OrderAggregate : Aggregate<OrderState> { ... }`
    },
    {
      id: 'FAES0005',
      title: 'Generated file missing',
      severity: 'warning',
      category: 'CodeGeneration',
      description: 'Classes inheriting from Aggregate or Projection require generated code. The generated file ({ClassName}.Generated.cs) was not found.',
      fix: 'Run \'dotnet faes\' to generate the supporting code.',
      example: `// After adding a new Aggregate, run:
dotnet faes

// Or with watch mode for continuous generation:
dotnet faes watch`
    },
    {
      id: 'FAES0006',
      title: 'Generated code is out of date',
      severity: 'warning',
      category: 'CodeGeneration',
      description: 'A When method or [When<T>] attribute was added but the generated Fold method doesn\'t include it. The generated code needs to be regenerated.',
      fix: 'Run \'dotnet faes\' to regenerate the code.',
      example: `// When you add a new event handler:
public void When(OrderShipped evt) { ... }

// Run to update generated code:
dotnet faes`
    },
    {
      id: 'FAES0007',
      title: 'Property not in generated interface',
      severity: 'warning',
      category: 'CodeGeneration',
      description: 'A public property was added to the aggregate but the generated interface (I{ClassName}) doesn\'t include it.',
      fix: 'Run \'dotnet faes\' to regenerate the interface.',
      example: `// When you add a new property:
public OrderStatus Status { get; private set; }

// Run to update generated interface:
dotnet faes`
    }
  ];

  activeSection = signal<string>('overview');

  badExampleCodeHtml = signal<SafeHtml>('');
  goodExampleCodeHtml = signal<SafeHtml>('');
  configCodeHtml = signal<SafeHtml>('');

  private readonly badExampleCodeSample = `// FAES0002 violation - event not applied to state
public async Task CreateOrder(OrderData data)
{
    await Stream.Session(async context =>
    {
        // This triggers FAES0002
        context.Append(new OrderCreated
        {
            OrderId = Id,
            CustomerId = data.CustomerId
        });
    });
}`;

  private readonly goodExampleCodeSample = `// Correct usage - event applied via Fold
public async Task CreateOrder(OrderData data)
{
    await Stream.Session(async context =>
    {
        // Event is applied to state via Fold
        Fold(context.Append(new OrderCreated
        {
            OrderId = Id,
            CustomerId = data.CustomerId
        }));
    });
}`;

  private readonly configCodeSample = `// .editorconfig - configure analyzer severity
[*.cs]
# Suppress FAES0002 for test projects
dotnet_diagnostic.FAES0002.severity = none

# Make FAES0003 an error
dotnet_diagnostic.FAES0003.severity = error

// Or in project file (.csproj)
<PropertyGroup>
  <NoWarn>FAES0005</NoWarn>
</PropertyGroup>

// Or inline suppression
#pragma warning disable FAES0002
context.Append(new LegacyEvent());
#pragma warning restore FAES0002`;

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

  getSeverityIcon(severity: string): string {
    switch (severity) {
      case 'error': return 'error';
      case 'warning': return 'warning';
      case 'info': return 'info';
      default: return 'help';
    }
  }

  private async highlightCodeSamples(): Promise<void> {
    const [badHtml, goodHtml, configHtml] = await Promise.all([
      this.codeHighlighter.highlight(this.badExampleCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.goodExampleCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.configCodeSample, { language: 'csharp' })
    ]);

    this.badExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(badHtml));
    this.goodExampleCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(goodHtml));
    this.configCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(configHtml));
  }
}
