using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.InMemory;
using NSubstitute;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.Benchmarks.Snapshots;

/// <summary>
/// Benchmarks comparing aggregate loading performance WITH and WITHOUT snapshots.
/// This demonstrates the key value proposition of snapshotting for event sourcing.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Snapshots", "Performance")]
public class SnapshotLoadBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemorySnapShotStore _snapshotStore = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;
    private IObjectDocument _documentWithSnapshot = null!;
    private IObjectDocument _documentWithoutSnapshot = null!;
    private IEvent[] _allEvents = null!;

    [Params(100, 500)]
    public int EventCount { get; set; }

    [Params(50)]
    public int SnapshotAtVersion { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dataStore = new InMemoryDataStore();
        _snapshotStore = new InMemorySnapShotStore();
        _documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());

        // Create events
        _allEvents = CreateEvents(EventCount);

        // Setup document WITH snapshot
        _documentWithSnapshot = _documentFactory.GetOrCreateAsync("benchmark", $"with-snapshot").Result;
        _dataStore.AppendAsync(_documentWithSnapshot, default, _allEvents).Wait();

        // Create snapshot at SnapshotAtVersion
        var snapshotAggregate = new CounterAggregate();
        snapshotAggregate.ApplyEventsUpTo(_allEvents.Take(SnapshotAtVersion).ToArray());
        _snapshotStore.SetAsync(
            snapshotAggregate,
            SnapshotJsonContext.Default.CounterAggregate,
            _documentWithSnapshot,
            SnapshotAtVersion).Wait();

        // Setup document WITHOUT snapshot
        _documentWithoutSnapshot = _documentFactory.GetOrCreateAsync("benchmark", $"without-snapshot").Result;
        _dataStore.AppendAsync(_documentWithoutSnapshot, default, _allEvents).Wait();
    }

    [Benchmark(Baseline = true, Description = "Without Snapshot (replay all)")]
    public CounterAggregate LoadWithoutSnapshot()
    {
        var aggregate = new CounterAggregate();
        var events = _dataStore.ReadAsync(_documentWithoutSnapshot).Result;
        foreach (var @event in events!)
        {
            aggregate.Fold(@event);
        }
        return aggregate;
    }

    [Benchmark(Description = "With Snapshot (load + replay remaining)")]
    public CounterAggregate LoadWithSnapshot()
    {
        // Load snapshot
        var aggregate = _snapshotStore.GetAsync(
            SnapshotJsonContext.Default.CounterAggregate,
            _documentWithSnapshot,
            SnapshotAtVersion).Result;

        if (aggregate == null)
        {
            aggregate = new CounterAggregate();
        }

        // Replay only events after snapshot
        var events = _dataStore.ReadAsync(_documentWithSnapshot, startVersion: SnapshotAtVersion).Result;
        foreach (var @event in events!)
        {
            aggregate.Fold(@event);
        }
        return aggregate;
    }

    private static IEvent[] CreateEvents(int count)
    {
        var events = new IEvent[count];
        for (int i = 0; i < count; i++)
        {
            var data = new CounterIncrementedEvent { Amount = 1, Timestamp = DateTime.UtcNow };
            events[i] = new JsonEvent
            {
                EventType = "CounterIncremented",
                EventVersion = i,
                SchemaVersion = 1,
                Payload = JsonSerializer.Serialize(data, SnapshotJsonContext.Default.CounterIncrementedEvent)
            };
        }
        return events;
    }
}

