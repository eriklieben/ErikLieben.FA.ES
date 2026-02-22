using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Benchmarks.Serialization;

/// <summary>
/// Compares Source-Generated vs Reflection-Based JSON serialization.
/// Grouped by PayloadSize so each size has its own baseline.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[BenchmarkCategory("Serialization")]
public class JsonSerializeBenchmarks
{
    private SmallEventData _smallData = null!;
    private MediumEventData _mediumData = null!;
    private LargeEventData _largeData = null!;

    private static readonly JsonSerializerOptions ReflectionOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Params("Small", "Medium", "Large")]
    public string PayloadSize { get; set; } = "Small";

    [GlobalSetup]
    public void Setup()
    {
        _smallData = new SmallEventData { Id = "test-123", Name = "Test Event" };

        _mediumData = new MediumEventData
        {
            Id = "test-456",
            Name = "Medium Test Event",
            Description = new string('A', 500),
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 10).ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };

        _largeData = new LargeEventData
        {
            Id = "test-789",
            Name = "Large Test Event",
            Description = new string('B', 5000),
            Tags = Enumerable.Range(1, 100).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 50).ToDictionary(i => $"key-{i}", i => $"value-{i}"),
            Items = Enumerable.Range(1, 50).Select(i => new ItemData { ItemId = i, ItemName = $"Item {i}", ItemValue = i * 10.5m }).ToList()
        };
    }

    [Benchmark(Baseline = true)]
    public string SourceGen()
    {
        return PayloadSize switch
        {
            "Small" => JsonSerializer.Serialize(_smallData, BenchmarkJsonContext.Default.SmallEventData),
            "Medium" => JsonSerializer.Serialize(_mediumData, BenchmarkJsonContext.Default.MediumEventData),
            "Large" => JsonSerializer.Serialize(_largeData, BenchmarkJsonContext.Default.LargeEventData),
            _ => throw new InvalidOperationException()
        };
    }

    [Benchmark]
    public string Reflection()
    {
        return PayloadSize switch
        {
            "Small" => JsonSerializer.Serialize(_smallData, ReflectionOptions),
            "Medium" => JsonSerializer.Serialize(_mediumData, ReflectionOptions),
            "Large" => JsonSerializer.Serialize(_largeData, ReflectionOptions),
            _ => throw new InvalidOperationException()
        };
    }
}

/// <summary>
/// Compares Source-Generated vs Reflection-Based JSON deserialization.
/// Grouped by PayloadSize so each size has its own baseline.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[BenchmarkCategory("Deserialization")]
public class JsonDeserializeBenchmarks
{
    private string _smallJson = null!;
    private string _mediumJson = null!;
    private string _largeJson = null!;

    private static readonly JsonSerializerOptions ReflectionOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Params("Small", "Medium", "Large")]
    public string PayloadSize { get; set; } = "Small";

    [GlobalSetup]
    public void Setup()
    {
        var smallData = new SmallEventData { Id = "test-123", Name = "Test Event" };
        _smallJson = JsonSerializer.Serialize(smallData, BenchmarkJsonContext.Default.SmallEventData);

        var mediumData = new MediumEventData
        {
            Id = "test-456",
            Name = "Medium Test Event",
            Description = new string('A', 500),
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 10).ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };
        _mediumJson = JsonSerializer.Serialize(mediumData, BenchmarkJsonContext.Default.MediumEventData);

        var largeData = new LargeEventData
        {
            Id = "test-789",
            Name = "Large Test Event",
            Description = new string('B', 5000),
            Tags = Enumerable.Range(1, 100).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 50).ToDictionary(i => $"key-{i}", i => $"value-{i}"),
            Items = Enumerable.Range(1, 50).Select(i => new ItemData { ItemId = i, ItemName = $"Item {i}", ItemValue = i * 10.5m }).ToList()
        };
        _largeJson = JsonSerializer.Serialize(largeData, BenchmarkJsonContext.Default.LargeEventData);
    }

    [Benchmark(Baseline = true)]
    public object SourceGen()
    {
        return PayloadSize switch
        {
            "Small" => JsonSerializer.Deserialize(_smallJson, BenchmarkJsonContext.Default.SmallEventData)!,
            "Medium" => JsonSerializer.Deserialize(_mediumJson, BenchmarkJsonContext.Default.MediumEventData)!,
            "Large" => JsonSerializer.Deserialize(_largeJson, BenchmarkJsonContext.Default.LargeEventData)!,
            _ => throw new InvalidOperationException()
        };
    }

    [Benchmark]
    public object? Reflection()
    {
        return PayloadSize switch
        {
            "Small" => JsonSerializer.Deserialize<SmallEventData>(_smallJson, ReflectionOptions),
            "Medium" => JsonSerializer.Deserialize<MediumEventData>(_mediumJson, ReflectionOptions),
            "Large" => JsonSerializer.Deserialize<LargeEventData>(_largeJson, ReflectionOptions),
            _ => throw new InvalidOperationException()
        };
    }
}

