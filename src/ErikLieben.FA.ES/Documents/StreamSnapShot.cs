namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Describes a snapshot of the aggregate materialized at a specific stream version.
/// </summary>
public class StreamSnapShot
{
    /// <summary>
    /// Gets or sets the version up to which the snapshot was taken.
    /// </summary>
    public required int UntilVersion { get; set; }

    /// <summary>
    /// Gets or sets an optional name or version of the snapshot type.
    /// </summary>
    public string? Name { get; set; }
}
