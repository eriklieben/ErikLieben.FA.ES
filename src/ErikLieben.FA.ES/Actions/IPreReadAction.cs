using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Defines an action that executes before an event is read from the stream.
/// </summary>
public interface IPreReadAction : IAction
{
    /// <summary>
    /// Executes before an event is read, allowing preparation or modification of the data.
    /// </summary>
    /// <typeparam name="T">The type of data being processed.</typeparam>
    /// <param name="data">The current data state.</param>
    /// <param name="event">The event to be read.</param>
    /// <param name="objectDocument">The object document associated with the stream.</param>
    /// <returns>A function that returns the potentially modified data.</returns>
    Func<T> PreRead<T>(T data, JsonEvent @event, IObjectDocument objectDocument) where T : class;
}