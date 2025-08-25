using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

public interface IExecutionContext
{
    bool IsRoot { get; }

    IObjectDocument Document { get; }

    IExecutionContext? ParentContext { get; }
}

public interface IExecutionContextWithData<out Td> : IExecutionContext where Td : class
{
    Td? Item { get; }

    IEvent Event { get; }
}

public interface IExecutionContextWithEvent<out Te> : IExecutionContext where Te : class
{
    IEvent<Te> Event { get; }
}

public interface IExecutionContext<out Te, out Td> :
    IExecutionContextWithData<Td>, IExecutionContextWithEvent<Te> where Td : class where Te : class
{
    // We hide the event from IExecutionContextWithData because the other is more enriched
    new IEvent<Te> Event { get; }
}

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

    public IEvent<Te> Event { get;  init; } = @event;
    public IObjectDocument Document { get; init; } = objectDocument;

    IEvent IExecutionContextWithData<Td>.Event => Event;

    public Td? Item => item ?? (ParentContext as IExecutionContextWithData<Td>)?.Item;

    public IExecutionContext? ParentContext { get; } = parentContext;

    public override string ToString()
    {
        var parent = "None";
        if (ParentContext != null)
        {
            parent = ParentContext.ToString();
        }

        return $"objectName '{Document.ObjectName}', objectId '{Document.ObjectId}', eventType '{Event.EventType}', eventVersion '{Event.EventVersion}'{Environment.NewLine}  Parent: {parent}";
    }

    public bool IsRoot => ParentContext == null;
}
