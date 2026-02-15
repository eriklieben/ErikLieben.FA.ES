using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Testing.InMemory;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Benchmarks.Parsing;

/// <summary>
/// Benchmarks for VersionToken parsing and creation operations.
/// These operations occur frequently during event processing and projection updates.
/// </summary>
[MemoryDiagnoser]

[BenchmarkCategory("Parsing", "VersionToken")]
public class VersionTokenBenchmarks
{
    private string _shortTokenString = null!;
    private string _longTokenString = null!;
    private IEvent _testEvent = null!;
    private IObjectDocument _testDocument = null!;
    private ObjectIdentifier _objectIdentifier = null!;
    private VersionIdentifier _versionIdentifier = null!;
    private VersionToken _existingToken = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Short token (typical case)
        _shortTokenString = "Order__12345__stream-abc__00000000000000000042";

        // Long token (edge case with long identifiers)
        _longTokenString = "VeryLongAggregateNameForTesting__very-long-object-id-12345-abcdef-ghijkl__stream-identifier-that-is-also-quite-long__00000000000000099999";

        _testEvent = new JsonEvent
        {
            EventType = "TestEvent",
            EventVersion = 42,
            SchemaVersion = 1,
            Payload = "{}"
        };

        var documentFactory = new InMemoryObjectDocumentFactory(new InMemoryDocumentTagStore());
        _testDocument = documentFactory.GetOrCreateAsync("Order", "12345").Result;

        _objectIdentifier = new ObjectIdentifier("Order", "12345");
        _versionIdentifier = new VersionIdentifier("stream-abc", 42);

        _existingToken = new VersionToken("Order", "12345", "stream-abc", 42);
    }

    [Benchmark(Baseline = true)]
    public VersionToken ParseShortToken()
    {
        return new VersionToken(_shortTokenString);
    }

    [Benchmark]
    public VersionToken ParseLongToken()
    {
        return new VersionToken(_longTokenString);
    }

    [Benchmark]
    public VersionToken CreateFromEventAndDocument()
    {
        return new VersionToken(_testEvent, _testDocument);
    }

    [Benchmark]
    public VersionToken CreateFromExplicitParts()
    {
        return new VersionToken("Order", "12345", "stream-abc", 42);
    }

    [Benchmark]
    public VersionToken CreateFromIdentifiers()
    {
        return new VersionToken(_objectIdentifier, _versionIdentifier);
    }

    [Benchmark]
    public string StaticFrom()
    {
        return VersionToken.From(_testEvent, _testDocument);
    }

    [Benchmark]
    public string ToVersionTokenString()
    {
        return VersionToken.ToVersionTokenString(99999);
    }

    [Benchmark]
    public VersionToken ToLatestVersion()
    {
        return _existingToken.ToLatestVersion();
    }

    [Benchmark]
    public ObjectIdentifier GetObjectIdentifier()
    {
        return _existingToken.ObjectIdentifier;
    }

    [Benchmark]
    public VersionIdentifier GetVersionIdentifier()
    {
        return _existingToken.VersionIdentifier;
    }

    [Benchmark]
    public int ParseManyTokens()
    {
        int count = 0;
        for (int i = 0; i < 100; i++)
        {
            var token = new VersionToken($"Order__{i}__stream-abc__00000000000000000{i:D3}");
            count += token.Version;
        }
        return count;
    }
}
