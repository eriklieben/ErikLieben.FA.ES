import { z } from 'zod';

// Event schemas
export const DomainEventSchema = z.object({
  eventType: z.string(),
  timestamp: z.coerce.date().transform(d => d.toISOString()),
  data: z.any(),
  version: z.number(),
  schemaVersion: z.number().default(1),  // Schema version of the event payload (defaults to 1 for legacy events)
  deserializationType: z.string().nullable().optional(),  // The C# record type used for deserialization (from EventTypeRegistry)
  metadata: z.any().optional(),
});
export type DomainEvent = z.infer<typeof DomainEventSchema>;

// Time travel schemas
export const WorkItemVersionStateSchema = z.object({
  workItemId: z.string(),
  version: z.number(),
  currentVersion: z.number(),
  state: z.object({
    projectId: z.string(),
    title: z.string(),
    description: z.string(),
    priority: z.string(),
    status: z.string(),
    assignedTo: z.string().nullable(),
    deadline: z.string().datetime().nullable(),
    estimatedHours: z.number().nullable(),
    tags: z.array(z.string()),
    commentCount: z.number(),
  }),
  events: z.array(DomainEventSchema),
});
export type WorkItemVersionState = z.infer<typeof WorkItemVersionStateSchema>;

export const ProjectVersionStateSchema = z.object({
  projectId: z.string(),
  version: z.number(),
  currentVersion: z.number(),
  state: z.object({
    name: z.string(),
    description: z.string(),
    ownerId: z.string(),
    isCompleted: z.boolean(),
    outcome: z.number(),
    teamMembers: z.record(z.string()),
    workItemCounts: z.object({
      planned: z.number(),
      inProgress: z.number(),
      completed: z.number(),
      total: z.number(),
    }),
  }),
  events: z.array(DomainEventSchema),
});
export type ProjectVersionState = z.infer<typeof ProjectVersionStateSchema>;

// Enriched version with user profile data for on-demand projection
export const EnrichedProjectVersionStateSchema = z.object({
  projectId: z.string(),
  version: z.number(),
  currentVersion: z.number(),
  state: z.object({
    name: z.string(),
    description: z.string(),
    owner: z.object({
      userId: z.string(),
      name: z.string(),
      role: z.string(),
    }),
    isCompleted: z.boolean(),
    outcome: z.number(),
    teamMembers: z.array(z.object({
      userId: z.string(),
      name: z.string(),
      role: z.string(),
    })),
    workItemCounts: z.object({
      planned: z.number(),
      inProgress: z.number(),
      completed: z.number(),
      total: z.number(),
    }),
  }),
  events: z.array(DomainEventSchema),
});
export type EnrichedProjectVersionState = z.infer<typeof EnrichedProjectVersionStateSchema>;

// Snapshot schemas
export const SnapshotResultSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  workItemId: z.string().optional(),
  projectId: z.string().optional(),
  version: z.number(),
});
export type SnapshotResult = z.infer<typeof SnapshotResultSchema>;

// Seed data schemas
export const SeedDataResultSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  projectsCreated: z.number(),
  workItemsCreated: z.number(),
  projects: z.array(z.string()),
  timeTravelProject: z.object({
    id: z.string(),
    name: z.string(),
    description: z.string(),
    eventCount: z.number(),
    outcomes: z.array(z.object({
      version: z.number(),
      outcome: z.string(),
      eventType: z.string(),
    })),
  }).optional(),
  legacyEventDemo: z.object({
    description: z.string(),
    projects: z.array(z.object({
      id: z.string(),
      name: z.string(),
      legacyEventType: z.string(),
      upcastsTo: z.string(),
      outcome: z.string(),
    })),
  }).optional(),
  newEventDemo: z.object({
    description: z.string(),
    projects: z.array(z.object({
      id: z.string(),
      name: z.string(),
      eventType: z.string(),
      outcome: z.string(),
      enumValue: z.string(),
    })),
  }).optional(),
});
export type SeedDataResult = z.infer<typeof SeedDataResultSchema>;

// Aggregate list for dropdowns
export const AggregateInfoSchema = z.object({
  id: z.string(),
  name: z.string(),
});
export type AggregateInfo = z.infer<typeof AggregateInfoSchema>;

// Storage connection info
export const StorageAccountSchema = z.object({
  connectionString: z.string(),
  connectionName: z.string(),
  containers: z.string(),
  isAzurite: z.boolean(),
});
export type StorageAccount = z.infer<typeof StorageAccountSchema>;

export const StorageConnectionSchema = z.object({
  storageAccounts: z.array(StorageAccountSchema),
  instructions: z.string(),
});
export type StorageConnection = z.infer<typeof StorageConnectionSchema>;

// Event Upcasting Demonstration
export const EventSummarySchema = z.object({
  EventType: z.string(),
  Timestamp: z.coerce.date(),
  EventStreamVersion: z.number(),
  Summary: z.string().nullable(),
});
export type EventSummary = z.infer<typeof EventSummarySchema>;

// Map from C# enum integers to string names
const outcomeMap: Record<number, string> = {
  0: 'None',
  1: 'Successful',
  2: 'Cancelled',
  3: 'Failed',
  4: 'Delivered',
  5: 'Suspended'
};

export const ProjectOutcomeSchema = z.number().transform(val => outcomeMap[val] || 'None');
export type ProjectOutcome = 'None' | 'Successful' | 'Cancelled' | 'Failed' | 'Delivered' | 'Suspended';

