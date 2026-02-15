/**
 * Benchmark metadata provides explanations for each benchmark type and method.
 * This metadata is used to display human-readable descriptions in the benchmark viewer.
 *
 * When new benchmarks are added, add their metadata here to provide explanations.
 * If a benchmark doesn't have metadata, it will display with a generic description.
 */

export interface BenchmarkTypeMetadata {
  /** Short title for the benchmark class */
  title: string;
  /** Detailed description of what this benchmark class measures */
  description: string;
  /** Key insights or what to look for in the results */
  insights?: string[];
  /** What the user should take away from this benchmark */
  whatToLookFor?: string;
  /** Method-specific explanations */
  methods?: Record<string, BenchmarkMethodMetadata>;
}

export interface BenchmarkMethodMetadata {
  /** Short description of what this specific benchmark measures */
  description: string;
  /** What makes this benchmark important */
  importance?: string;
  /** Expected baseline or comparison notes */
  baseline?: string;
}

/**
 * Metadata for all benchmark types.
 * Keys should match the Type field from BenchmarkDotNet JSON output.
 */
export const BENCHMARK_METADATA: Record<string, BenchmarkTypeMetadata> = {
  // Event Type Registry Benchmarks
  'EventTypeRegistryBenchmarks': {
    title: 'Event Type Registry Lookups',
    description: 'The event type registry maps event names (like "Order.Created") to their .NET types for deserialization. This benchmark compares two registry implementations: FrozenRegistry (uses FrozenDictionary, optimized for read-heavy workloads) vs MutableRegistry (uses ConcurrentDictionary, allows runtime modifications).',
    whatToLookFor: 'FrozenRegistry should be significantly faster for lookups. The "SingleLookup" methods show the raw dictionary access speed, while "TryGetByName" and "TryGetByType" include the full lookup logic. All operations should have zero allocations.',
    insights: [
      'FrozenRegistry is 3-4x faster for single lookups due to better cache locality',
      'All lookup operations have zero heap allocations',
      'Performance gap widens as the number of registered types increases',
      'Use FrozenRegistry in production, MutableRegistry only during startup for registration'
    ],
    methods: {
      'FrozenRegistry_SingleLookup': {
        description: 'Direct dictionary lookup in FrozenDictionary',
        importance: 'Shows raw lookup speed without any business logic'
      },
      'MutableRegistry_SingleLookup': {
        description: 'Direct dictionary lookup in ConcurrentDictionary',
        importance: 'Baseline for comparison with frozen variant'
      },
      'FrozenRegistry_TryGetByType': {
        description: 'Full lookup by CLR Type (used during serialization)',
        importance: 'Hot path - called for every event being serialized'
      },
      'MutableRegistry_TryGetByType': {
        description: 'Full lookup by CLR Type using mutable registry'
      },
      'FrozenRegistry_TryGetByName': {
        description: 'Full lookup by event name string (used during deserialization)',
        importance: 'Hot path - called for every event being deserialized'
      },
      'MutableRegistry_TryGetByName': {
        description: 'Full lookup by event name string using mutable registry'
      },
      'FrozenRegistry_TryGetByNameAndVersion': {
        description: 'Lookup by name + schema version for event upcasting',
        importance: 'Used when loading events that may need schema migration'
      },
      'MutableRegistry_TryGetByNameAndVersion': {
        description: 'Versioned lookup using mutable registry'
      }
    }
  },

  // Version Token Benchmarks
  'VersionTokenBenchmarks': {
    title: 'Version Token Parsing',
    description: 'VersionTokens encode stream identity and version (e.g., "workitem:abc123:42"). They are used for optimistic concurrency control and event ordering. This benchmark measures parsing and property access performance.',
    whatToLookFor: 'Property accessors (GetObjectIdentifier, GetVersion) should be under 10ns as they are called frequently. Token parsing is less frequent but should still be fast. String creation allocates memory.',
    insights: [
      'Property access is ultra-fast (< 10ns) as tokens are parsed once and cached',
      'ParseShortToken is faster than ParseLongToken due to less string processing',
      'ToVersionTokenString allocates a new string, use sparingly in hot paths'
    ],
    methods: {
      'ParseShortToken': {
        description: 'Parses compact format: "objectId:version"',
        importance: 'Common format for internal references'
      },
      'ParseLongToken': {
        description: 'Parses full format: "objectName:objectId:version"',
        importance: 'Used for cross-aggregate references'
      },
      'ToVersionTokenString': {
        description: 'Converts token to string representation',
        importance: 'Allocates memory - avoid in hot paths'
      },
      'GetObjectIdentifier': {
        description: 'Extracts object ID from parsed token',
        importance: 'Ultra-fast property access (< 10ns)'
      },
      'GetVersion': {
        description: 'Extracts version number from parsed token',
        importance: 'Ultra-fast property access (< 10ns)'
      }
    }
  },

  // Event Stream Benchmarks
  'EventStreamBenchmarks': {
    title: 'Event Stream Operations',
    description: 'Core event stream operations using InMemory storage to isolate stream logic from I/O. Measures appending events (write path) and reading events (load path).',
    whatToLookFor: 'Compare materialized reads (all events loaded into memory at once) vs streaming reads (IAsyncEnumerable). Streaming has slight overhead but uses constant memory regardless of event count.',
    insights: [
      'AppendEvents includes serialization overhead',
      'StreamEventsAsync is memory-efficient for large event streams',
      'Performance scales linearly O(n) with event count'
    ],
    methods: {
      'AppendEvents': {
        description: 'Appends events to a stream (serialization + write)',
        importance: 'Core write path - called on every aggregate save'
      },
      'ReadEvents': {
        description: 'Reads all events from a stream into memory',
        importance: 'Used when loading aggregates'
      },
      'StreamEventsAsync': {
        description: 'Streams events via IAsyncEnumerable',
        importance: 'Preferred for large event streams - constant memory usage'
      }
    }
  },

  // InMemory DataStore Benchmarks
  'InMemoryDataStoreBenchmarks': {
    title: 'InMemory Storage Provider',
    description: 'Performance characteristics of the InMemory storage provider (used for testing and development). Compares different read strategies and shows how performance scales with event count.',
    whatToLookFor: 'MaterializedRead should be fastest for complete reads. StreamingRead shines when you need early exit or have memory constraints. The EventCount parameter shows scaling behavior.',
    insights: [
      'Materialized reads are fastest when you need all events',
      'Streaming reads have setup overhead but constant memory usage',
      'Early exit with streaming saves work when only partial data needed',
      'O(n) scaling with event count'
    ],
    methods: {
      'ReadAllEvents': {
        description: 'Reads all events using materialized list',
        baseline: 'Baseline for complete read performance'
      },
      'ReadAllEvents_Streaming': {
        description: 'Reads all events via IAsyncEnumerable',
        importance: 'Shows streaming overhead vs materialized'
      },
      'ReadFirstEvent_Materialized': {
        description: 'Reads all events but only uses the first',
        importance: 'Shows wasted work with materialized approach'
      },
      'ReadFirstEvent_Streaming': {
        description: 'Uses streaming with early break after first event',
        importance: 'Demonstrates streaming efficiency for partial reads'
      }
    }
  },

  // Event Upcaster Benchmarks
  'EventUpcasterBenchmarks': {
    title: 'Event Schema Evolution (Upcasting)',
    description: 'Measures the performance of transforming old event versions to new versions during deserialization. Upcasting enables schema evolution without data migration.',
    whatToLookFor: 'NoUpcastNeeded should have minimal overhead (just a type check). Single upcasts are fast. Chains of upcasters (v1->v2->v3) show cumulative overhead.',
    insights: [
      'Upcasting is applied lazily during event read, not stored',
      'No-op scenarios (current version) have minimal overhead',
      'Chain length affects performance linearly'
    ],
    methods: {
      'UpcastSingleEvent': {
        description: 'Transforms one event from v1 to v2 schema'
      },
      'UpcastChain': {
        description: 'Applies multiple upcasters: v1 -> v2 -> v3',
        importance: 'Shows cumulative overhead of version jumps'
      },
      'NoUpcastNeeded': {
        description: 'Processes event already at current version',
        importance: 'Should show minimal overhead (version check only)'
      }
    }
  },

  // Aggregate Fold Benchmarks
  'AggregateFoldBenchmarks': {
    title: 'Aggregate State Reconstruction',
    description: 'Measures the core event sourcing operation: replaying (folding) events to reconstruct aggregate state. This is what happens when you load an aggregate.',
    whatToLookFor: 'Fold operations should be very fast as they just apply events to in-memory state. Memory allocations should be minimal (only state changes, not new objects per event).',
    insights: [
      'Fold performance directly impacts aggregate load time',
      'Generated code eliminates reflection overhead',
      'Snapshots can skip replaying old events'
    ],
    methods: {
      'FoldSingleEvent': {
        description: 'Applies one event to aggregate state'
      },
      'FoldMultipleEvents': {
        description: 'Applies a sequence of events in order'
      },
      'FoldFromSnapshot': {
        description: 'Starts from snapshot, folds only new events',
        importance: 'Shows benefit of snapshotting for old aggregates'
      }
    }
  },

  // Session Benchmarks
  'SessionBenchmarks': {
    title: 'Session and Transaction Operations',
    description: 'The Session abstraction provides transaction-like semantics: you append events, then commit. This benchmark measures the overhead of session management.',
    whatToLookFor: 'Session creation should be lightweight. Commit includes version validation and persistence. EventCount parameter shows how session overhead scales.',
    insights: [
      'Sessions provide optimistic concurrency control',
      'Commit validates version and persists atomically',
      'Rollback is fast (just discards in-memory changes)'
    ]
  },

  // JSON Serialization Benchmarks
  'JsonSerializeBenchmarks': {
    title: 'JSON Serialization: Source-Gen vs Reflection',
    description: 'Compares JSON serialization (object to string) using two approaches: Source-Generated (compile-time, AOT-compatible, used by this library) vs Reflection-Based (runtime, traditional). Each payload size has its own baseline for fair comparison.',
    whatToLookFor: 'For each PayloadSize, SourceGen should be faster than Reflection. The ratio shows how much faster/slower Reflection is compared to SourceGen for that specific payload size.',
    insights: [
      'Source-Gen serializers are 15-25% faster than reflection',
      'The performance gap is consistent across payload sizes',
      'Source-Gen is required for Native AOT compilation',
      'Memory allocations are similar between approaches'
    ],
    methods: {
      'SourceGen': {
        description: 'Serializes using compile-time generated JsonSerializerContext',
        importance: 'Required for Native AOT support',
        baseline: 'Baseline for each payload size'
      },
      'Reflection': {
        description: 'Serializes using runtime reflection',
        importance: 'Compare to SourceGen to see the benefit'
      }
    }
  },

  // JSON Deserialization Benchmarks
  'JsonDeserializeBenchmarks': {
    title: 'JSON Deserialization: Source-Gen vs Reflection',
    description: 'Compares JSON deserialization (string to object) using two approaches: Source-Generated vs Reflection-Based. Each payload size has its own baseline for fair comparison.',
    whatToLookFor: 'For each PayloadSize, compare SourceGen (baseline) to Reflection. The performance difference is typically smaller for deserialization than serialization.',
    insights: [
      'Deserialization performance is often similar between approaches',
      'Large payloads show more variation due to object allocation patterns',
      'Source-Gen avoids runtime type resolution overhead'
    ],
    methods: {
      'SourceGen': {
        description: 'Deserializes using compile-time generated code',
        baseline: 'Baseline for each payload size'
      },
      'Reflection': {
        description: 'Deserializes using runtime reflection'
      }
    }
  },

  // Event Processing Benchmarks
  'EventProcessingBenchmarks': {
    title: 'Event Processing Pipeline',
    description: 'Measures the full event processing path used by this library. Compares raw deserialization (just the payload) vs ToEvent which includes creating the IEvent wrapper with metadata.',
    whatToLookFor: 'ToEventWithMetadata adds minimal overhead over RawDeserialize. The wrapper allocation is the main cost. PayloadSize shows how payload complexity affects processing.',
    insights: [
      'IEvent wrapper adds minimal overhead',
      'Most time is spent in JSON deserialization',
      'Wrapper provides type safety and metadata access'
    ],
    methods: {
      'RawDeserialize': {
        description: 'Deserializes just the event payload',
        baseline: 'Baseline for raw deserialization speed'
      },
      'ToEventWithMetadata': {
        description: 'Deserializes and wraps in IEvent with metadata',
        importance: 'What actually happens when loading events'
      }
    }
  },

  // Snapshot Benchmarks
  'SnapshotBenchmarks': {
    title: 'Snapshot Performance',
    description: 'Snapshots store aggregate state at a point in time, reducing the number of events that need to be replayed. This benchmark shows the trade-off between snapshot creation cost and load time savings.',
    whatToLookFor: 'LoadWithSnapshot should be faster than LoadWithoutSnapshot, especially as event count increases. CreateSnapshot has serialization overhead but pays off over many loads.',
    insights: [
      'Snapshots trade storage space for faster load times',
      'Benefit increases with aggregate age (more events to skip)',
      'Snapshot creation has serialization overhead',
      'Consider snapshotting aggregates with 100+ events'
    ],
    methods: {
      'LoadWithoutSnapshot': {
        description: 'Replays all events from the beginning',
        importance: 'O(n) with event count - gets slower over time'
      },
      'LoadWithSnapshot': {
        description: 'Loads from snapshot, replays only new events',
        importance: 'O(k) where k << n, much faster for old aggregates'
      },
      'CreateSnapshot': {
        description: 'Serializes current aggregate state to storage',
        importance: 'One-time cost that amortizes over many reads'
      }
    }
  }
};

/**
 * Gets metadata for a benchmark type, returning a default if not found.
 */
export function getBenchmarkMetadata(typeName: string): BenchmarkTypeMetadata {
  // Try exact match
  if (BENCHMARK_METADATA[typeName]) {
    return BENCHMARK_METADATA[typeName];
  }

  // Try matching by suffix (e.g., "ErikLieben.FA.ES.Benchmarks.Registry.EventTypeRegistryBenchmarks" -> "EventTypeRegistryBenchmarks")
  const shortName = typeName.split('.').pop() || typeName;
  if (BENCHMARK_METADATA[shortName]) {
    return BENCHMARK_METADATA[shortName];
  }

  // Return default metadata
  return {
    title: shortName,
    description: `Performance benchmarks for ${shortName}`,
    insights: []
  };
}

/**
 * Gets method-specific metadata
 */
export function getMethodMetadata(typeName: string, methodName: string): BenchmarkMethodMetadata | undefined {
  const typeMetadata = getBenchmarkMetadata(typeName);
  return typeMetadata.methods?.[methodName];
}
