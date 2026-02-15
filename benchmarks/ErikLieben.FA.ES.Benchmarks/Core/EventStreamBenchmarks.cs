using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Benchmarks.Core;

/// <summary>
/// Benchmarks for event stream read and append operations.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Core", "EventStream")]
public class EventStreamBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;
    private JsonEvent[] _events = null!;

    [Params(1, 10)]
    public int EventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dataStore = new InMemoryDataStore();
        _documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());
        _events = Enumerable.Range(0, EventCount)
            .Select(i => CreateEvent(i))
            .ToArray();
    }

    [Benchmark(Description = "Append events to stream")]
    public async Task AppendEvents()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"append-{Guid.NewGuid()}");
        await _dataStore.AppendAsync(doc, default, _events);
    }

    [Benchmark(Description = "Read all events from stream")]
    public async Task ReadEvents()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"read-{Guid.NewGuid()}");
        await _dataStore.AppendAsync(doc, default, _events);
        var events = await _dataStore.ReadAsync(doc);
        _ = events?.Count();
    }

    [Benchmark(Description = "Stream events async")]
    public async Task StreamEventsAsync()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"stream-{Guid.NewGuid()}");
        await _dataStore.AppendAsync(doc, default, _events);

        var count = 0;
        await foreach (var _ in _dataStore.ReadAsStreamAsync(doc))
        {
            count++;
        }
    }

    private static JsonEvent CreateEvent(int index) => new()
    {
        EventType = "SampleEvent",
        EventVersion = index,
        Payload = $"{{\"id\":{index},\"name\":\"Event {index}\"}}"
    };
}
