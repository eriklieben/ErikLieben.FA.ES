using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Defines an action that executes after an event has been read from the stream.
/// </summary>
public interface IPostReadAction : IAction
{
    /// <summary>
    /// Executes after an event has been read, allowing modification or transformation of the data.
    /// </summary>
    /// <typeparam name="T">The type of data being processed.</typeparam>
    /// <param name="data">The current data state.</param>
    /// <param name="event">The event that was read.</param>
    /// <param name="objectDocument">The object document associated with the stream.</param>
    /// <returns>A function that returns the potentially modified data.</returns>
    Func<T> PostRead<T>(T data, IEvent @event, IObjectDocument objectDocument) where T : class;
}
