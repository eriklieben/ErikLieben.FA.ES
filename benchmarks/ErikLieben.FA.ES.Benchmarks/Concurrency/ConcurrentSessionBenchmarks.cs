using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Testing.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Benchmarks.Concurrency;

/// <summary>
/// Benchmarks for concurrent session operations measuring parallel writer throughput.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Concurrency")]
public class ConcurrentSessionBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemoryEventStreamFactory _factory = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;

    [Params(1, 5, 10)]
    public int ConcurrentWriters { get; set; }

    [Params(10)]
    public int EventsPerWriter { get; set; }

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

    [Benchmark(Baseline = true, Description = "Sequential writers to separate streams")]
    public async Task SequentialWritersSeparateStreams()
    {
        for (int w = 0; w < ConcurrentWriters; w++)
        {
            var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"seq-separate-{w}-{Guid.NewGuid()}");
            var stream = _factory.Create(doc);
            await stream.Session(ctx =>
            {
                for (int i = 0; i < EventsPerWriter; i++)
                {
                    ctx.Append(new JsonEvent
                    {
                        EventType = "TestEvent",
                        EventVersion = i,
                        Payload = $"{{\"writer\":{w},\"index\":{i}}}"
                    });
                }
            });
        }
    }

    [Benchmark(Description = "Parallel writers to separate streams")]
    public async Task ParallelWritersSeparateStreams()
    {
        var tasks = Enumerable.Range(0, ConcurrentWriters).Select(async w =>
        {
            var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"par-separate-{w}-{Guid.NewGuid()}");
            var stream = _factory.Create(doc);
            await stream.Session(ctx =>
            {
                for (int i = 0; i < EventsPerWriter; i++)
                {
                    ctx.Append(new JsonEvent
                    {
                        EventType = "TestEvent",
                        EventVersion = i,
                        Payload = $"{{\"writer\":{w},\"index\":{i}}}"
                    });
                }
            });
        });

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Sequential writers to same stream")]
    public async Task SequentialWritersSameStream()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"seq-same-{Guid.NewGuid()}");
        var stream = _factory.Create(doc);

        for (int w = 0; w < ConcurrentWriters; w++)
        {
            await stream.Session(ctx =>
            {
                for (int i = 0; i < EventsPerWriter; i++)
                {
                    ctx.Append(new JsonEvent
                    {
                        EventType = "TestEvent",
                        EventVersion = w * EventsPerWriter + i,
                        Payload = $"{{\"writer\":{w},\"index\":{i}}}"
                    });
                }
            });
        }
    }

    [Benchmark(Description = "Parallel writers to same stream")]
    public async Task ParallelWritersSameStream()
    {
        var doc = await _documentFactory.GetOrCreateAsync("benchmark", $"par-same-{Guid.NewGuid()}");
        var stream = _factory.Create(doc);

        var tasks = Enumerable.Range(0, ConcurrentWriters).Select(async w =>
        {
            await stream.Session(ctx =>
            {
                for (int i = 0; i < EventsPerWriter; i++)
                {
                    ctx.Append(new JsonEvent
                    {
                        EventType = "TestEvent",
                        EventVersion = w * EventsPerWriter + i,
                        Payload = $"{{\"writer\":{w},\"index\":{i}}}"
                    });
                }
            });
        });

        await Task.WhenAll(tasks);
    }
}