/// <summary>
/// Benchmarks for snapshot creation and retrieval operations.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Snapshots", "Operations")]
public class SnapshotOperationBenchmarks
{
    private InMemorySnapShotStore _snapshotStore = null!;
    private IObjectDocument _document = null!;
    private CounterAggregate _smallAggregate = null!;
    private CounterAggregate _mediumAggregate = null!;
    private CounterAggregate _largeAggregate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _snapshotStore = new InMemorySnapShotStore();
        var documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());
        _document = documentFactory.GetOrCreateAsync("benchmark", "snapshot-ops").Result;

        // Create aggregates of different sizes
        _smallAggregate = new CounterAggregate { Counter = 100 };
        _mediumAggregate = new CounterAggregate { Counter = 10_000, Items = CreateItems(100) };
        _largeAggregate = new CounterAggregate { Counter = 100_000, Items = CreateItems(1000) };

        // Pre-store snapshots for read benchmarks
        _snapshotStore.SetAsync(_smallAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 100).Wait();
        _snapshotStore.SetAsync(_mediumAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 1000).Wait();
        _snapshotStore.SetAsync(_largeAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 10000).Wait();
    }

    private static List<string> CreateItems(int count)
    {
        return Enumerable.Range(0, count).Select(i => $"Item-{i:D5}").ToList();
    }

    // === WRITE BENCHMARKS ===

    [Benchmark(Baseline = true, Description = "Save Small Snapshot")]
    [BenchmarkCategory("Write")]
    public async Task SaveSmallSnapshot()
    {
        await _snapshotStore.SetAsync(_smallAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 101);
    }

    [Benchmark(Description = "Save Medium Snapshot")]
    [BenchmarkCategory("Write")]
    public async Task SaveMediumSnapshot()
    {
        await _snapshotStore.SetAsync(_mediumAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 1001);
    }

    [Benchmark(Description = "Save Large Snapshot")]
    [BenchmarkCategory("Write")]
    public async Task SaveLargeSnapshot()
    {
        await _snapshotStore.SetAsync(_largeAggregate, SnapshotJsonContext.Default.CounterAggregate, _document, 10001);
    }

    // === READ BENCHMARKS ===

    [Benchmark(Description = "Load Small Snapshot")]
    [BenchmarkCategory("Read")]
    public async Task<CounterAggregate?> LoadSmallSnapshot()
    {
        return await _snapshotStore.GetAsync(SnapshotJsonContext.Default.CounterAggregate, _document, 100);
    }

    [Benchmark(Description = "Load Medium Snapshot")]
    [BenchmarkCategory("Read")]
    public async Task<CounterAggregate?> LoadMediumSnapshot()
    {
        return await _snapshotStore.GetAsync(SnapshotJsonContext.Default.CounterAggregate, _document, 1000);
    }

    [Benchmark(Description = "Load Large Snapshot")]
    [BenchmarkCategory("Read")]
    public async Task<CounterAggregate?> LoadLargeSnapshot()
    {
        return await _snapshotStore.GetAsync(SnapshotJsonContext.Default.CounterAggregate, _document, 10000);
    }

    // === LIST/DELETE BENCHMARKS ===

    [Benchmark(Description = "List Snapshots")]
    [BenchmarkCategory("Metadata")]
    public async Task<int> ListSnapshots()
    {
        var snapshots = await _snapshotStore.ListSnapshotsAsync(_document);
        return snapshots.Count;
    }
}

