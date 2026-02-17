using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;

namespace ErikLieben.FA.ES.Benchmarks.Core;

/// <summary>
/// Benchmarks comparing event stream operations with and without chunking enabled.
/// Measures the overhead of chunk management and the impact on read/append performance.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Core", "Chunking")]
public class ChunkingBenchmarks
{
    private InMemoryDataStore _dataStore = null!;
    private InMemoryObjectDocumentFactory _documentFactory = null!;
    private IObjectDocument _nonChunkedDoc = null!;
    private IObjectDocument _chunkedDoc = null!;
    private JsonEvent[] _appendBatch = null!;

    [Params(100, 500, 1000, 5000)]
    public int PrePopulatedEventCount { get; set; }

    [Params(100, 500)]
    public int ChunkSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _dataStore = new InMemoryDataStore();
        _documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());

        // Setup non-chunked document
        _nonChunkedDoc = await _documentFactory.GetOrCreateAsync("benchmark", "no-chunk");
        var events = CreateEvents(PrePopulatedEventCount);
        await _dataStore.AppendAsync(_nonChunkedDoc, default, events);

        // Setup chunked document
        _chunkedDoc = await _documentFactory.GetOrCreateAsync("benchmark", "chunked");
        _chunkedDoc.Active.ChunkSettings = new StreamChunkSettings
        {
            EnableChunks = true,
            ChunkSize = ChunkSize
        };
        _chunkedDoc.Active.StreamChunks = [new StreamChunk(0, 0, -1)];
        await _dataStore.AppendAsync(_chunkedDoc, default, events);

        // Events to append in benchmarks
        _appendBatch = CreateEvents(10, startVersion: PrePopulatedEventCount);
    }

    [Benchmark(Baseline = true, Description = "Read all events (no chunking)")]
    public async Task<int> ReadWithoutChunking()
    {
        var result = await _dataStore.ReadAsync(_nonChunkedDoc);
        return result?.Count() ?? 0;
    }

    [Benchmark(Description = "Read all events (chunked)")]
    public async Task<int> ReadWithChunking()
    {
        var result = await _dataStore.ReadAsync(_chunkedDoc);
        return result?.Count() ?? 0;
    }

    [Benchmark(Description = "Stream read (no chunking)")]
    public async Task<int> StreamReadWithoutChunking()
    {
        var count = 0;
        await foreach (var _ in _dataStore.ReadAsStreamAsync(_nonChunkedDoc))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Stream read (chunked)")]
    public async Task<int> StreamReadWithChunking()
    {
        var count = 0;
        await foreach (var _ in _dataStore.ReadAsStreamAsync(_chunkedDoc))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Append batch (no chunking)")]
    public async Task AppendWithoutChunking()
    {
        await _dataStore.AppendAsync(_nonChunkedDoc, default, _appendBatch);
    }

    [Benchmark(Description = "Append batch (chunked)")]
    public async Task AppendWithChunking()
    {
        await _dataStore.AppendAsync(_chunkedDoc, default, _appendBatch);
    }

    [Benchmark(Description = "Read from midpoint (no chunking)")]
    public async Task<int> ReadFromVersionWithoutChunking()
    {
        var result = await _dataStore.ReadAsync(_nonChunkedDoc, startVersion: PrePopulatedEventCount / 2);
        return result?.Count() ?? 0;
    }

    [Benchmark(Description = "Read from midpoint (chunked)")]
    public async Task<int> ReadFromVersionWithChunking()
    {
        var result = await _dataStore.ReadAsync(_chunkedDoc, startVersion: PrePopulatedEventCount / 2);
        return result?.Count() ?? 0;
    }

    private static JsonEvent[] CreateEvents(int count, int startVersion = 0)
    {
        var events = new JsonEvent[count];
        for (int i = 0; i < count; i++)
        {
            events[i] = new JsonEvent
            {
                EventType = "BenchmarkEvent",
                EventVersion = startVersion + i,
                Payload = $"{{\"id\":{startVersion + i},\"data\":\"benchmark-data\"}}"
            };
        }
        return events;
    }
}
