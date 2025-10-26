namespace ErikLieben.FA.ES;

/// <summary>
/// Defines concurrency constraints for event stream sessions.
/// </summary>
public enum Constraint
{
    /// <summary>
    /// No concurrency constraint - session can append to new or existing streams.
    /// </summary>
    Loose = 0,

    /// <summary>
    /// Session can only append to a new stream (version must be 0).
    /// </summary>
    New = 1,

    /// <summary>
    /// Session can only append to an existing stream (version must be greater than 0).
    /// </summary>
    Existing = 2,
}