using System.Diagnostics;

namespace ErikLieben.FA.ES.Processors;

public abstract class Aggregate : IBase
{
     private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");
    
    protected IEventStream Stream { get; private set; }

    protected Aggregate(IEventStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Stream = stream;
#pragma warning disable S1699 // Content of method is generated using roslyn
        GeneratedSetup();
#pragma warning restore S1699
    }

    //public abstract void Fold(IEvent @event);

    public virtual void Fold(IEvent @event) { }

    public async Task Fold()
    {
        
        using var activity = ActivitySource.StartActivity($"Aggregate.{nameof(Fold)}");
        
        if (Stream.Settings.ManualFolding)
        {
            return;
        }

        IReadOnlyCollection<IEvent> events = [];
        if (Stream.Document.Active.HasSnapShots())
        {
            var lastSnapshot = Stream.Document.Active.SnapShots.LastOrDefault();
            if (lastSnapshot != null)
            {
                var snapshot = await Stream.GetSnapShot(lastSnapshot.UntilVersion);
                if (snapshot != null)
                {
                    ProcessSnapshot(snapshot);
                }
                events = await Stream.ReadAsync(lastSnapshot.UntilVersion + 1);
            }
        }
        else
        {
            events = await Stream.ReadAsync();
        }

        var eventsToFold = events.ToList();       
        activity?.AddTag("EventsToFold", eventsToFold.Count.ToString());
        
        eventsToFold.ForEach(Fold);
    }

    //public async Task Fold(Type type)
    //{
    //    if (Stream.Settings.ManualFolding)
    //    {
    //        return;
    //    }

    //    IReadOnlyCollection<IEvent>? events = null;
    //    if (Stream.Document.Active.HasSnapShots())
    //    {
    //        var lastSnapshot = Stream.Document.Active.SnapShots.LastOrDefault();
    //        if (lastSnapshot != null)
    //        {
    //            var snapshot = await Stream.GetSnapShot(lastSnapshot.UntilVersion);
    //            if (snapshot != null)
    //            {
    //                ProcessSnapshot(snapshot);
    //            }
    //            events = await Stream.ReadAsync(lastSnapshot.UntilVersion + 1);
    //        }
    //    }
    //    else
    //    {
    //        events = await Stream.ReadAsync();
    //    }

    //    events?.ToList().ForEach(Fold);
    //}

    //protected abstract void GeneratedSetup();
    protected virtual void GeneratedSetup() { }

    //public abstract void ProcessSnapshot(object snapshot);
    public virtual void ProcessSnapshot(object snapshot) { }

}