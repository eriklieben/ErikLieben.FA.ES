using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class S3SettingsBenchmarks
{
    [Benchmark(Baseline = true)]
    public EventStreamS3Settings CreateSettings_Minimal()
    {
        return new EventStreamS3Settings("s3");
    }

    [Benchmark]
    public EventStreamS3Settings CreateSettings_Full()
    {
        return new EventStreamS3Settings("s3",
            defaultDocumentStore: "doc-store",
            defaultSnapShotStore: "snap-store",
            defaultDocumentTagStore: "tag-store",
            serviceUrl: "http://localhost:9000",
            accessKey: "minioadmin",
            secretKey: "minioadmin",
            region: "eu-west-1",
            bucketName: "custom-bucket",
            forcePathStyle: true,
            autoCreateBucket: false,
            enableStreamChunks: true,
            defaultChunkSize: 500,
            maxConnectionsPerServer: 100);
    }
}
