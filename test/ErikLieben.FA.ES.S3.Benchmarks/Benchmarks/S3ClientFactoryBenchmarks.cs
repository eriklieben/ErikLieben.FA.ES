using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.S3;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class S3ClientFactoryBenchmarks
{
    private S3ClientFactory _factory = null!;

    [GlobalSetup]
    public void Setup()
    {
        var settings = new EventStreamS3Settings("s3",
            serviceUrl: "http://localhost:9000",
            accessKey: "minioadmin",
            secretKey: "minioadmin");
        _factory = new S3ClientFactory(settings);
    }

    [Benchmark(Baseline = true)]
    public object CreateClient_FirstTime()
    {
        // Each iteration creates a fresh factory to test first-time creation
        var settings = new EventStreamS3Settings("s3",
            serviceUrl: "http://localhost:9000",
            accessKey: "minioadmin",
            secretKey: "minioadmin");
        var factory = new S3ClientFactory(settings);
        return factory.CreateClient("bench");
    }

    [Benchmark]
    public object CreateClient_Cached()
    {
        return _factory.CreateClient("cached-client");
    }

    [Benchmark]
    public object CreateClient_MultipleDifferentNames()
    {
        var factory = new S3ClientFactory(new EventStreamS3Settings("s3",
            serviceUrl: "http://localhost:9000",
            accessKey: "minioadmin",
            secretKey: "minioadmin"));

        factory.CreateClient("client-a");
        factory.CreateClient("client-b");
        return factory.CreateClient("client-c");
    }
}
