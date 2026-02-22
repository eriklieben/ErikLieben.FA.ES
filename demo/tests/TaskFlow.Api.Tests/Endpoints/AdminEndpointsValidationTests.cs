using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace TaskFlow.Api.Tests.Endpoints;

/// <summary>
/// Tests for validation logic in AdminEndpoints, covering GUID parsing,
/// container name allowlisting, benchmark filename validation, and error detail gating.
/// </summary>
public class AdminEndpointsValidationTests
{
    #region GUID Validation

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE")]
    public void GuidTryParse_ValidGuid_Succeeds(string input)
    {
        Assert.True(Guid.TryParse(input, out _));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")]
    [InlineData("550e8400-e29b-41d4-a716")]
    [InlineData("' OR 1=1 --")]
    [InlineData("../../../etc/passwd")]
    public void GuidTryParse_InvalidGuid_Fails(string input)
    {
        Assert.False(Guid.TryParse(input, out _));
    }

    #endregion

    #region Container Name Allowlist

    /// <summary>
    /// Mirror of the AllowedCosmosContainers set from AdminEndpoints.
    /// We test that the allowlist logic correctly accepts valid containers
    /// and rejects invalid ones.
    /// </summary>
    private static readonly HashSet<string> AllowedCosmosContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "documents", "events", "tags", "projections", "leases"
    };

    [Theory]
    [InlineData("documents")]
    [InlineData("events")]
    [InlineData("tags")]
    [InlineData("projections")]
    [InlineData("leases")]
    public void AllowedContainers_ValidName_IsAccepted(string containerName)
    {
        Assert.Contains(containerName, AllowedCosmosContainers);
    }

    [Theory]
    [InlineData("Documents")]
    [InlineData("EVENTS")]
    [InlineData("Tags")]
    [InlineData("Projections")]
    [InlineData("LEASES")]
    public void AllowedContainers_CaseInsensitive_IsAccepted(string containerName)
    {
        Assert.Contains(containerName, AllowedCosmosContainers);
    }

    [Theory]
    [InlineData("secrets")]
    [InlineData("users")]
    [InlineData("admin")]
    [InlineData("passwords")]
    [InlineData("")]
    [InlineData("../documents")]
    [InlineData("documents; DROP TABLE")]
    public void AllowedContainers_InvalidName_IsRejected(string containerName)
    {
        Assert.DoesNotContain(containerName, AllowedCosmosContainers);
    }

    [Fact]
    public void AllowedContainers_NullDefault_FallsBackToDocuments()
    {
        // The endpoint uses: var targetContainer = containerName ?? "documents";
        string? containerName = null;
        var targetContainer = containerName ?? "documents";
        Assert.Contains(targetContainer, AllowedCosmosContainers);
    }

    [Fact]
    public void AllowedContainers_MatchesExpectedCount()
    {
        // Verify the set has exactly 5 entries — a change would indicate
        // someone added/removed a container name and tests should be updated.
        Assert.Equal(5, AllowedCosmosContainers.Count);
    }

    #endregion

    #region Benchmark Filename Validation

    /// <summary>
    /// Matches the regex from AdminEndpoints.GetBenchmarkFile:
    ///   @"^[\w\-\.]+$"
    /// Plus the additional ".." check.
    /// </summary>
    private static bool IsValidBenchmarkFilename(string filename)
    {
        if (!Regex.IsMatch(filename, @"^[\w\-\.]+$"))
            return false;
        if (filename.Contains(".."))
            return false;
        return true;
    }

