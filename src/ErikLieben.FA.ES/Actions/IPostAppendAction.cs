using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Defines an action that executes after an event has been appended to the stream.
/// </summary>
public interface IPostAppendAction : IAction
{
    /// <summary>
    /// Executes after an event has been appended, allowing modification or transformation of the data.
    /// </summary>
    /// <typeparam name="T">The type of data being processed.</typeparam>
    /// <param name="data">The current data state.</param>
    /// <param name="event">The event that was appended.</param>
    /// <param name="document">The object document associated with the stream.</param>
    /// <returns>A function that returns the potentially modified data.</returns>
    Func<T> PostAppend<T>(T data, JsonEvent @event, IObjectDocument document) where T : class;
}
