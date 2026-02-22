import { Component, OnInit, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatChipsModule } from '@angular/material/chips';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AdminApiService } from '../../core/services/admin-api.service';
import { CodeHighlighterService } from '../../core/services/code-highlighter.service';
import { ThemeService } from '../../core/services/theme.service';
import { forkJoin, of, catchError } from 'rxjs';

// Well-known schema versioning demo project IDs (must match DemoProjectIds.cs)
const SCHEMA_VERSION_PROJECT_IDS = {
  v1Only: '30000000-0000-0000-0000-000000000001',    // Enterprise CRM System
  v2Only: '30000000-0000-0000-0000-000000000002',    // Cloud Security Platform
  mixed: '30000000-0000-0000-0000-000000000003'      // DevOps Pipeline Modernization
};

interface SchemaVersionProject {
  id: string;
  name: string;
  description: string;
  schemaVersionType: 'v1-only' | 'v2-only' | 'mixed';
  memberEvents: MemberEvent[];
  totalEvents: number;
}

interface MemberEvent {
  memberId: string;
  role: string;
  schemaVersion: number;
  deserializationType: string;  // The C# record type used for deserialization (from EventTypeRegistry.Type.Name)
  permissions?: {
    canEdit: boolean;
    canDelete: boolean;
    canInvite: boolean;
    canManageWorkItems: boolean;
  };
  invitedBy: string;
  joinedAt: Date;
  eventVersion: number;
}

