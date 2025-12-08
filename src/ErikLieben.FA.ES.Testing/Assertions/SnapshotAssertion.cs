using System.Text.Json;

namespace ErikLieben.FA.ES.Testing.Assertions;

/// <summary>
/// Provides snapshot testing functionality for capturing and comparing complex state.
/// </summary>
public static class SnapshotAssertion
{
    private static string _snapshotDirectory = "__snapshots__";
    private static readonly string UpdateSnapshotsEnvVar = "UPDATE_SNAPSHOTS";

    /// <summary>
    /// Gets or sets the directory where snapshots are stored.
    /// </summary>
    public static string SnapshotDirectory
    {
        get => _snapshotDirectory;
        set => _snapshotDirectory = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Verifies that the actual value matches the stored snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the value to snapshot.</typeparam>
    /// <param name="actual">The actual value to compare.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <exception cref="TestAssertionException">Thrown when the snapshot doesn't match or can't be loaded.</exception>
    public static void MatchesSnapshot<T>(T actual, string snapshotName, SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(snapshotName);

        options ??= new SnapshotOptions();

        var snapshotPath = GetSnapshotPath(snapshotName, options.Format);
        var actualContent = SerializeToSnapshot(actual, options);

        // Check if we should update snapshots
        if (ShouldUpdateSnapshots())
        {
            UpdateSnapshot(actual, snapshotName, options);
            return;
        }

        // Load existing snapshot
        if (!File.Exists(snapshotPath))
        {
            // First run - create snapshot
            UpdateSnapshot(actual, snapshotName, options);
            return;
        }

        var expectedContent = File.ReadAllText(snapshotPath);

        // Compare
        if (!CompareSnapshots(actualContent, expectedContent, options))
        {
            throw new TestAssertionException(
                $"Snapshot '{snapshotName}' does not match.\n" +
                $"Expected:\n{expectedContent}\n\n" +
                $"Actual:\n{actualContent}\n\n" +
                $"To update snapshots, set environment variable {UpdateSnapshotsEnvVar}=true");
        }
    }

    /// <summary>
    /// Asynchronously verifies that the actual value matches the stored snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the value to snapshot.</typeparam>
    /// <param name="actual">The actual value to compare.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <param name="options">Optional snapshot options.</param>
    /// <exception cref="TestAssertionException">Thrown when the snapshot doesn't match or can't be loaded.</exception>
    public static async Task MatchesSnapshotAsync<T>(T actual, string snapshotName, SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(snapshotName);

        options ??= new SnapshotOptions();

        var snapshotPath = GetSnapshotPath(snapshotName, options.Format);
        var actualContent = SerializeToSnapshot(actual, options);

        // Check if we should update snapshots
        if (ShouldUpdateSnapshots())
        {
            await UpdateSnapshotAsync(actual, snapshotName, options);
            return;
        }

        // Load existing snapshot
        if (!File.Exists(snapshotPath))
        {
            // First run - create snapshot
            await UpdateSnapshotAsync(actual, snapshotName, options);
            return;
        }

        var expectedContent = await File.ReadAllTextAsync(snapshotPath);

        // Compare
        if (!CompareSnapshots(actualContent, expectedContent, options))
        {
            throw new TestAssertionException(
                $"Snapshot '{snapshotName}' does not match.\n" +
                $"Expected:\n{expectedContent}\n\n" +
                $"Actual:\n{actualContent}\n\n" +
                $"To update snapshots, set environment variable {UpdateSnapshotsEnvVar}=true");
        }
    }

    /// <summary>
    /// Updates the stored snapshot with the actual value.
    /// </summary>
    /// <typeparam name="T">The type of the value to snapshot.</typeparam>
    /// <param name="actual">The actual value to store.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <param name="options">Optional snapshot options.</param>
    public static void UpdateSnapshot<T>(T actual, string snapshotName, SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(snapshotName);

        options ??= new SnapshotOptions();

        var snapshotPath = GetSnapshotPath(snapshotName, options.Format);
        var directory = Path.GetDirectoryName(snapshotPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = SerializeToSnapshot(actual, options);
        File.WriteAllText(snapshotPath, content);
    }

    /// <summary>
    /// Asynchronously updates the stored snapshot with the actual value.
    /// </summary>
    /// <typeparam name="T">The type of the value to snapshot.</typeparam>
    /// <param name="actual">The actual value to store.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <param name="options">Optional snapshot options.</param>
    public static async Task UpdateSnapshotAsync<T>(T actual, string snapshotName, SnapshotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(snapshotName);

        options ??= new SnapshotOptions();

        var snapshotPath = GetSnapshotPath(snapshotName, options.Format);
        var directory = Path.GetDirectoryName(snapshotPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = SerializeToSnapshot(actual, options);
        await File.WriteAllTextAsync(snapshotPath, content);
    }

    /// <summary>
    /// Verifies that the actual value matches the expected value using a custom comparer.
    /// </summary>
    /// <typeparam name="T">The type of the value to compare.</typeparam>
    /// <param name="actual">The actual value.</param>
    /// <param name="snapshotName">The name of the snapshot file (without extension).</param>
    /// <param name="comparer">The custom comparer to use.</param>
    /// <exception cref="TestAssertionException">Thrown when the values don't match.</exception>
    public static void MatchesSnapshot<T>(T actual, string snapshotName, ISnapshotComparer<T> comparer)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(snapshotName);
        ArgumentNullException.ThrowIfNull(comparer);

        var options = new SnapshotOptions();
        var snapshotPath = GetSnapshotPath(snapshotName, options.Format);

        if (!File.Exists(snapshotPath))
        {
            UpdateSnapshot(actual, snapshotName, options);
            return;
        }

        var expectedJson = File.ReadAllText(snapshotPath);
        var expected = JsonSerializer.Deserialize<T>(expectedJson, options.JsonOptions);

        if (EqualityComparer<T>.Default.Equals(expected, default))
        {
            throw new TestAssertionException($"Failed to deserialize snapshot '{snapshotName}'.");
        }

        if (!comparer.Matches(actual, expected, out var differenceMessage))
        {
            throw new TestAssertionException(
                $"Snapshot '{snapshotName}' does not match.\n{differenceMessage}");
        }
    }

    private static string GetSnapshotPath(string snapshotName, SnapshotFormat format)
    {
        var extension = format switch
        {
            SnapshotFormat.Json => ".json",
            SnapshotFormat.Yaml => ".yaml",
            SnapshotFormat.Text => ".txt",
            _ => throw new ArgumentException($"Unsupported snapshot format: {format}")
        };

        return Path.Combine(_snapshotDirectory, $"{snapshotName}{extension}");
    }

    private static string SerializeToSnapshot<T>(T value, SnapshotOptions options)
    {
        if (options.Format == SnapshotFormat.Text)
        {
            return value?.ToString() ?? string.Empty;
        }

        // For JSON format
        var jsonOptions = options.JsonOptions ?? new JsonSerializerOptions
        {
            WriteIndented = options.PrettyPrint
        };

        var json = JsonSerializer.Serialize(value, jsonOptions);

        // Filter ignored properties if specified
        if (options.IgnoredProperties.Count > 0)
        {
            json = FilterIgnoredProperties(json, options.IgnoredProperties);
        }

        return json;
    }

    private static string FilterIgnoredProperties(string json, List<string> ignoredProperties)
    {
        var document = JsonDocument.Parse(json);
        var filtered = FilterJsonElement(document.RootElement, ignoredProperties);
        return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object? FilterJsonElement(JsonElement element, List<string> ignoredProperties)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var property in element.EnumerateObject())
            {
                if (!ignoredProperties.Contains(property.Name))
                {
                    dict[property.Name] = FilterJsonElement(property.Value, ignoredProperties);
                }
            }
            return dict;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(FilterJsonElement(item, ignoredProperties));
            }
            return list;
        }
        else
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            };
        }
    }

    private static bool CompareSnapshots(string actual, string expected, SnapshotOptions options)
    {
        if (options.IgnoreWhitespace)
        {
            actual = actual.Trim();
            expected = expected.Trim();
        }

        return actual == expected;
    }

    private static bool ShouldUpdateSnapshots()
    {
        var envValue = Environment.GetEnvironmentVariable(UpdateSnapshotsEnvVar);
        return !string.IsNullOrEmpty(envValue) &&
               (envValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                envValue.Equals("1", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Options for configuring snapshot behavior.
/// </summary>
public class SnapshotOptions
{
    /// <summary>
    /// Gets or sets the snapshot format.
    /// </summary>
    public SnapshotFormat Format { get; set; } = SnapshotFormat.Json;

    /// <summary>
    /// Gets or sets whether to ignore whitespace differences when comparing snapshots.
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to pretty-print the snapshot output.
    /// </summary>
    public bool PrettyPrint { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of property names to ignore when comparing snapshots.
    /// </summary>
    public List<string> IgnoredProperties { get; set; } = new();

    /// <summary>
    /// Gets or sets custom JSON serializer options.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }
}

/// <summary>
/// Defines the format for storing snapshots.
/// </summary>
public enum SnapshotFormat
{
    /// <summary>
    /// JSON format (default).
    /// </summary>
    Json,

    /// <summary>
    /// YAML format.
    /// </summary>
    Yaml,

    /// <summary>
    /// Plain text format.
    /// </summary>
    Text
}

/// <summary>
/// Defines a custom comparer for snapshot comparison.
/// </summary>
/// <typeparam name="T">The type being compared.</typeparam>
public interface ISnapshotComparer<T>
{
    /// <summary>
    /// Determines whether the actual value matches the expected value.
    /// </summary>
    /// <param name="actual">The actual value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="differenceMessage">A message describing the difference if the values don't match.</param>
    /// <returns>True if the values match; otherwise, false.</returns>
    bool Matches(T actual, T expected, out string? differenceMessage);
}
