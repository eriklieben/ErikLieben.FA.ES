using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;

namespace ErikLieben.FA.ES.Testing.InMemory;

public class InMemorySnapShotStore : ISnapShotStore
{
    private readonly Dictionary<string, (IBase Snapshot, DateTimeOffset CreatedAt)> snapshots = new();

    public Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default) where T : class, IBase
    {
        var documentPath = CreatePath(document.Active.StreamIdentifier, version, name);
        if (snapshots.TryGetValue(documentPath, out var entry))
        {
            return Task.FromResult(entry.Snapshot as T);
        }
        return Task.FromResult<T?>(null);
    }

    public Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        var documentPath = CreatePath(document.Active.StreamIdentifier, version, name);
        if (snapshots.TryGetValue(documentPath, out var entry))
        {
            return Task.FromResult<object?>(entry.Snapshot);
        }
        return Task.FromResult<object?>(null);
    }

    public Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null, CancellationToken cancellationToken = default)
    {
        var documentPath = CreatePath(document.Active.StreamIdentifier, version, name);
        snapshots[documentPath] = (@object, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SnapshotMetadata>> ListSnapshotsAsync(
        IObjectDocument document,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"snapshot/{document.Active.StreamIdentifier}-";
        var result = new List<SnapshotMetadata>();

        result.AddRange(snapshots
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kvp => ParsePath(kvp.Key, kvp.Value.CreatedAt))
            .Where(metadata => metadata is not null)!);

        return Task.FromResult<IReadOnlyList<SnapshotMetadata>>(
            result.OrderByDescending(s => s.Version).ToList());
    }

    public Task<bool> DeleteAsync(
        IObjectDocument document,
        int version,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var documentPath = CreatePath(document.Active.StreamIdentifier, version, name);
        return Task.FromResult(snapshots.Remove(documentPath));
    }

    public Task<int> DeleteManyAsync(
        IObjectDocument document,
        IEnumerable<int> versions,
        CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        foreach (var version in versions)
        {
            var documentPath = CreatePath(document.Active.StreamIdentifier, version, null);
            if (snapshots.Remove(documentPath))
            {
                deleted++;
            }
        }
        return Task.FromResult(deleted);
    }

    private static string CreatePath(string streamIdentifier, int version, string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? $"snapshot/{streamIdentifier}-{version:d20}.json"
            : $"snapshot/{streamIdentifier}-{version:d20}_{name}.json";
    }

    private static SnapshotMetadata? ParsePath(string path, DateTimeOffset createdAt)
    {
        // Pattern: snapshot/{streamId}-{version:d20}.json or snapshot/{streamId}-{version:d20}_{name}.json
        var lastDash = path.LastIndexOf('-');
        if (lastDash < 0) return null;

        var suffix = path[(lastDash + 1)..];
        var dotIndex = suffix.IndexOf('.');
        if (dotIndex < 0) return null;

        var versionPart = suffix[..dotIndex];
        string? name = null;

        var underscoreIndex = versionPart.IndexOf('_');
        if (underscoreIndex > 0)
        {
            name = versionPart[(underscoreIndex + 1)..];
            versionPart = versionPart[..underscoreIndex];
        }

        if (!int.TryParse(versionPart, out var version))
            return null;

        return new SnapshotMetadata(version, createdAt, name, null);
    }
}
