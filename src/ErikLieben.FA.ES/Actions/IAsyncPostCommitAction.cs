using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Defines an action that executes asynchronously after events are committed to the stream.
/// </summary>
public interface IAsyncPostCommitAction : IAction
{
    /// <summary>
    /// Executes asynchronously after events have been successfully committed to the event stream.
    /// </summary>
    /// <param name="events">The events that were committed.</param>
    /// <param name="document">The object document associated with the stream.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document);
}