export const UpcastingDemoProjectSchema = z.object({
  ProjectId: z.string(),
  Name: z.string(),
  InitiatedAt: z.coerce.date(),
  IsCompleted: z.boolean(),
  CompletedAt: z.coerce.date().nullable(),
  EventType: z.string().nullable(),
  Outcome: ProjectOutcomeSchema,
  CompletionMessage: z.string().nullable(),
  IsLegacyEvent: z.boolean(),
  EventStreamSummaryStart: z.array(EventSummarySchema),
  EventStreamSummaryLast: EventSummarySchema.nullable(),
  TotalEventCount: z.number(),
});
export type UpcastingDemoProject = z.infer<typeof UpcastingDemoProjectSchema>;

export const EventUpcastingDemonstrationSchema = z.object({
  DemoProjects: z.record(UpcastingDemoProjectSchema),
});
export type EventUpcastingDemonstration = z.infer<typeof EventUpcastingDemonstrationSchema>;

// Stream Migration Contracts
export const MigrationPhaseSchema = z.enum(['idle', 'normal', 'replicating', 'tailing', 'cutover', 'complete']);
export type MigrationPhase = z.infer<typeof MigrationPhaseSchema>;

export const StreamEventSchema = z.object({
  id: z.string(),
  version: z.number(),
  type: z.string(),
  timestamp: z.coerce.date(),
  data: z.record(z.unknown()),
  schemaVersion: z.number(),
  isLiveEvent: z.boolean().optional(),
  writtenTo: z.array(z.enum(['source', 'target'])).optional(),
});
export type StreamEvent = z.infer<typeof StreamEventSchema>;

export const TransformationRuleSchema = z.object({
  eventType: z.string(),
  fromVersion: z.number(),
  toVersion: z.number(),
  changes: z.array(z.string()),
});
export type TransformationRule = z.infer<typeof TransformationRuleSchema>;

export const MigrationDemoStateSchema = z.object({
  migrationId: z.string().nullable(),
  phase: MigrationPhaseSchema,
  sourceStreamId: z.string(),
  targetStreamId: z.string().nullable(),
  sourceEvents: z.array(StreamEventSchema),
  targetEvents: z.array(StreamEventSchema),
  eventsProcessed: z.number(),
  totalEvents: z.number(),
  progress: z.number(),
  transformations: z.array(TransformationRuleSchema),
});
export type MigrationDemoState = z.infer<typeof MigrationDemoStateSchema>;

export const MigrationStartResponseSchema = z.object({
  success: z.boolean(),
  migrationId: z.string(),
  message: z.string(),
  initialState: MigrationDemoStateSchema,
});
export type MigrationStartResponse = z.infer<typeof MigrationStartResponseSchema>;

export const LiveEventResponseSchema = z.object({
  success: z.boolean(),
  sourceEvent: StreamEventSchema.nullable(),
  targetEvent: StreamEventSchema.nullable(),
  phase: MigrationPhaseSchema,
  message: z.string(),
});
export type LiveEventResponse = z.infer<typeof LiveEventResponseSchema>;

export const MigrationStatusResponseSchema = z.object({
  migrationId: z.string(),
  phase: MigrationPhaseSchema,
  eventsProcessed: z.number(),
  totalEvents: z.number(),
  progress: z.number(),
  sourceEvents: z.array(StreamEventSchema),
  targetEvents: z.array(StreamEventSchema),
});
export type MigrationStatusResponse = z.infer<typeof MigrationStatusResponseSchema>;

// Audit Log types
export const AuditLogEntrySchema = z.object({
  id: z.string(),
  workItemId: z.string(),
  eventType: z.string().nullable(),
  eventData: z.any().nullable(),
  occurredAt: z.coerce.date().nullable(),
  userId: z.string().nullable(),
  userName: z.string().nullable(),
  schemaVersion: z.number().optional(),
  description: z.string().nullable().optional(),
  details: z.any().nullable().optional(),
});
export type AuditLogEntry = z.infer<typeof AuditLogEntrySchema>;

export const AuditLogResponseSchema = z.object({
  workItemId: z.string(),
  entries: z.array(AuditLogEntrySchema),
  storageType: z.string(),
  containerName: z.string(),
  message: z.string().nullable().optional(),
});
export type AuditLogResponse = z.infer<typeof AuditLogResponseSchema>;

// Reporting Index types
export const ReportingIndexItemSchema = z.object({
  partitionKey: z.string(),
  rowKey: z.string(),
  workItemId: z.string(),
  projectId: z.string(),
  title: z.string().nullable(),
  status: z.string().nullable(),
  priority: z.string().nullable(),
  assignedTo: z.string().nullable(),
  lastUpdatedAt: z.coerce.date().nullable(),
});
export type ReportingIndexItem = z.infer<typeof ReportingIndexItemSchema>;

export const ReportingIndexResponseSchema = z.object({
  items: z.array(ReportingIndexItemSchema),
  storageType: z.string(),
  tableName: z.string(),
});
export type ReportingIndexResponse = z.infer<typeof ReportingIndexResponseSchema>;

// Projection status types
export const ProjectionStatusSchema = z.object({
  name: z.string(),
  storageType: z.string().optional(),
  status: z.string(),
  projectionStatus: z.string().optional(),
  schemaVersion: z.number().optional(),
  codeSchemaVersion: z.number().optional(),
  needsSchemaUpgrade: z.boolean().optional(),
  lastUpdate: z.string().nullable(),
  checkpoint: z.number(),
  checkpointFingerprint: z.string(),
  isPersisted: z.boolean().optional(),
  lastGenerationDurationMs: z.number().nullable().optional(),
});
export type ProjectionStatus = z.infer<typeof ProjectionStatusSchema>;