@Component({
  selector: 'app-event-versioning',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatDividerModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatChipsModule,
    MatButtonModule
  ],
  templateUrl: './event-versioning.component.html',
  styleUrl: './event-versioning.component.css'
})
export class EventVersioningComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);
  private readonly router = inject(Router);
  private readonly codeHighlighter = inject(CodeHighlighterService);
  private readonly themeService = inject(ThemeService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly noDataGenerated = signal(false);
  readonly demoProjects = signal<SchemaVersionProject[]>([]);

  // Highlighted code HTML signals
  readonly eventVersionAttributeCodeHtml = signal<SafeHtml>('');
  readonly foldMethodCodeHtml = signal<SafeHtml>('');
  readonly eventRegistrationCodeHtml = signal<SafeHtml>('');
  readonly whenHandlersCodeHtml = signal<SafeHtml>('');
  readonly jsonStorageCodeHtml = signal<SafeHtml>('');

  // Code samples for the documentation
  readonly eventVersionAttributeCode = `// Version 1 (implicit - no attribute needed)
[EventName("Project.MemberJoined")]
public record MemberJoinedProjectV1(
    string MemberId,
    string Role,
    string InvitedBy,
    DateTime JoinedAt);

// Version 2 (explicit - breaking change with permissions)
[EventName("Project.MemberJoined")]
[EventVersion(2)]
public record MemberJoinedProject(
    string MemberId,
    string Role,
    MemberPermissions Permissions,  // New field!
    string InvitedBy,
    DateTime JoinedAt);`;

  readonly foldMethodCode = `case "Project.MemberJoined":
    if (@event.SchemaVersion == 1)
        When(JsonEvent.To(@event,
            MemberJoinedProjectV1JsonSerializerContext
                .Default.MemberJoinedProjectV1));
    else
        When(JsonEvent.To(@event,
            MemberJoinedProjectJsonSerializerContext
                .Default.MemberJoinedProject));
    break;`;

  readonly eventRegistrationCode = `// V1 registration (schema version 1 - default)
Stream.RegisterEvent<MemberJoinedProjectV1>(
    "Project.MemberJoined",
    MemberJoinedProjectV1JsonSerializerContext
        .Default.MemberJoinedProjectV1);

// V2 registration (schema version 2)
Stream.RegisterEvent<MemberJoinedProject>(
    "Project.MemberJoined",
    2,  // Schema version parameter
    MemberJoinedProjectJsonSerializerContext
        .Default.MemberJoinedProject);`;

  readonly whenHandlersCode = `// Handle V1 (legacy) events
private void When(MemberJoinedProjectV1 @event)
{
    TeamMembers[UserId.From(@event.MemberId)] = @event.Role;
    // No permissions in V1
}

// Handle V2 (current) events with permissions
private void When(MemberJoinedProject @event)
{
    TeamMembers[UserId.From(@event.MemberId)] = @event.Role;
    // Permissions available in @event.Permissions
}`;

  readonly jsonStorageCode = `// V1 Event (no schemaVersion property - defaults to 1)
{
  "type": "Project.MemberJoined",
  "version": 5,
  "payload": "{\\"MemberId\\":\\"user-1\\",\\"Role\\":\\"Developer\\",...}"
}

// V2 Event (explicit schemaVersion: 2)
{
  "type": "Project.MemberJoined",
  "version": 8,
  "schemaVersion": 2,
  "payload": "{\\"MemberId\\":\\"user-2\\",\\"Role\\":\\"Lead\\",\\"Permissions\\":{...},...}"
}`;

  constructor() {
    effect(() => {
      // Re-highlight when theme changes
      this.themeService.theme();
      this.highlightCodeSamples();
    });
  }

  ngOnInit() {
    this.loadDemoData();
    this.highlightCodeSamples();
  }

  private async highlightCodeSamples(): Promise<void> {
    const [eventVersion, fold, registration, whenHandlers, jsonStorage] = await Promise.all([
      this.codeHighlighter.highlight(this.eventVersionAttributeCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.foldMethodCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.eventRegistrationCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.whenHandlersCode, { language: 'csharp' }),
      this.codeHighlighter.highlight(this.jsonStorageCode, { language: 'json' })
    ]);

    this.eventVersionAttributeCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(eventVersion));
    this.foldMethodCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(fold));
    this.eventRegistrationCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(registration));
    this.whenHandlersCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(whenHandlers));
    this.jsonStorageCodeHtml.set(this.sanitizer.bypassSecurityTrustHtml(jsonStorage));
  }

  private loadDemoData() {
    this.loading.set(true);
    this.error.set(null);
    this.noDataGenerated.set(false);

    // Fetch events for all three schema versioning demo projects
    forkJoin({
      v1Only: this.adminApi.getProjectEvents(SCHEMA_VERSION_PROJECT_IDS.v1Only).pipe(
        catchError(() => of([]))
      ),
      v2Only: this.adminApi.getProjectEvents(SCHEMA_VERSION_PROJECT_IDS.v2Only).pipe(
        catchError(() => of([]))
      ),
      mixed: this.adminApi.getProjectEvents(SCHEMA_VERSION_PROJECT_IDS.mixed).pipe(
        catchError(() => of([]))
      )
    }).subscribe({
      next: (results) => {
        // Check if we have any data
        const hasAnyData = results.v1Only.length > 0 || results.v2Only.length > 0 || results.mixed.length > 0;

        if (!hasAnyData) {
          this.noDataGenerated.set(true);
          this.loading.set(false);
          return;
        }

        // Log the transformed results showing schemaVersion is now present on all events
        console.log('Schema Versioning Demo - Loaded Events:');
        console.log('V1 Only Project Events:', results.v1Only.map(e => ({
          eventType: e.eventType,
          version: e.version,
          schemaVersion: e.schemaVersion,  // Now correctly shows 1 (defaulted by Zod schema)
          hasPermissions: !!e.data?.Permissions
        })));
        console.log('V2 Only Project Events:', results.v2Only.map(e => ({
          eventType: e.eventType,
          version: e.version,
          schemaVersion: e.schemaVersion,  // Now correctly shows 2 (from API response)
          hasPermissions: !!e.data?.Permissions
        })));
        console.log('Mixed Project Events:', results.mixed.map(e => ({
          eventType: e.eventType,
          version: e.version,
          schemaVersion: e.schemaVersion,  // Shows 1 or 2 depending on event
          hasPermissions: !!e.data?.Permissions
        })));

        const projects: SchemaVersionProject[] = [];

        // Process V1 Only project
        if (results.v1Only.length > 0) {
          projects.push(this.processProjectEvents(
            SCHEMA_VERSION_PROJECT_IDS.v1Only,
            'Enterprise CRM System',
            'Uses V1 MemberJoinedProject events (legacy, no permissions). Created 400+ days ago when permissions weren\'t tracked.',
            'v1-only',
            results.v1Only
          ));
        }

        // Process V2 Only project
        if (results.v2Only.length > 0) {
          projects.push(this.processProjectEvents(
            SCHEMA_VERSION_PROJECT_IDS.v2Only,
            'Cloud Security Platform',
            'Uses V2 MemberJoinedProject events (with permissions). Created recently with the new permission-aware member system.',
            'v2-only',
            results.v2Only
          ));
        }

        // Process Mixed project
        if (results.mixed.length > 0) {
          projects.push(this.processProjectEvents(
            SCHEMA_VERSION_PROJECT_IDS.mixed,
            'DevOps Pipeline Modernization',
            'Mixed: started with V1, later members added with V2. Shows real-world migration scenario where old and new events coexist.',
            'mixed',
            results.mixed
          ));
        }

        this.demoProjects.set(projects);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load schema versioning demo data:', err);
        this.error.set('Failed to load demo data. Please check your connection and try again.');
        this.loading.set(false);
      }
    });
  }

  private processProjectEvents(
    id: string,
    name: string,
    description: string,
    schemaVersionType: 'v1-only' | 'v2-only' | 'mixed',
    events: any[]
  ): SchemaVersionProject {
    const memberEvents: MemberEvent[] = [];

    for (const event of events) {
      if (event.eventType === 'Project.MemberJoined' && event.data) {
        const data = event.data;
        const schemaVersion = event.schemaVersion;  // Now always present from DomainEventSchema (defaults to 1)

        // Get the C# deserialization type from the API (looked up via EventTypeRegistry.TryGetByNameAndVersion)
        const deserializationType = event.deserializationType || `Unknown (v${schemaVersion})`;

        memberEvents.push({
          memberId: data.MemberId || data.memberId || 'unknown',
          role: data.Role || data.role || 'Unknown',
          schemaVersion: schemaVersion,
          deserializationType: deserializationType,
          permissions: schemaVersion === 2 && data.Permissions ? {
            canEdit: data.Permissions.CanEdit ?? data.Permissions.canEdit ?? false,
            canDelete: data.Permissions.CanDelete ?? data.Permissions.canDelete ?? false,
            canInvite: data.Permissions.CanInvite ?? data.Permissions.canInvite ?? false,
            canManageWorkItems: data.Permissions.CanManageWorkItems ?? data.Permissions.canManageWorkItems ?? false,
          } : undefined,
          invitedBy: data.InvitedBy || data.invitedBy || 'System',
          joinedAt: new Date(data.JoinedAt || data.joinedAt || event.timestamp),
          eventVersion: event.version
        });
      }
    }

    return {
      id,
      name,
      description,
      schemaVersionType,
      memberEvents,
      totalEvents: events.length
    };
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }

  goToGenerateDemoData() {
    this.router.navigate(['/demo-data']);
  }
}
