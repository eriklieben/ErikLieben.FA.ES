import { DomainEventSchema, DomainEvent } from './admin.contracts';

describe('DomainEventSchema', () => {
  describe('schemaVersion', () => {
    it('should default schemaVersion to 1 when not present in the input', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: { foo: 'bar' },
        version: 1,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.schemaVersion).toBe(1);
    });

    it('should preserve schemaVersion when present in the input', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: { foo: 'bar' },
        version: 1,
        schemaVersion: 2,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.schemaVersion).toBe(2);
    });

    it('should handle schemaVersion of 1 explicitly set', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: { foo: 'bar' },
        version: 1,
        schemaVersion: 1,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.schemaVersion).toBe(1);
    });

    it('should handle higher schema versions', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: { foo: 'bar' },
        version: 1,
        schemaVersion: 5,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.schemaVersion).toBe(5);
    });
  });

  describe('parsing', () => {
    it('should parse a complete DomainEvent', () => {
      // Arrange
      const input = {
        eventType: 'Project.MemberJoined',
        timestamp: '2024-01-01T12:00:00Z',
        data: {
          MemberId: 'user-123',
          Role: 'Developer'
        },
        version: 3,
        schemaVersion: 2,
        metadata: { correlationId: 'abc-123' }
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.eventType).toBe('Project.MemberJoined');
      expect(result.version).toBe(3);
      expect(result.schemaVersion).toBe(2);
      expect(result.data.MemberId).toBe('user-123');
    });

    it('should transform timestamp to ISO string', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: new Date('2024-01-01T12:00:00Z'),
        data: {},
        version: 1
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(typeof result.timestamp).toBe('string');
      expect(result.timestamp).toContain('2024-01-01');
    });

    it('should make metadata optional', () => {
      // Arrange
      const input = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: {},
        version: 1
      };

      // Act
      const result = DomainEventSchema.parse(input);

      // Assert
      expect(result.metadata).toBeUndefined();
    });
  });

  describe('type inference', () => {
    it('should have schemaVersion in the DomainEvent type', () => {
      // This test verifies that the TypeScript type includes schemaVersion
      const event: DomainEvent = {
        eventType: 'TestEvent',
        timestamp: '2024-01-01T00:00:00Z',
        data: {},
        version: 1,
        schemaVersion: 2
      };

      expect(event.schemaVersion).toBe(2);
    });
  });

  describe('API response transformation', () => {
    it('should transform v1 API response (without schemaVersion) to object with schemaVersion=1', () => {
      // Simulates: Backend returns v1 event without schemaVersion property (storage optimization)
      const apiResponse = {
        eventType: 'Project.MemberJoined',
        timestamp: '2025-04-20T11:03:31.760Z',
        data: {
          MemberId: 'd224b420-d78c-482a-816a-555770fe265c',
          Role: 'DevOps',
          InvitedBy: '49f58068-b756-4d55-a595-87d3cf307dea',
          JoinedAt: '2025-04-20T11:03:31.7609135Z'
        },
        version: 2,
        metadata: {}
        // Note: schemaVersion is NOT present (v1 events don't store it)
      };

      // Act - This is what AdminApiService does when loading events
      const result = DomainEventSchema.parse(apiResponse);

      // Assert - schemaVersion should be defaulted to 1
      expect(result.schemaVersion).toBe(1);
      expect(result.eventType).toBe('Project.MemberJoined');
      expect(result.version).toBe(2); // stream version (position in stream)
    });

    it('should transform v2 API response (with schemaVersion) to object preserving schemaVersion=2', () => {
      // Simulates: Backend returns v2 event WITH schemaVersion property
      const apiResponse = {
        eventType: 'Project.MemberJoined',
        timestamp: '2025-04-20T11:03:31.760Z',
        data: {
          MemberId: 'd224b420-d78c-482a-816a-555770fe265c',
          Role: 'DevOps',
          InvitedBy: '49f58068-b756-4d55-a595-87d3cf307dea',
          JoinedAt: '2025-04-20T11:03:31.7609135Z',
          Permissions: {
            CanEdit: true,
            CanDelete: false,
            CanInvite: true,
            CanManageWorkItems: true
          }
        },
        version: 3,
        schemaVersion: 2, // v2 events include schemaVersion
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(apiResponse);

      // Assert
      expect(result.schemaVersion).toBe(2);
      expect(result.data.Permissions).toBeDefined();
      expect(result.data.Permissions.CanEdit).toBe(true);
    });

    it('should transform mixed API response array with correct schemaVersions', () => {
      // Simulates: Loading events from a project with mixed v1 and v2 events
      const apiResponseArray = [
        // Event 1: v1 schema (no schemaVersion in response)
        {
          eventType: 'Project.Created',
          timestamp: '2025-04-20T10:00:00.000Z',
          data: { ProjectId: 'proj-1', Name: 'Test Project' },
          version: 1,
          metadata: {}
        },
        // Event 2: v1 schema member joined
        {
          eventType: 'Project.MemberJoined',
          timestamp: '2025-04-20T11:00:00.000Z',
          data: { MemberId: 'user-1', Role: 'Developer' },
          version: 2,
          metadata: {}
        },
        // Event 3: v2 schema member joined (with Permissions)
        {
          eventType: 'Project.MemberJoined',
          timestamp: '2025-04-20T12:00:00.000Z',
          data: {
            MemberId: 'user-2',
            Role: 'Admin',
            Permissions: { CanEdit: true, CanDelete: true }
          },
          version: 3,
          schemaVersion: 2,
          metadata: {}
        }
      ];

      // Act - Transform each event (simulating what AdminApiService.getProjectEvents does)
      const results = apiResponseArray.map(item => DomainEventSchema.parse(item));

      // Assert - Each event should have the correct schemaVersion
      expect(results[0].schemaVersion).toBe(1); // v1 event defaults to 1
      expect(results[1].schemaVersion).toBe(1); // v1 event defaults to 1
      expect(results[2].schemaVersion).toBe(2); // v2 event preserves schemaVersion

      // Verify we can distinguish v1 from v2 by schemaVersion
      const v1Events = results.filter(e => e.schemaVersion === 1);
      const v2Events = results.filter(e => e.schemaVersion === 2);
      expect(v1Events.length).toBe(2);
      expect(v2Events.length).toBe(1);
    });

    it('should allow downstream code to use schemaVersion for conditional logic', () => {
      // Simulates: How event-versioning component uses schemaVersion
      const events = [
        { eventType: 'Project.MemberJoined', timestamp: '2025-04-20T10:00:00Z', data: { Role: 'Dev' }, version: 1, metadata: {} },
        { eventType: 'Project.MemberJoined', timestamp: '2025-04-20T11:00:00Z', data: { Role: 'Admin', Permissions: { CanEdit: true } }, version: 2, schemaVersion: 2, metadata: {} }
      ].map(e => DomainEventSchema.parse(e));

      // Act - Use schemaVersion for conditional rendering (like event-versioning component does)
      const processedEvents = events.map(event => {
        const hasPermissions = event.schemaVersion === 2 && event.data.Permissions;
        return {
          role: event.data.Role,
          schemaVersion: event.schemaVersion,
          permissions: hasPermissions ? event.data.Permissions : undefined
        };
      });

      // Assert
      expect(processedEvents[0].schemaVersion).toBe(1);
      expect(processedEvents[0].permissions).toBeUndefined();
      expect(processedEvents[1].schemaVersion).toBe(2);
      expect(processedEvents[1].permissions).toEqual({ CanEdit: true });
    });
  });

  describe('deserializationType', () => {
    it('should preserve deserializationType when present in the API response', () => {
      // Simulates: Backend returns the C# type name from EventTypeRegistry.Type.Name
      const apiResponse = {
        eventType: 'Project.MemberJoined',
        timestamp: '2025-04-20T11:03:31.760Z',
        data: { MemberId: 'user-1', Role: 'Developer' },
        version: 2,
        schemaVersion: 1,
        deserializationType: 'MemberJoinedProjectV1',  // From eventTypeRegistry.TryGetByNameAndVersion()
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(apiResponse);

      // Assert - deserializationType should be preserved
      expect(result.deserializationType).toBe('MemberJoinedProjectV1');
    });

    it('should handle null deserializationType', () => {
      // Simulates: Event type not found in registry
      const apiResponse = {
        eventType: 'Unknown.Event',
        timestamp: '2025-04-20T11:03:31.760Z',
        data: {},
        version: 1,
        schemaVersion: 1,
        deserializationType: null,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(apiResponse);

      // Assert
      expect(result.deserializationType).toBeNull();
    });

    it('should handle missing deserializationType (undefined)', () => {
      // Simulates: Older API responses without deserializationType
      const apiResponse = {
        eventType: 'Project.MemberJoined',
        timestamp: '2025-04-20T11:03:31.760Z',
        data: { MemberId: 'user-1' },
        version: 1,
        metadata: {}
      };

      // Act
      const result = DomainEventSchema.parse(apiResponse);

      // Assert
      expect(result.deserializationType).toBeUndefined();
    });

    it('should show different types for same event with different schema versions', () => {
      // Simulates: Project.MemberJoined has two versions with different C# types
      const events = [
        {
          eventType: 'Project.MemberJoined',
          timestamp: '2025-04-20T10:00:00Z',
          data: { MemberId: 'user-1', Role: 'Dev' },
          version: 1,
          schemaVersion: 1,
          deserializationType: 'MemberJoinedProjectV1',  // V1 type
          metadata: {}
        },
        {
          eventType: 'Project.MemberJoined',
          timestamp: '2025-04-20T11:00:00Z',
          data: { MemberId: 'user-2', Role: 'Admin', Permissions: { CanEdit: true } },
          version: 2,
          schemaVersion: 2,
          deserializationType: 'MemberJoinedProject',  // V2 type (no V1 suffix)
          metadata: {}
        }
      ].map(e => DomainEventSchema.parse(e));

      // Assert - Same eventType but different C# deserialization types
      expect(events[0].eventType).toBe('Project.MemberJoined');
      expect(events[1].eventType).toBe('Project.MemberJoined');
      expect(events[0].deserializationType).toBe('MemberJoinedProjectV1');
      expect(events[1].deserializationType).toBe('MemberJoinedProject');
    });
  });
});
