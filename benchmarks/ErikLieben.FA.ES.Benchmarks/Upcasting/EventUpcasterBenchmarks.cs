using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.EventStream;

namespace ErikLieben.FA.ES.Benchmarks.Upcasting;

/// <summary>
/// Benchmarks for event upcasting operations.
/// Measures the cost of schema version migration chains.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Upcasting", "SchemaEvolution")]
public class EventUpcasterBenchmarks
{
    private EventUpcasterRegistry _registry = null!;
    private EventV1 _v1Event = null!;

    [Params(1, 5)]
    public int UpcastChainLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _registry = new EventUpcasterRegistry();

        // Register upcasters for a chain: v1 -> v2 -> v3 -> v4 -> v5 -> v6
        _registry.Add<EventV1, EventV2>("TestEvent", 1, 2, v1 => new EventV2
        {
            Id = v1.Id,
            Name = v1.Name,
            CreatedAt = DateTime.UtcNow
        });

        _registry.Add<EventV2, EventV3>("TestEvent", 2, 3, v2 => new EventV3
        {
            Id = v2.Id,
            Name = v2.Name,
            CreatedAt = v2.CreatedAt,
            UpdatedAt = v2.CreatedAt
        });

        _registry.Add<EventV3, EventV4>("TestEvent", 3, 4, v3 => new EventV4
        {
            Id = v3.Id,
            Name = v3.Name,
            CreatedAt = v3.CreatedAt,
            UpdatedAt = v3.UpdatedAt,
            Status = "Active"
        });

        _registry.Add<EventV4, EventV5>("TestEvent", 4, 5, v4 => new EventV5
        {
            Id = v4.Id,
            Name = v4.Name,
            CreatedAt = v4.CreatedAt,
            UpdatedAt = v4.UpdatedAt,
            Status = v4.Status,
            Version = 5
        });

        _registry.Add<EventV5, EventV6>("TestEvent", 5, 6, v5 => new EventV6
        {
            Id = v5.Id,
            Name = v5.Name,
            CreatedAt = v5.CreatedAt,
            UpdatedAt = v5.UpdatedAt,
            Status = v5.Status,
            Version = v5.Version,
            Tags = []
        });

        _registry.Freeze();

        _v1Event = new EventV1 { Id = "test-123", Name = "Test Event" };
    }

    [Benchmark(Baseline = true)]
    public (object Data, int SchemaVersion) UpcastChain()
    {
        // Upcast from v1 to target version based on chain length
        int targetVersion = 1 + UpcastChainLength;
        return _registry.UpcastToVersion("TestEvent", 1, targetVersion, _v1Event);
    }

    [Benchmark]
    public bool TryGetUpcaster_Exists()
    {
        return _registry.TryGetUpcaster("TestEvent", 1, out _);
    }

    [Benchmark]
    public bool TryGetUpcaster_NotExists()
    {
        return _registry.TryGetUpcaster("NonExistentEvent", 1, out _);
    }

    [Benchmark]
    public (object Data, int SchemaVersion) SingleUpcast()
    {
        return _registry.UpcastToVersion("TestEvent", 1, 2, _v1Event);
    }
}

public class EventV1
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class EventV2
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EventV3
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EventV4
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class EventV5
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class EventV6
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public List<string> Tags { get; set; } = [];
}
