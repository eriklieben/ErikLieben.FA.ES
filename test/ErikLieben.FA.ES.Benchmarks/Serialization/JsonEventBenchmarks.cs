using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES;

namespace ErikLieben.FA.ES.Benchmarks.Serialization;

/// <summary>
/// Benchmarks for JSON event serialization and deserialization operations.
/// These are hot-path operations that occur on every event during folding.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class JsonEventBenchmarks
{
    private JsonEvent _smallPayloadEvent = null!;
    private JsonEvent _mediumPayloadEvent = null!;
    private JsonEvent _largePayloadEvent = null!;

    private string _smallJson = null!;
    private string _mediumJson = null!;
    private string _largeJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small payload (~100 bytes)
        var smallData = new SmallEventData { Id = "test-123", Name = "Test Event" };
        _smallJson = JsonSerializer.Serialize(smallData, BenchmarkJsonContext.Default.SmallEventData);
        _smallPayloadEvent = CreateEvent(_smallJson, "SmallEvent");

        // Medium payload (~1KB)
        var mediumData = new MediumEventData
        {
            Id = "test-456",
            Name = "Medium Test Event",
            Description = new string('A', 500),
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 10).ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };
        _mediumJson = JsonSerializer.Serialize(mediumData, BenchmarkJsonContext.Default.MediumEventData);
        _mediumPayloadEvent = CreateEvent(_mediumJson, "MediumEvent");

        // Large payload (~10KB)
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
        _largePayloadEvent = CreateEvent(_largeJson, "LargeEvent");
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
    public SmallEventData DeserializeSmallPayload()
    {
        return JsonEvent.To(_smallPayloadEvent, BenchmarkJsonContext.Default.SmallEventData);
    }

    [Benchmark]
    public MediumEventData DeserializeMediumPayload()
    {
        return JsonEvent.To(_mediumPayloadEvent, BenchmarkJsonContext.Default.MediumEventData);
    }

    [Benchmark]
    public LargeEventData DeserializeLargePayload()
    {
        return JsonEvent.To(_largePayloadEvent, BenchmarkJsonContext.Default.LargeEventData);
    }

    [Benchmark]
    public IEvent<SmallEventData> ToEventSmallPayload()
    {
        return JsonEvent.ToEvent(_smallPayloadEvent, BenchmarkJsonContext.Default.SmallEventData);
    }

    [Benchmark]
    public IEvent<MediumEventData> ToEventMediumPayload()
    {
        return JsonEvent.ToEvent(_mediumPayloadEvent, BenchmarkJsonContext.Default.MediumEventData);
    }

    [Benchmark]
    public IEvent<LargeEventData> ToEventLargePayload()
    {
        return JsonEvent.ToEvent(_largePayloadEvent, BenchmarkJsonContext.Default.LargeEventData);
    }

    [Benchmark]
    public string SerializeSmallPayload()
    {
        var data = new SmallEventData { Id = "test-123", Name = "Test Event" };
        return JsonSerializer.Serialize(data, BenchmarkJsonContext.Default.SmallEventData);
    }

    [Benchmark]
    public string SerializeMediumPayload()
    {
        var data = new MediumEventData
        {
            Id = "test-456",
            Name = "Medium Test Event",
            Description = new string('A', 500),
            Tags = Enumerable.Range(1, 20).Select(i => $"tag-{i}").ToList(),
            Properties = Enumerable.Range(1, 10).ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };
        return JsonSerializer.Serialize(data, BenchmarkJsonContext.Default.MediumEventData);
    }
}

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
