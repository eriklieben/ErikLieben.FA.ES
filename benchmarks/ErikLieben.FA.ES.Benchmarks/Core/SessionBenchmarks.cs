using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Testing.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Benchmarks.Core;

/// <summary>
/// Benchmarks for session commit operations including event appending and folding.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Core", "Session")]
public class SessionBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemoryEventStreamFactory _factory = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;

    [Params(1, 10, 50)]
    public int EventsPerSession { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        _dataStore = new InMemoryDataStore();
        var documentTagStore = new InMemoryDocumentTagStore();
        _documentFactory = new InMemoryObjectDocumentFactory(documentTagStore);
        _factory = new InMemoryEventStreamFactory(
            new InMemoryDocumentTagDocumentFactory(),
            _documentFactory,
            _dataStore,
            new InMemoryAggregateFactory(serviceProvider, []));
    }

    [Benchmark(Description = "Session with single event append")]
    public async Task SessionSingleAppend()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"session-single-{Guid.NewGuid()}");
        var stream = _factory.Create(doc);
        await stream.Session(ctx =>
        {
            ctx.Append(new JsonEvent
            {
                EventType = "TestEvent",
                EventVersion = 0,
                Payload = "{\"data\":\"test\"}"
            });
        });
    }

    [Benchmark(Description = "Session with multiple event appends")]
    public async Task SessionMultipleAppends()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"session-multi-{Guid.NewGuid()}");
        var stream = _factory.Create(doc);
        await stream.Session(ctx =>
        {
            for (var i = 0; i < EventsPerSession; i++)
            {
                ctx.Append(new JsonEvent
                {
                    EventType = "TestEvent",
                    EventVersion = i,
                    Payload = $"{{\"index\":{i}}}"
                });
            }
        });
    }

    [Benchmark(Description = "Sequential sessions on same stream")]
    public async Task SequentialSessions()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"session-seq-{Guid.NewGuid()}");
        var stream = _factory.Create(doc);

        for (var i = 0; i < 5; i++)
        {
            await stream.Session(ctx =>
            {
                ctx.Append(new JsonEvent
                {
                    EventType = "TestEvent",
                    EventVersion = i,
                    Payload = $"{{\"iteration\":{i}}}"
                });
            });
        }
    }
}
