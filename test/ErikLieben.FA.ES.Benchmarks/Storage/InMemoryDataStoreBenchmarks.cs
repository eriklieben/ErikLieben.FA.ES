using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using NSubstitute;

namespace ErikLieben.FA.ES.Benchmarks.Storage;

/// <summary>
/// Benchmarks for InMemoryDataStore read and write operations.
/// Useful as a baseline for comparing with cloud storage providers.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class InMemoryDataStoreBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private IObjectDocument _document = null!;
    private IEvent[] _singleEvent = null!;
    private IEvent[] _batchEvents10 = null!;
    private IEvent[] _batchEvents100 = null!;

    [Params(100, 1000, 10000)]
    public int PrePopulatedEventCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dataStore = new InMemoryDataStore();
        _document = CreateMockDocument("TestAggregate", "test-123", "stream-abc");

        // Pre-populate the store with events
        var events = CreateEvents(PrePopulatedEventCount);
        _dataStore.AppendAsync(_document, events).Wait();

        // Create events for append benchmarks
        _singleEvent = CreateEvents(1);
        _batchEvents10 = CreateEvents(10);
        _batchEvents100 = CreateEvents(100);
    }

    [IterationSetup(Target = nameof(AppendSingleEvent))]
    public void SetupAppendSingle()
    {
        // Reset for each iteration
        _dataStore = new InMemoryDataStore();
        _dataStore.AppendAsync(_document, CreateEvents(PrePopulatedEventCount)).Wait();
    }

    [IterationSetup(Targets = new[] { nameof(AppendBatch10Events), nameof(AppendBatch100Events) })]
    public void SetupAppendBatch()
    {
        _dataStore = new InMemoryDataStore();
        _dataStore.AppendAsync(_document, CreateEvents(PrePopulatedEventCount)).Wait();
    }

    private static IObjectDocument CreateMockDocument(string objectName, string objectId, string streamIdentifier)
    {
        var document = Substitute.For<IObjectDocument>();
        document.ObjectName.Returns(objectName);
        document.ObjectId.Returns(objectId);

        var activeStream = Substitute.For<IStreamInformation>();
        activeStream.StreamIdentifier.Returns(streamIdentifier);
        document.Active.Returns(activeStream);

        return document;
    }

    private static IEvent[] CreateEvents(int count)
    {
        var events = new IEvent[count];
        for (int i = 0; i < count; i++)
        {
            events[i] = new JsonEvent
            {
                EventType = "TestEvent",
                EventVersion = i,
                SchemaVersion = 1,
                Payload = $"{{\"id\":\"{i}\",\"data\":\"test-data-{i}\"}}"
            };
        }
        return events;
    }

    [Benchmark(Baseline = true)]
    public async Task<List<IEvent>> ReadAllEvents()
    {
        var result = await _dataStore.ReadAsync(_document);
        return result?.ToList() ?? [];
    }

    [Benchmark]
    public async Task<List<IEvent>> ReadFromVersion()
    {
        var result = await _dataStore.ReadAsync(_document, startVersion: PrePopulatedEventCount / 2);
        return result?.ToList() ?? [];
    }

    [Benchmark]
    public async Task AppendSingleEvent()
    {
        await _dataStore.AppendAsync(_document, _singleEvent);
    }

    [Benchmark]
    public async Task AppendBatch10Events()
    {
        await _dataStore.AppendAsync(_document, _batchEvents10);
    }

    [Benchmark]
    public async Task AppendBatch100Events()
    {
        await _dataStore.AppendAsync(_document, _batchEvents100);
    }

    [Benchmark]
    public string GetStoreKey()
    {
        return InMemoryDataStore.GetStoreKey("TestAggregate", "test-123");
    }

    [Benchmark]
    public Dictionary<int, IEvent> GetDataStoreFor()
    {
        var key = InMemoryDataStore.GetStoreKey("TestAggregate", "test-123");
        return _dataStore.GetDataStoreFor(key);
    }
}