/// <summary>
/// Benchmarks the full event processing pipeline used in this library.
/// Compares raw deserialization vs the IEvent wrapper creation.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[BenchmarkCategory("EventProcessing")]
public class EventProcessingBenchmarks
{
    private JsonEvent _smallPayloadEvent = null!;
    private JsonEvent _mediumPayloadEvent = null!;
    private JsonEvent _largePayloadEvent = null!;

    [Params("Small", "Medium", "Large")]
    public string PayloadSize { get; set; } = "Small";

    [GlobalSetup]
    public void Setup()
    {
        var smallData = new SmallEventData { Id = "test-123", Name = "Test Event" };
        var smallJson = JsonSerializer.Serialize(smallData, BenchmarkJsonContext.Default.SmallEventData);
        _smallPayloadEvent = CreateEvent(smallJson, "SmallEvent");

        var mediumData = new MediumEventData
        {
            Id = "test-456",
            Name = "Medium Test Event",
            Description = new string('A', 500),
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 10).ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };
        var mediumJson = JsonSerializer.Serialize(mediumData, BenchmarkJsonContext.Default.MediumEventData);
        _mediumPayloadEvent = CreateEvent(mediumJson, "MediumEvent");

        var largeData = new LargeEventData
        {
            Id = "test-789",
            Name = "Large Test Event",
            Description = new string('B', 5000),
            Tags = Enumerable.Range(1, 100).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 50).ToDictionary(i => $"key-{i}", i => $"value-{i}"),
            Items = Enumerable.Range(1, 50).Select(i => new ItemData { ItemId = i, ItemName = $"Item {i}", ItemValue = i * 10.5m }).ToList()
        };
        var largeJson = JsonSerializer.Serialize(largeData, BenchmarkJsonContext.Default.LargeEventData);
        _largePayloadEvent = CreateEvent(largeJson, "LargeEvent");
    }

    private static JsonEvent CreateEvent(string payload, string eventType)
    {
        return new JsonEvent
        {
            EventType = eventType,
            EventVersion = 1,
            SchemaVersion = 1,
            Payload = payload
        };
    }

    [Benchmark(Baseline = true)]
    public object RawDeserialize()
    {
        return PayloadSize switch
        {
            "Small" => JsonEvent.To(_smallPayloadEvent, BenchmarkJsonContext.Default.SmallEventData),
            "Medium" => JsonEvent.To(_mediumPayloadEvent, BenchmarkJsonContext.Default.MediumEventData),
            "Large" => JsonEvent.To(_largePayloadEvent, BenchmarkJsonContext.Default.LargeEventData),
            _ => throw new InvalidOperationException()
        };
    }

    [Benchmark]
    public object ToEventWithMetadata()
    {
        return PayloadSize switch
        {
            "Small" => JsonEvent.ToEvent(_smallPayloadEvent, BenchmarkJsonContext.Default.SmallEventData),
            "Medium" => JsonEvent.ToEvent(_mediumPayloadEvent, BenchmarkJsonContext.Default.MediumEventData),
            "Large" => JsonEvent.ToEvent(_largePayloadEvent, BenchmarkJsonContext.Default.LargeEventData),
            _ => throw new InvalidOperationException()
        };
    }
}

// === DATA CLASSES ===

public class SmallEventData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MediumEventData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
}

public class LargeEventData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Properties { get; set; } = [];
    public List<ItemData> Items { get; set; } = [];
}

public class ItemData
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal ItemValue { get; set; }
}

[JsonSerializable(typeof(SmallEventData))]
[JsonSerializable(typeof(MediumEventData))]
[JsonSerializable(typeof(LargeEventData))]
[JsonSerializable(typeof(ItemData))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class BenchmarkJsonContext : JsonSerializerContext
{
}
