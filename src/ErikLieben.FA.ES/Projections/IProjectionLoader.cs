namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Loads projections with automatic version routing.
/// </summary>
/// <remarks>
/// This service abstracts version management from consuming code.
/// By default, it loads the active version of a projection. Configuration
/// can override this to pin specific versions or load the latest rebuilding version.
/// </remarks>
public interface IProjectionLoader
{
    /// <summary>
    /// Loads the configured version of a projection (default: active).
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projection, or null if not found.</returns>
    Task<T?> GetAsync<T>(string objectId, CancellationToken cancellationToken = default)
        where T : Projection;

    /// <summary>
    /// Loads a specific version (for testing/debugging).
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="version">The schema version to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projection, or null if not found.</returns>
    Task<T?> GetVersionAsync<T>(string objectId, int version, CancellationToken cancellationToken = default)
        where T : Projection;

    /// <summary>
    /// Gets metadata about available versions for a projection.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Version metadata.</returns>
    Task<ProjectionVersionMetadata> GetVersionMetadataAsync<T>(
        string objectId,
        CancellationToken cancellationToken = default)
        where T : Projection;

    /// <summary>
    /// Loads a projection with schema version checking.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection and schema mismatch info.</returns>
    Task<ProjectionLoadResult<T>> GetWithVersionCheckAsync<T>(
        string objectId,
        CancellationToken cancellationToken = default)
        where T : Projection;
}

/// <summary>
/// Metadata about available projection versions.
/// </summary>
/// <param name="ActiveVersion">The currently active version.</param>
/// <param name="RebuildingVersion">Version being rebuilt, if any.</param>
/// <param name="AllVersions">All available versions.</param>
public record ProjectionVersionMetadata(
    int ActiveVersion,
    int? RebuildingVersion,
    IReadOnlyList<VersionInfo> AllVersions);

/// <summary>
/// Information about a specific projection version.
/// </summary>
/// <param name="Version">The schema version number.</param>
/// <param name="Status">The status of this version.</param>
/// <param name="CheckpointFingerprint">The checkpoint fingerprint.</param>
/// <param name="CreatedAt">When this version was created.</param>
public record VersionInfo(
    int Version,
    ProjectionStatus Status,
    string? CheckpointFingerprint,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result of loading a projection with version checking.
/// </summary>
/// <typeparam name="T">The projection type.</typeparam>
/// <param name="Projection">The loaded projection.</param>
/// <param name="SchemaMismatch">Whether a schema mismatch was detected.</param>
/// <param name="StoredVersion">The stored schema version.</param>
/// <param name="CodeVersion">The code schema version.</param>
public record ProjectionLoadResult<T>(
    T? Projection,
    bool SchemaMismatch,
    int StoredVersion,
    int CodeVersion)
    where T : Projection
{
    /// <summary>
    /// Creates a result for a successfully loaded projection.
    /// </summary>
    public static ProjectionLoadResult<T> Success(T projection) =>
        new(projection, projection.NeedsSchemaUpgrade, projection.SchemaVersion, projection.CodeSchemaVersion);

    /// <summary>
    /// Creates a result for a not found projection.
    /// </summary>
    public static ProjectionLoadResult<T> NotFound(int codeVersion) =>
        new(null, false, 0, codeVersion);
}
