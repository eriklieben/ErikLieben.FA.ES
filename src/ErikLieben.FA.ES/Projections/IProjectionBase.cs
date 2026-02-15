#pragma warning disable S1133 // Deprecated code - legacy Fold overloads maintained for backwards compatibility

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Defines the contract for projection types that can fold events into state and update to specific versions.
/// </summary>
public interface IProjectionBase
{
    /// <summary>
    /// Folds a single event into the projection state.
    /// </summary>
    /// <typeparam name="T">The type of the auxiliary data passed to the fold operation.</typeparam>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <param name="data">Optional auxiliary data; may be null.</param>
    /// <param name="context">Optional execution context; may be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Use Fold<T>(IEvent, VersionToken, T?, IExecutionContext?) instead. This overload will be removed in a future major version.")]
    Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null, IExecutionContext? context = null)
        where T: class;

    /// <summary>
    /// Folds a single event without auxiliary data or context.
    /// </summary>
    /// <param name="event">The event to fold.</param>
    /// <param name="document">The projection object document.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Obsolete("Use Fold(IEvent, VersionToken) instead. This overload will be removed in a future major version.")]
    Task Fold(IEvent @event, IObjectDocument document);

    /// <summary>
    /// Updates the projection to the specified version using optional typed context and data.
    /// </summary>
    /// <typeparam name="T">The type of the auxiliary data and execution context.</typeparam>
    /// <param name="token">The version token that identifies the object and target version.</param>
    /// <param name="context">Optional execution context carrying the parent event; may be null.</param>
    /// <param name="data">Optional auxiliary data used during folding; may be null.</param>
    /// <returns>A result indicating whether the update was processed or skipped due to projection status.</returns>
    Task<ProjectionUpdateResult> UpdateToVersion<T>(VersionToken token, IExecutionContextWithData<T>? context = null, T? data = null)
        where T : class;

    /// <summary>
    /// Updates the projection to the latest versions for all tracked streams.
    /// </summary>
    /// <param name="context">Optional execution context; may be null.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateToLatestVersion(IExecutionContext? context = null);

    /// <summary>
    /// Gets the checkpoint map storing the latest processed version identifiers per object stream.
    /// </summary>
    Checkpoint Checkpoint { get; }

    /// <summary>
    /// Gets or sets the fingerprint (SHA-256 hex string) computed from the checkpoint content; may be null.
    /// </summary>
    string? CheckpointFingerprint { get; set; }
}
