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
  selector: 'app-stream-actions',
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './stream-actions.component.html',
  styleUrl: './stream-actions.component.css'
})
export class StreamActionsComponent implements OnInit {
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly navItems: NavItem[] = [
    { id: 'overview', label: 'Overview', icon: 'info' },
    { id: 'action-types', label: 'Action Types', icon: 'category' },
    { id: 'preappend', label: 'Pre-Append Actions', icon: 'input' },
    { id: 'postappend', label: 'Post-Append Actions', icon: 'output' },
    { id: 'postcommit', label: 'Post-Commit Actions', icon: 'check_circle' },
    { id: 'registration', label: 'Registration', icon: 'app_registration' },
    { id: 'usecases', label: 'Use Cases', icon: 'lightbulb' }
  ];

  activeSection = signal<string>('overview');

  preAppendCodeHtml = signal<SafeHtml>('');
  postAppendCodeHtml = signal<SafeHtml>('');
  postCommitCodeHtml = signal<SafeHtml>('');
  registrationCodeHtml = signal<SafeHtml>('');
  validationCodeHtml = signal<SafeHtml>('');
  auditCodeHtml = signal<SafeHtml>('');

  private readonly preAppendCodeSample = `public class ValidationAction : IPreAppendAction
{
    public Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument)
        where T : class
    {
        // Validate the event before it's appended
        if (@event.EventType == "OrderCreated")
        {
            var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(@event.Payload);
            if (payload?.Amount <= 0)
            {
                throw new ValidationException("Order amount must be positive");
            }
        }

        // Return the data unchanged (or modify if needed)
        return () => data;
    }
}`;

  private readonly postAppendCodeSample = `public class MetricsAction : IPostAppendAction
{
    private readonly IMetricsService _metrics;

    public MetricsAction(IMetricsService metrics)
    {
        _metrics = metrics;
    }

    public Func<T> PostAppend<T>(T data, JsonEvent @event, IObjectDocument document)
        where T : class
    {
        // Track metrics after event is appended
        _metrics.IncrementCounter("events_appended", new Dictionary<string, string>
        {
            ["event_type"] = @event.EventType,
            ["object_name"] = document.ObjectName
        });

        return () => data;
    }
}`;

  private readonly postCommitCodeSample = `public class NotificationAction : IAsyncPostCommitAction
{
    private readonly INotificationService _notifications;

    public NotificationAction(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        // Send notifications after events are committed
        foreach (var evt in events)
        {
            if (evt.EventType == "OrderShipped")
            {
                await _notifications.SendAsync(new OrderShippedNotification
                {
                    OrderId = document.ObjectId,
                    EventVersion = evt.EventVersion
                });
            }
        }
    }
}`;

  private readonly registrationCodeSample = `// Register actions on the event stream
eventStream.RegisterAction(new ValidationAction());
eventStream.RegisterAction(new MetricsAction(metricsService));
eventStream.RegisterAction(new NotificationAction(notificationService));

// Or register via dependency injection
services.AddSingleton<IPreAppendAction, ValidationAction>();
services.AddSingleton<IPostAppendAction, MetricsAction>();
services.AddSingleton<IAsyncPostCommitAction, NotificationAction>();`;

  private readonly validationCodeSample = `public class BusinessRuleValidationAction : IPreAppendAction
{
    private readonly IBusinessRuleEngine _rules;

    public BusinessRuleValidationAction(IBusinessRuleEngine rules)
    {
        _rules = rules;
    }

    public Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument)
        where T : class
    {
        // Run business rules before appending
        var context = new ValidationContext
        {
            EventType = @event.EventType,
            Payload = @event.Payload,
            CurrentState = data,
            ObjectId = objectDocument.ObjectId
        };

        var result = _rules.Validate(context);
        if (!result.IsValid)
        {
            throw new BusinessRuleViolationException(result.Errors);
        }

        return () => data;
    }
}`;

  private readonly auditCodeSample = `public class AuditLogAction : IAsyncPostCommitAction
{
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentUserService _currentUser;

    public AuditLogAction(IAuditLogService auditLog, ICurrentUserService currentUser)
    {
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        var auditEntries = events.Select(evt => new AuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            UserId = _currentUser.UserId,
            Action = evt.EventType,
            ObjectType = document.ObjectName,
            ObjectId = document.ObjectId,
            EventVersion = evt.EventVersion,
            Payload = evt.Payload
        });

        await _auditLog.WriteEntriesAsync(auditEntries);
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
      preAppendHtml,
      postAppendHtml,
      postCommitHtml,
      registrationHtml,
      validationHtml,
      auditHtml
    ] = await Promise.all([
      this.codeHighlighter.highlight(this.preAppendCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.postAppendCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.postCommitCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.registrationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.validationCodeSample, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.auditCodeSample, { language: 'csharp' })
    ]);

    this.preAppendCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(preAppendHtml));
    this.postAppendCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(postAppendHtml));
    this.postCommitCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(postCommitHtml));
    this.registrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(registrationHtml));
    this.validationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(validationHtml));
    this.auditCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(auditHtml));
  }
}