/// <summary>
/// Benchmarks showing the break-even point for snapshotting.
/// At what event count does snapshotting become beneficial?
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Snapshots", "BreakEven")]
public class SnapshotBreakEvenBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemorySnapShotStore _snapshotStore = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;

    // Different event counts to find break-even point
    private IObjectDocument _doc10 = null!;
    private IObjectDocument _doc50 = null!;
    private IObjectDocument _doc100 = null!;
    private IObjectDocument _doc500 = null!;
    private IObjectDocument _doc1000 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dataStore = new InMemoryDataStore();
        _snapshotStore = new InMemorySnapShotStore();
        _documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());

        // Setup documents with different event counts
        _doc10 = SetupDocument("doc10", 10, 5);
        _doc50 = SetupDocument("doc50", 50, 25);
        _doc100 = SetupDocument("doc100", 100, 50);
        _doc500 = SetupDocument("doc500", 500, 250);
        _doc1000 = SetupDocument("doc1000", 1000, 500);
    }

    private IObjectDocument SetupDocument(string id, int eventCount, int snapshotAt)
    {
        var document = _documentFactory.GetOrCreateAsync("benchmark", id).Result;
        var events = CreateEvents(eventCount);
        _dataStore.AppendAsync(document, default, events).Wait();

        // Create snapshot at midpoint
        var aggregate = new CounterAggregate();
        aggregate.ApplyEventsUpTo(events.Take(snapshotAt).ToArray());
        _snapshotStore.SetAsync(aggregate, SnapshotJsonContext.Default.CounterAggregate, document, snapshotAt).Wait();

        return document;
    }

    private static IEvent[] CreateEvents(int count)
    {
        var events = new IEvent[count];
        for (int i = 0; i < count; i++)
        {
            var data = new CounterIncrementedEvent { Amount = 1, Timestamp = DateTime.UtcNow };
            events[i] = new JsonEvent
            {
                EventType = "CounterIncremented",
                EventVersion = i,
                SchemaVersion = 1,
                Payload = JsonSerializer.Serialize(data, SnapshotJsonContext.Default.CounterIncrementedEvent)
            };
        }
        return events;
    }

    private CounterAggregate LoadWithoutSnapshot(IObjectDocument doc)
    {
        var aggregate = new CounterAggregate();
        var events = _dataStore.ReadAsync(doc).Result;
        foreach (var @event in events!)
        {
            aggregate.Fold(@event);
        }
        return aggregate;
    }

    private CounterAggregate LoadWithSnapshot(IObjectDocument doc, int snapshotVersion)
    {
        var aggregate = _snapshotStore.GetAsync(
            SnapshotJsonContext.Default.CounterAggregate, doc, snapshotVersion).Result;

        if (aggregate == null) aggregate = new CounterAggregate();

        var events = _dataStore.ReadAsync(doc, startVersion: snapshotVersion).Result;
        foreach (var @event in events!)
        {
            aggregate.Fold(@event);
        }
        return aggregate;
    }

    // 10 events - likely NO benefit from snapshot
    [Benchmark(Description = "10 events - No Snapshot")]
    [BenchmarkCategory("10events")]
    public CounterAggregate Events10_NoSnapshot() => LoadWithoutSnapshot(_doc10);

    [Benchmark(Description = "10 events - With Snapshot")]
    [BenchmarkCategory("10events")]
    public CounterAggregate Events10_WithSnapshot() => LoadWithSnapshot(_doc10, 5);

    // 50 events - marginal benefit
    [Benchmark(Description = "50 events - No Snapshot")]
    [BenchmarkCategory("50events")]
    public CounterAggregate Events50_NoSnapshot() => LoadWithoutSnapshot(_doc50);

    [Benchmark(Description = "50 events - With Snapshot")]
    [BenchmarkCategory("50events")]
    public CounterAggregate Events50_WithSnapshot() => LoadWithSnapshot(_doc50, 25);

    // 100 events - should start to see benefit
    [Benchmark(Description = "100 events - No Snapshot")]
    [BenchmarkCategory("100events")]
    public CounterAggregate Events100_NoSnapshot() => LoadWithoutSnapshot(_doc100);

    [Benchmark(Description = "100 events - With Snapshot")]
    [BenchmarkCategory("100events")]
    public CounterAggregate Events100_WithSnapshot() => LoadWithSnapshot(_doc100, 50);

    // 500 events - clear benefit
    [Benchmark(Description = "500 events - No Snapshot")]
    [BenchmarkCategory("500events")]
    public CounterAggregate Events500_NoSnapshot() => LoadWithoutSnapshot(_doc500);

    [Benchmark(Description = "500 events - With Snapshot")]
    [BenchmarkCategory("500events")]
    public CounterAggregate Events500_WithSnapshot() => LoadWithSnapshot(_doc500, 250);

    // 1000 events - significant benefit
    [Benchmark(Baseline = true, Description = "1000 events - No Snapshot")]
    [BenchmarkCategory("1000events")]
    public CounterAggregate Events1000_NoSnapshot() => LoadWithoutSnapshot(_doc1000);

    [Benchmark(Description = "1000 events - With Snapshot")]
    [BenchmarkCategory("1000events")]
    public CounterAggregate Events1000_WithSnapshot() => LoadWithSnapshot(_doc1000, 500);
}

// === SUPPORTING TYPES ===

/// <summary>
/// Simple aggregate for snapshot benchmarking.
/// </summary>
public class CounterAggregate : IBase
{
    public int Counter { get; set; }
    public List<string> Items { get; set; } = [];

    public Task Fold()
    {
        // No-op for benchmarks - we fold events explicitly
        return Task.CompletedTask;
    }

    public void Fold(IEvent @event)
    {
        if (@event.EventType == "CounterIncremented")
        {
            var data = JsonEvent.To((JsonEvent)@event, SnapshotJsonContext.Default.CounterIncrementedEvent);
            Counter += data.Amount;
        }
    }

    public void ProcessSnapshot(object snapshot)
    {
        if (snapshot is CounterAggregate source)
        {
            Counter = source.Counter;
            Items = new List<string>(source.Items);
        }
    }

    public void ApplyEventsUpTo(IEvent[] events)
    {
        foreach (var @event in events)
        {
            Fold(@event);
        }
    }
}

public class CounterIncrementedEvent
{
    public int Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

[JsonSerializable(typeof(CounterAggregate))]
[JsonSerializable(typeof(CounterIncrementedEvent))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class SnapshotJsonContext : JsonSerializerContext
{
}