    [Theory]
    [InlineData("ErikLieben.FA.ES.Benchmarks-net10-report-full.json")]
    [InlineData("benchmark-results.json")]
    [InlineData("results_2024.json")]
    [InlineData("a.json")]
    public void BenchmarkFilename_ValidNames_Pass(string filename)
    {
        Assert.True(IsValidBenchmarkFilename(filename));
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\windows\\system32\\config")]
    [InlineData("foo/../bar.json")]
    public void BenchmarkFilename_PathTraversal_Rejected(string filename)
    {
        Assert.False(IsValidBenchmarkFilename(filename));
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\windows\\system32")]
    [InlineData("file with spaces.json")]
    [InlineData("file;rm -rf.json")]
    [InlineData("file$(command).json")]
    [InlineData("")]
    public void BenchmarkFilename_UnsafeCharacters_Rejected(string filename)
    {
        Assert.False(IsValidBenchmarkFilename(filename));
    }

    [Fact]
    public void BenchmarkFilename_DoubleDot_InFilename_Rejected()
    {
        // Even if the regex would pass (e.g., "a..b" matches [\w\-\.]+),
        // the ".." check still catches it
        Assert.False(IsValidBenchmarkFilename("a..b"));
    }

    [Fact]
    public void BenchmarkFilePath_StartsWith_PreventsDirectoryEscape()
    {
        // Simulates the StartsWith check from GetBenchmarkFile
        var benchmarkResultsPath = Path.GetFullPath("/app/benchmarks/results");
        var validPath = Path.GetFullPath(Path.Combine(benchmarkResultsPath, "report.json"));
        var escapedPath = Path.GetFullPath("/app/benchmarks/other/evil.json");

        Assert.StartsWith(benchmarkResultsPath, validPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(escapedPath.StartsWith(benchmarkResultsPath, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region SafeErrorDetail Gating

    [Fact]
    public void SafeErrorDetail_InDevelopment_ReturnsExceptionMessage()
    {
        var env = CreateEnvironment(Environments.Development);
        var ex = new InvalidOperationException("Something broke");

        var detail = InvokeSafeErrorDetail(ex, env);

        Assert.Equal("Something broke", detail);
    }

    [Fact]
    public void SafeErrorDetail_InDevelopment_IncludesInnerExceptionMessage()
    {
        var env = CreateEnvironment(Environments.Development);
        var inner = new ArgumentException("inner cause");
        var ex = new InvalidOperationException("outer message", inner);

        var detail = InvokeSafeErrorDetail(ex, env);

        Assert.Contains("outer message", detail);
        Assert.Contains("inner cause", detail);
    }

    [Fact]
    public void SafeErrorDetail_InProduction_ReturnsGenericMessage()
    {
        var env = CreateEnvironment(Environments.Production);
        var ex = new InvalidOperationException("Secret DB connection string");

        var detail = InvokeSafeErrorDetail(ex, env);

        Assert.Equal("An internal error occurred. Check server logs for details.", detail);
        Assert.DoesNotContain("Secret", detail);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void SafeErrorDetail_NonDevelopment_HidesDetails(string environmentName)
    {
        var env = CreateEnvironment(environmentName);
        var ex = new Exception("password=abc123");

        var detail = InvokeSafeErrorDetail(ex, env);

        Assert.DoesNotContain("password", detail);
        Assert.DoesNotContain("abc123", detail);
    }

    /// <summary>
    /// Invokes the private SafeErrorDetail method on AdminEndpoints via reflection.
    /// </summary>
    private static string InvokeSafeErrorDetail(Exception ex, IWebHostEnvironment env)
    {
        var method = typeof(TaskFlow.Api.Endpoints.AdminEndpoints)
            .GetMethod("SafeErrorDetail", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [ex, env]);
        Assert.NotNull(result);

        return (string)result!;
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        return new TestWebHostEnvironment { EnvironmentName = environmentName };
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "TestApp";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    #endregion

    #region AllowedCosmosContainers Field Verification

    [Fact]
    public void AdminEndpoints_AllowedCosmosContainers_MatchesExpectedValues()
    {
        // Verify the actual field in AdminEndpoints via reflection
        var field = typeof(TaskFlow.Api.Endpoints.AdminEndpoints)
            .GetField("AllowedCosmosContainers", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        var containers = field!.GetValue(null) as HashSet<string>;
        Assert.NotNull(containers);
        Assert.Equal(5, containers!.Count);
        Assert.Contains("documents", containers);
        Assert.Contains("events", containers);
        Assert.Contains("tags", containers);
        Assert.Contains("projections", containers);
        Assert.Contains("leases", containers);
    }

    #endregion
}
