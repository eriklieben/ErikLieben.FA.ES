using System.Text.Json;
using ErikLieben.FA.ES.Testing.Assertions;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Assertions;

// Collection definition to disable parallelization for snapshot tests
// since SnapshotAssertion uses a static SnapshotDirectory property
[CollectionDefinition("SnapshotTests", DisableParallelization = true)]
public class SnapshotTestsCollection { }

public class SnapshotAssertionTests
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    [Collection("SnapshotTests")]
    public class SnapshotDirectoryTests
    {
        [Fact]
        public void Should_throw_when_set_to_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.SnapshotDirectory = null!);
        }

        [Fact]
        public void Should_allow_setting_custom_directory()
        {
            var originalDir = SnapshotAssertion.SnapshotDirectory;
            try
            {
                SnapshotAssertion.SnapshotDirectory = "custom_snapshots";
                Assert.Equal("custom_snapshots", SnapshotAssertion.SnapshotDirectory);
            }
            finally
            {
                SnapshotAssertion.SnapshotDirectory = originalDir;
            }
        }
    }

    [Collection("SnapshotTests")]
    public class MatchesSnapshot : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public MatchesSnapshot()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Should_throw_when_actual_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshot<object>(null!, "test"));
        }

        [Fact]
        public void Should_throw_when_snapshot_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshot("value", null!));
        }

        [Fact]
        public void Should_create_snapshot_on_first_run()
        {
            var testObject = new { Name = "Test", Value = 42 };

            SnapshotAssertion.MatchesSnapshot(testObject, "first_run");

            var snapshotPath = Path.Combine(_testSnapshotDir, "first_run.json");
            Assert.True(File.Exists(snapshotPath));
        }

        [Fact]
        public void Should_match_existing_snapshot()
        {
            var testObject = new { Name = "Test", Value = 42 };
            var snapshotPath = Path.Combine(_testSnapshotDir, "existing.json");
            var json = JsonSerializer.Serialize(testObject, IndentedJsonOptions);
            File.WriteAllText(snapshotPath, json);

            // Act & Assert - matching snapshot should not throw
            SnapshotAssertion.MatchesSnapshot(testObject, "existing");
            Assert.True(true);
        }

        [Fact]
        public void Should_throw_when_snapshot_does_not_match()
        {
            var snapshotPath = Path.Combine(_testSnapshotDir, "mismatch.json");
            File.WriteAllText(snapshotPath, """{"Name":"Original","Value":1}""");

            var testObject = new { Name = "Changed", Value = 2 };

            var ex = Assert.Throws<TestAssertionException>(() =>
                SnapshotAssertion.MatchesSnapshot(testObject, "mismatch"));

            Assert.Contains("mismatch", ex.Message);
        }
    }

    [Collection("SnapshotTests")]
    public class MatchesSnapshotAsync : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public MatchesSnapshotAsync()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Should_throw_when_actual_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshotAsync<object>(null!, "test"));
        }

        [Fact]
        public async Task Should_throw_when_snapshot_name_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshotAsync("value", null!));
        }

        [Fact]
        public async Task Should_create_snapshot_on_first_run()
        {
            var testObject = new { Name = "Test", Value = 42 };

            await SnapshotAssertion.MatchesSnapshotAsync(testObject, "first_run_async");

            var snapshotPath = Path.Combine(_testSnapshotDir, "first_run_async.json");
            Assert.True(File.Exists(snapshotPath));
        }

        [Fact]
        public async Task Should_match_existing_snapshot()
        {
            var testObject = new { Name = "Test", Value = 42 };
            var snapshotPath = Path.Combine(_testSnapshotDir, "existing_async.json");
            var json = JsonSerializer.Serialize(testObject, IndentedJsonOptions);
            await File.WriteAllTextAsync(snapshotPath, json);

            // Act & Assert - matching snapshot should not throw
            await SnapshotAssertion.MatchesSnapshotAsync(testObject, "existing_async");
            Assert.True(true);
        }
    }

    [Collection("SnapshotTests")]
    public class UpdateSnapshot : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public UpdateSnapshot()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Should_throw_when_actual_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.UpdateSnapshot<object>(null!, "test"));
        }

        [Fact]
        public void Should_throw_when_snapshot_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.UpdateSnapshot("value", null!));
        }

        [Fact]
        public void Should_create_snapshot_file()
        {
            var testObject = new { Name = "Test" };

            SnapshotAssertion.UpdateSnapshot(testObject, "update_test");

            var snapshotPath = Path.Combine(_testSnapshotDir, "update_test.json");
            Assert.True(File.Exists(snapshotPath));
        }

        [Fact]
        public void Should_overwrite_existing_snapshot()
        {
            var snapshotPath = Path.Combine(_testSnapshotDir, "overwrite.json");
            File.WriteAllText(snapshotPath, """{"Old":"Value"}""");

            var testObject = new { New = "Value" };
            SnapshotAssertion.UpdateSnapshot(testObject, "overwrite");

            var content = File.ReadAllText(snapshotPath);
            Assert.Contains("New", content);
            Assert.DoesNotContain("Old", content);
        }
    }

    [Collection("SnapshotTests")]
    public class UpdateSnapshotAsync : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public UpdateSnapshotAsync()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Should_throw_when_actual_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                SnapshotAssertion.UpdateSnapshotAsync<object>(null!, "test"));
        }

        [Fact]
        public async Task Should_throw_when_snapshot_name_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                SnapshotAssertion.UpdateSnapshotAsync("value", null!));
        }

        [Fact]
        public async Task Should_create_snapshot_file()
        {
            var testObject = new { Name = "Test" };

            await SnapshotAssertion.UpdateSnapshotAsync(testObject, "update_async_test");

            var snapshotPath = Path.Combine(_testSnapshotDir, "update_async_test.json");
            Assert.True(File.Exists(snapshotPath));
        }
    }

    [Collection("SnapshotTests")]
    public class SnapshotOptionsTests : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public SnapshotOptionsTests()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Should_use_json_format_by_default()
        {
            var options = new SnapshotOptions();

            Assert.Equal(SnapshotFormat.Json, options.Format);
        }

        [Fact]
        public void Should_ignore_whitespace_by_default()
        {
            var options = new SnapshotOptions();

            Assert.True(options.IgnoreWhitespace);
        }

        [Fact]
        public void Should_pretty_print_by_default()
        {
            var options = new SnapshotOptions();

            Assert.True(options.PrettyPrint);
        }

        [Fact]
        public void Should_have_empty_ignored_properties_by_default()
        {
            var options = new SnapshotOptions();

            Assert.Empty(options.IgnoredProperties);
        }

        [Fact]
        public void Should_use_text_format_when_configured()
        {
            var testObject = "plain text value";
            var options = new SnapshotOptions { Format = SnapshotFormat.Text };

            SnapshotAssertion.UpdateSnapshot(testObject, "text_format", options);

            var snapshotPath = Path.Combine(_testSnapshotDir, "text_format.txt");
            Assert.True(File.Exists(snapshotPath));
            Assert.Equal("plain text value", File.ReadAllText(snapshotPath));
        }

        [Fact]
        public void Should_filter_ignored_properties()
        {
            var testObject = new { Name = "Test", Timestamp = DateTime.Now, Id = Guid.NewGuid() };
            var options = new SnapshotOptions
            {
                IgnoredProperties = new List<string> { "Timestamp", "Id" }
            };

            SnapshotAssertion.UpdateSnapshot(testObject, "ignored_props", options);

            var snapshotPath = Path.Combine(_testSnapshotDir, "ignored_props.json");
            var content = File.ReadAllText(snapshotPath);
            Assert.Contains("Name", content);
            Assert.DoesNotContain("Timestamp", content);
            Assert.DoesNotContain("Id", content);
        }

        [Fact]
        public void Should_use_custom_json_options()
        {
            var testObject = new { camelCase = "value" };
            var options = new SnapshotOptions
            {
                JsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                }
            };

            SnapshotAssertion.UpdateSnapshot(testObject, "custom_json", options);

            var snapshotPath = Path.Combine(_testSnapshotDir, "custom_json.json");
            var content = File.ReadAllText(snapshotPath);
            Assert.Contains("camelCase", content);
        }
    }

    [Collection("SnapshotTests")]
    public class MatchesSnapshotWithComparer : IDisposable
    {
        private readonly string _testSnapshotDir;
        private readonly string _originalDir;

        public MatchesSnapshotWithComparer()
        {
            _originalDir = SnapshotAssertion.SnapshotDirectory;
            _testSnapshotDir = Path.Combine(Path.GetTempPath(), $"snapshot_tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSnapshotDir);
            SnapshotAssertion.SnapshotDirectory = _testSnapshotDir;
        }

        public void Dispose()
        {
            SnapshotAssertion.SnapshotDirectory = _originalDir;
            if (Directory.Exists(_testSnapshotDir))
            {
                Directory.Delete(_testSnapshotDir, recursive: true);
            }
            GC.SuppressFinalize(this);
        }

        private class TestComparer : ISnapshotComparer<TestData>
        {
            public bool Matches(TestData actual, TestData expected, out string? differenceMessage)
            {
                if (actual.Name != expected.Name)
                {
                    differenceMessage = $"Name mismatch: {actual.Name} vs {expected.Name}";
                    return false;
                }
                differenceMessage = null;
                return true;
            }
        }

        public record TestData(string Name, int Value);

        [Fact]
        public void Should_throw_when_actual_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshot<TestData>(null!, "test", new TestComparer()));
        }

        [Fact]
        public void Should_throw_when_snapshot_name_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshot(new TestData("Test", 1), null!, new TestComparer()));
        }

        [Fact]
        public void Should_throw_when_comparer_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SnapshotAssertion.MatchesSnapshot(new TestData("Test", 1), "test", (ISnapshotComparer<TestData>)null!));
        }

        [Fact]
        public void Should_create_snapshot_when_not_exists()
        {
            var data = new TestData("Test", 1);

            SnapshotAssertion.MatchesSnapshot(data, "comparer_first_run", new TestComparer());

            var snapshotPath = Path.Combine(_testSnapshotDir, "comparer_first_run.json");
            Assert.True(File.Exists(snapshotPath));
        }

        [Fact]
        public void Should_use_custom_comparer()
        {
            var snapshotPath = Path.Combine(_testSnapshotDir, "comparer_match.json");
            var json = JsonSerializer.Serialize(new TestData("Test", 1));
            File.WriteAllText(snapshotPath, json);

            // Value differs but comparer only checks Name
            var data = new TestData("Test", 999);

            // Act & Assert - should not throw because comparer ignores Value
            SnapshotAssertion.MatchesSnapshot(data, "comparer_match", new TestComparer());
            Assert.True(true);
        }

        [Fact]
        public void Should_throw_when_comparer_returns_false()
        {
            var snapshotPath = Path.Combine(_testSnapshotDir, "comparer_fail.json");
            var json = JsonSerializer.Serialize(new TestData("Original", 1));
            File.WriteAllText(snapshotPath, json);

            var data = new TestData("Different", 1);

            var ex = Assert.Throws<TestAssertionException>(() =>
                SnapshotAssertion.MatchesSnapshot(data, "comparer_fail", new TestComparer()));

            Assert.Contains("Name mismatch", ex.Message);
        }
    }
}
