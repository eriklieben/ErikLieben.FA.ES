namespace ErikLieben.FA.ES.Snapshots;

/// <summary>
/// Metadata about a stored snapshot.
/// </summary>
/// <param name="Version">The version at which the snapshot was taken.</param>
/// <param name="CreatedAt">The timestamp when the snapshot was created.</param>
/// <param name="Name">Optional name or variant of the snapshot.</param>
/// <param name="SizeBytes">Size of the snapshot in bytes, if known.</param>
public record SnapshotMetadata(
    int Version,
    DateTimeOffset CreatedAt,
    string? Name = null,
    long? SizeBytes = null)
{
    /// <summary>
    /// Gets the age of the snapshot relative to the current time.
    /// </summary>
    public TimeSpan Age => DateTimeOffset.UtcNow - CreatedAt;

    /// <summary>
    /// Determines if the snapshot is older than the specified duration.
    /// </summary>
    /// <param name="maxAge">The maximum allowed age.</param>
    /// <returns>True if the snapshot is older than maxAge.</returns>
    public bool IsOlderThan(TimeSpan maxAge) => Age > maxAge;
}
