using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Validation;

/// <summary>
/// Captures the context in which a decision was made.
/// Include this with commands to enable staleness detection.
/// </summary>
/// <param name="CheckpointFingerprint">Checkpoint fingerprint from the projection that was viewed.</param>
/// <param name="StreamVersions">Specific stream versions from the checkpoint (optional, for detailed validation).</param>
/// <param name="DecisionTimestamp">When the decision was made (for timeout validation).</param>
public record DecisionContext(
    string? CheckpointFingerprint,
    Checkpoint? StreamVersions,
    DateTimeOffset DecisionTimestamp)
{
    /// <summary>
    /// An empty decision context representing no checkpoint information.
    /// </summary>
    public static readonly DecisionContext Empty = new(null, null, DateTimeOffset.MinValue);

    /// <summary>
    /// Returns true if this context has no checkpoint information.
    /// </summary>
    public bool IsEmpty => CheckpointFingerprint is null && (StreamVersions is null || StreamVersions.Count == 0);

    /// <summary>
    /// Creates a decision context from a projection's checkpoint.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="projection">The projection to extract checkpoint from.</param>
    /// <returns>A new DecisionContext with the projection's checkpoint information.</returns>
    public static DecisionContext FromProjection<T>(T projection) where T : Projection
        => new(projection.CheckpointFingerprint, projection.Checkpoint, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a decision context from just a fingerprint string.
    /// </summary>
    /// <param name="fingerprint">The checkpoint fingerprint.</param>
    /// <returns>A new DecisionContext with the fingerprint.</returns>
    public static DecisionContext FromFingerprint(string fingerprint)
        => new(fingerprint, null, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a decision context from an HTTP header value.
    /// </summary>
    /// <param name="headerValue">The header value, or null if not present.</param>
    /// <returns>The decision context, or Empty if the header is null.</returns>
    public static DecisionContext FromHeader(string? headerValue)
        => headerValue is null ? Empty : FromFingerprint(headerValue);
}
