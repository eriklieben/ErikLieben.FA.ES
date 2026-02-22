using BenchmarkDotNet.Attributes;
using ErikLieben.FA.ES.S3.Extensions;
using System.Text;

namespace ErikLieben.FA.ES.S3.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class S3ExtensionsBenchmarks
{
    private string _smallJson = null!;
    private string _mediumJson = null!;
    private string _largeJson = null!;
    private byte[] _smallJsonBytes = null!;
    private byte[] _mediumJsonBytes = null!;
    private byte[] _largeJsonBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallJson = "{\"id\":\"test\",\"name\":\"small\"}";
        _mediumJson = new string('x', 10_000);
        _largeJson = new string('x', 100_000);

        _smallJsonBytes = Encoding.UTF8.GetBytes(_smallJson);
        _mediumJsonBytes = Encoding.UTF8.GetBytes(_mediumJson);
        _largeJsonBytes = Encoding.UTF8.GetBytes(_largeJson);
    }

    [Benchmark(Baseline = true)]
    public string ComputeSha256_SmallString()
    {
        return S3Extensions.ComputeSha256Hash(_smallJson);
    }

    [Benchmark]
    public string ComputeSha256_MediumString()
    {
        return S3Extensions.ComputeSha256Hash(_mediumJson);
    }

    [Benchmark]
    public string ComputeSha256_LargeString()
    {
        return S3Extensions.ComputeSha256Hash(_largeJson);
    }

    [Benchmark]
    public string ComputeSha256_SmallBytes()
    {
        return S3Extensions.ComputeSha256Hash(_smallJsonBytes, 0, _smallJsonBytes.Length);
    }

    [Benchmark]
    public string ComputeSha256_MediumBytes()
    {
        return S3Extensions.ComputeSha256Hash(_mediumJsonBytes, 0, _mediumJsonBytes.Length);
    }

    [Benchmark]
    public string ComputeSha256_LargeBytes()
    {
        return S3Extensions.ComputeSha256Hash(_largeJsonBytes, 0, _largeJsonBytes.Length);
    }
}
