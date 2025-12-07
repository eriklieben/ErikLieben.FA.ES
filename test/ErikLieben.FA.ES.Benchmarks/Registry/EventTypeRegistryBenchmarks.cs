using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Benchmarks.Serialization;

namespace ErikLieben.FA.ES.Benchmarks.Registry;

/// <summary>
/// Benchmarks for EventTypeRegistry lookup operations.
/// Compares frozen (immutable) vs mutable dictionary performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class EventTypeRegistryBenchmarks
{
    private EventTypeRegistry _frozenRegistry = null!;
    private EventTypeRegistry _mutableRegistry = null!;

    private Type[] _typesToLookup = null!;
    private string[] _namesToLookup = null!;
    private (string Name, int Version)[] _nameAndVersionToLookup = null!;

    [Params(10, 100, 500)]
    public int EventTypeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _frozenRegistry = new EventTypeRegistry();
        _mutableRegistry = new EventTypeRegistry();

        // Register event types
        for (int i = 0; i < EventTypeCount; i++)
        {
            var type = typeof(SmallEventData); // Reuse type for simplicity
            var name = $"Event_{i}";
            var version = (i % 3) + 1; // Versions 1, 2, or 3
            var jsonTypeInfo = BenchmarkJsonContext.Default.SmallEventData;

            _frozenRegistry.Add(type, name, version, jsonTypeInfo);
            _mutableRegistry.Add(type, name, version, jsonTypeInfo);
        }

        // Freeze one registry
        _frozenRegistry.Freeze();

        // Prepare lookup data - pick random items to look up
        var random = new Random(42);
        _typesToLookup = Enumerable.Range(0, 100).Select(_ => typeof(SmallEventData)).ToArray();
        _namesToLookup = Enumerable.Range(0, 100).Select(_ => $"Event_{random.Next(EventTypeCount)}").ToArray();
        _nameAndVersionToLookup = Enumerable.Range(0, 100)
            .Select(_ => ($"Event_{random.Next(EventTypeCount)}", random.Next(1, 4)))
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int FrozenRegistry_TryGetByName()
    {
        int found = 0;
        foreach (var name in _namesToLookup)
        {
            if (_frozenRegistry.TryGetByName(name, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int MutableRegistry_TryGetByName()
    {
        int found = 0;
        foreach (var name in _namesToLookup)
        {
            if (_mutableRegistry.TryGetByName(name, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int FrozenRegistry_TryGetByType()
    {
        int found = 0;
        foreach (var type in _typesToLookup)
        {
            if (_frozenRegistry.TryGetByType(type, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int MutableRegistry_TryGetByType()
    {
        int found = 0;
        foreach (var type in _typesToLookup)
        {
            if (_mutableRegistry.TryGetByType(type, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int FrozenRegistry_TryGetByNameAndVersion()
    {
        int found = 0;
        foreach (var (name, version) in _nameAndVersionToLookup)
        {
            if (_frozenRegistry.TryGetByNameAndVersion(name, version, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int MutableRegistry_TryGetByNameAndVersion()
    {
        int found = 0;
        foreach (var (name, version) in _nameAndVersionToLookup)
        {
            if (_mutableRegistry.TryGetByNameAndVersion(name, version, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public EventTypeInfo? FrozenRegistry_SingleLookup()
    {
        return _frozenRegistry.GetByName("Event_50");
    }

    [Benchmark]
    public EventTypeInfo? MutableRegistry_SingleLookup()
    {
        return _mutableRegistry.GetByName("Event_50");
    }
}
