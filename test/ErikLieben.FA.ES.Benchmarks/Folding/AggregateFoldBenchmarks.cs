using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using NSubstitute;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.Benchmarks.Folding;

/// <summary>
/// Benchmarks for Aggregate folding operations.
/// Folding is the hot path when loading aggregates - it replays events to rebuild state.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class AggregateFoldBenchmarks
{
    private List<IEvent> _events100 = null!;
    private List<IEvent> _events1000 = null!;
    private List<IEvent> _events10000 = null!;
    private IEvent _singleEvent = null!;
    private BenchmarkAggregate _aggregate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _events100 = CreateEvents(100);
        _events1000 = CreateEvents(1000);
        _events10000 = CreateEvents(10000);
        _singleEvent = CreateEvent(0);

        // Create a mock stream for the aggregate
        var stream = CreateMockStream();
        _aggregate = new BenchmarkAggregate(stream);
    }

    private static IEventStream CreateMockStream()
    {
        var stream = Substitute.For<IEventStream>();
        var settings = new EventStreamSettings { ManualFolding = true };
        stream.Settings.Returns(settings);
        return stream;
    }

    private static List<IEvent> CreateEvents(int count)
    {
        var events = new List<IEvent>(count);
        for (int i = 0; i < count; i++)
        {
            events.Add(CreateEvent(i));
        }
        return events;
    }

    private static JsonEvent CreateEvent(int version)
    {
        var data = new CounterIncremented { Amount = 1, Timestamp = DateTime.UtcNow };
        return new JsonEvent
        {
            EventType = "CounterIncremented",
            EventVersion = version,
            SchemaVersion = 1,
            Payload = JsonSerializer.Serialize(data, FoldingJsonContext.Default.CounterIncremented)
        };
    }

    [Benchmark(Baseline = true)]
    public int FoldSingleEvent()
    {
        _aggregate.Reset();
        _aggregate.Fold(_singleEvent);
        return _aggregate.Counter;
    }

    [Benchmark]
    public int Fold100Events()
    {
        _aggregate.Reset();
        foreach (var @event in _events100)
        {
            _aggregate.Fold(@event);
        }
        return _aggregate.Counter;
    }

    [Benchmark]
    public int Fold1000Events()
    {
        _aggregate.Reset();
        foreach (var @event in _events1000)
        {
            _aggregate.Fold(@event);
        }
        return _aggregate.Counter;
    }

    [Benchmark]
    public int Fold10000Events()
    {
        _aggregate.Reset();
        foreach (var @event in _events10000)
        {
            _aggregate.Fold(@event);
        }
        return _aggregate.Counter;
    }

    [Benchmark]
    public int FoldWithDeserialization100()
    {
        _aggregate.Reset();
        foreach (var @event in _events100)
        {
            _aggregate.FoldWithDeserialization(@event);
        }
        return _aggregate.Counter;
    }

    [Benchmark]
    public int FoldWithDeserialization1000()
    {
        _aggregate.Reset();
        foreach (var @event in _events1000)
        {
            _aggregate.FoldWithDeserialization(@event);
        }
        return _aggregate.Counter;
    }
}

/// <summary>
/// Simple aggregate for benchmarking folding performance.
/// </summary>
public class BenchmarkAggregate : Aggregate
{
    public int Counter { get; private set; }

    public BenchmarkAggregate(IEventStream stream) : base(stream)
    {
    }

    public void Reset()
    {
        Counter = 0;
    }

    public override void Fold(IEvent @event)
    {
        // Simulate minimal work - just increment counter
        // This measures the overhead of the folding infrastructure
        if (@event.EventType == "CounterIncremented")
        {
            Counter++;
        }
    }

    public void FoldWithDeserialization(IEvent @event)
    {
        // Full deserialization path - measures JSON parsing cost
        if (@event.EventType == "CounterIncremented")
        {
            var data = JsonEvent.To(@event, FoldingJsonContext.Default.CounterIncremented);
            Counter += data.Amount;
        }
    }
}

public class CounterIncremented
{
    public int Amount { get; set; }
    public DateTime Timestamp { get; set; }
}

[JsonSerializable(typeof(CounterIncremented))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class FoldingJsonContext : JsonSerializerContext
{
}
