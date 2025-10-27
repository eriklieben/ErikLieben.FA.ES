using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Represents the execution context for processing events in a projection, tracking the document and parent context hierarchy.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Gets a value indicating whether this context is the root context (has no parent).
    /// </summary>
    bool IsRoot { get; }

    /// <summary>
    /// Gets the object document being processed in this execution context.
    /// </summary>
    IObjectDocument Document { get; }

    /// <summary>
    /// Gets the parent execution context, or null if this is the root context.
    /// </summary>
    IExecutionContext? ParentContext { get; }
}

/// <summary>
/// Represents an execution context that carries additional data of type <typeparamref name="Td"/> along with event information.
/// </summary>
/// <typeparam name="Td">The type of data associated with this execution context.</typeparam>
public interface IExecutionContextWithData<out Td> : IExecutionContext where Td : class
{
    /// <summary>
    /// Gets the data item associated with this execution context, or inherited from the parent context if not set.
    /// </summary>
    Td? Item { get; }

    /// <summary>
    /// Gets the event that triggered this execution context.
    /// </summary>
    IEvent Event { get; }
}

/// <summary>
/// Represents an execution context with a strongly-typed event of type <typeparamref name="Te"/>.
/// </summary>
/// <typeparam name="Te">The type of the event payload.</typeparam>
public interface IExecutionContextWithEvent<out Te> : IExecutionContext where Te : class
{
    /// <summary>
    /// Gets the strongly-typed event that triggered this execution context.
    /// </summary>
    IEvent<Te> Event { get; }
}

/// <summary>
/// Represents an execution context with both strongly-typed event of type <typeparamref name="Te"/> and data of type <typeparamref name="Td"/>.
/// </summary>
/// <typeparam name="Te">The type of the event payload.</typeparam>
/// <typeparam name="Td">The type of data associated with this execution context.</typeparam>
public interface IExecutionContext<out Te, out Td> :
    IExecutionContextWithData<Td>, IExecutionContextWithEvent<Te> where Td : class where Te : class
{
    /// <summary>
    /// Gets the strongly-typed event that triggered this execution context. This property hides the less specific Event property from IExecutionContextWithData.
    /// </summary>
    new IEvent<Te> Event { get; }
}

/// <summary>
/// Provides a concrete implementation of an execution context that tracks events, documents, data items, and parent context relationships during projection processing.
/// </summary>
/// <typeparam name="Te">The type of the event payload.</typeparam>
/// <typeparam name="Td">The type of data associated with this execution context.</typeparam>
public class ExecutionContext<Te, Td>(
    IEvent<Te> @event,
    IObjectDocument objectDocument,
    Td? item,
    IExecutionContextWithData<Td>? parentContext = null)
    : IExecutionContext<Te, Td>
    where Te : class
    where Td : class
{
    private readonly Td? item = item;

    /// <summary>
    /// Gets the strongly-typed event that triggered this execution context.
    /// </summary>
    public IEvent<Te> Event { get;  init; } = @event;

    /// <summary>
    /// Gets the object document being processed in this execution context.
    /// </summary>
    public IObjectDocument Document { get; init; } = objectDocument;

    IEvent IExecutionContextWithData<Td>.Event => Event;

    /// <summary>
    /// Gets the data item associated with this execution context, or inherited from the parent context if not set locally.
    /// </summary>
    public Td? Item => item ?? (ParentContext as IExecutionContextWithData<Td>)?.Item;

    /// <summary>
    /// Gets the parent execution context, or null if this is the root context.
    /// </summary>
    public IExecutionContext? ParentContext { get; } = parentContext;

    /// <summary>
    /// Returns a string representation of this execution context including document details, event information, and parent context chain.
    /// </summary>
    /// <returns>A formatted string describing this execution context.</returns>
    public override string ToString()
    {
        var parent = "None";
        if (ParentContext != null)
        {
            parent = ParentContext.ToString();
        }

        return $"objectName '{Document.ObjectName}', objectId '{Document.ObjectId}', eventType '{Event.EventType}', eventVersion '{Event.EventVersion}'{Environment.NewLine}  Parent: {parent}";
    }

    /// <summary>
    /// Gets a value indicating whether this context is the root context (has no parent).
    /// </summary>
    public bool IsRoot => ParentContext == null;
}
