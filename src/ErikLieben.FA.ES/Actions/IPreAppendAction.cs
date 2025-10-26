using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Defines an action that executes before an event is appended to the stream.
/// </summary>
public interface IPreAppendAction : IAction
{
    /// <summary>
    /// Executes before an event is appended, allowing validation or modification of the data.
    /// </summary>
    /// <typeparam name="T">The type of data being processed.</typeparam>
    /// <param name="data">The current data state.</param>
    /// <param name="event">The event to be appended.</param>
    /// <param name="objectDocument">The object document associated with the stream.</param>
    /// <returns>A function that returns the potentially modified data.</returns>
    Func<T> PreAppend<T>(T data, JsonEvent @event, IObjectDocument objectDocument) where T : class;
}

