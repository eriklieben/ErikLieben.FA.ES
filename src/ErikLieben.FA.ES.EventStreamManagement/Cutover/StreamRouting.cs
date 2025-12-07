namespace ErikLieben.FA.ES.EventStreamManagement.Cutover;

/// <summary>
/// Defines routing information for reads and writes during different migration phases.
/// </summary>
/// <param name="Phase">The current migration phase.</param>
/// <param name="PrimaryReadStream">The primary stream to read from.</param>
/// <param name="PrimaryWriteStream">The primary stream to write to.</param>
/// <param name="SecondaryReadStream">Optional secondary stream to read from (fallback).</param>
/// <param name="SecondaryWriteStream">Optional secondary stream to write to (dual-write).</param>
public record StreamRouting(
    MigrationPhase Phase,
    string PrimaryReadStream,
    string PrimaryWriteStream,
    string? SecondaryReadStream = null,
    string? SecondaryWriteStream = null)
{
    /// <summary>
    /// Gets a value indicating whether dual-write mode is active.
    /// </summary>
    public bool IsDualWriteActive => SecondaryWriteStream is not null;

    /// <summary>
    /// Gets a value indicating whether dual-read mode is active (with fallback).
    /// </summary>
    public bool IsDualReadActive => SecondaryReadStream is not null;

    /// <summary>
    /// Creates routing for normal operations (single stream).
    /// </summary>
    public static StreamRouting Normal(string streamIdentifier) =>
        new(MigrationPhase.Normal, streamIdentifier, streamIdentifier);

    /// <summary>
    /// Creates routing for dual-write phase.
    /// </summary>
    public static StreamRouting DualWrite(string oldStream, string newStream) =>
        new(MigrationPhase.DualWrite, oldStream, oldStream, null, newStream);

    /// <summary>
    /// Creates routing for dual-read phase.
    /// </summary>
    public static StreamRouting DualRead(string oldStream, string newStream) =>
        new(MigrationPhase.DualRead, newStream, newStream, oldStream, oldStream);

    /// <summary>
    /// Creates routing for completed cutover.
    /// </summary>
    public static StreamRouting Cutover(string newStream) =>
        new(MigrationPhase.Cutover, newStream, newStream);
}
