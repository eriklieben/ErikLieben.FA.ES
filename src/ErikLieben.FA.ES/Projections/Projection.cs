using ErikLieben.FA.ES.Documents;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

public abstract class Projection : IProjectionBase
{
    protected readonly IObjectDocumentFactory? DocumentFactory;
    protected readonly IEventStreamFactory? EventStreamFactory;

    protected Projection()
    {
    }

    protected Projection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        DocumentFactory = documentFactory;
        EventStreamFactory = eventStreamFactory;
    }


    protected Projection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory,
        Checkpoint checkpoint,
        string? checkpointFingerprint) : this(documentFactory, eventStreamFactory)
    {
        Checkpoint = checkpoint;
        CheckpointFingerprint = checkpointFingerprint;
    }

    public abstract Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null,
        IExecutionContext? context = null)
        where T : class;

    public Task Fold(IEvent @event, IObjectDocument document)
    {
        return Fold<object>(@event, document, null!, null!);
    }

    protected Task Fold(IEvent @event, IObjectDocument document, IExecutionContext? context)
    {
        return Fold<object>(@event, document, null!, context);
    }

    // public abstract void LoadFromJson(string json);

    public abstract string ToJson();

    private readonly VersionTokenComparer comparer = new VersionTokenComparer();

    protected bool IsNewer(VersionToken token)
    {
        if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
        {
            return comparer.IsNewer(
                token.Value,
                $"{token.ObjectIdentifier}__{value}");
        }

        return true;
    }

    protected abstract Task PostWhenAll(IObjectDocument document);

    protected abstract Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; }

    protected T? GetWhenParameterValue<T, Te>(string forType, IObjectDocument document, IEvent @event)
        where Te : class where T : class
    {
        WhenParameterValueFactories.TryGetValue(forType, out var factory);
        switch (factory)
        {
            case null:
                return null;
            case IProjectionWhenParameterValueFactory<T, Te> factoryWithEvent:
            {
                var eventW = @event as IEvent<Te>;
                return factoryWithEvent?.Create(document, eventW!);
            }
            case IProjectionWhenParameterValueFactory<T> factoryWithoutEvent:
                return factoryWithoutEvent.Create(document, @event);
            default:
                return null;
        }
    }

    public async Task UpdateToVersion(VersionToken token, IExecutionContext? context = null)
    {

        if (DocumentFactory == null || EventStreamFactory == null)
        {
            throw new Exception("documentFactory or eventStreamFactory is null");
        }

        if (IsNewer(token) || token.TryUpdateToLatestVersion)
        {
            var startIdx = -1;
            if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
            {
                startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
            }

            var document = await DocumentFactory.GetAsync(token.ObjectName, token.ObjectId);
            var eventStream = EventStreamFactory.Create(document);
            var events = token.TryUpdateToLatestVersion ?
                await eventStream.ReadAsync(startIdx) :
                await eventStream.ReadAsync(startIdx, token.Version);

            foreach (var @event in events)
            {
                await Fold(@event, document, context);
                UpdateVersionIndex(@event, document);
            }
            await PostWhenAll(document);
        }
    }

    public async Task UpdateToVersion<T>(VersionToken token, IExecutionContextWithData<T>? context = null, T? data = null)
        where T: class
    {
        if (DocumentFactory == null || EventStreamFactory == null)
        {
            throw new Exception("documentFactory or eventStreamFactory is null");
        }

        if (IsNewer(token) || token.TryUpdateToLatestVersion)
        {
            var startIdx = -1;
            if (Checkpoint.TryGetValue(token.ObjectIdentifier, out var value))
            {
                startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
            }

            var document = await DocumentFactory.GetAsync(token.ObjectName, token.ObjectId);
            var eventStream = EventStreamFactory.Create(document);
            var events = token.TryUpdateToLatestVersion ?
                await eventStream.ReadAsync(startIdx) :
                await eventStream.ReadAsync(startIdx, token.Version);

            foreach (var @event in events)
            {
                if (context != null && @event == context.Event)
                {
                    throw new Exception("parent event is same as current event, are you running into a loop?");
                }

                await Fold(@event, document, data, context);
                UpdateVersionIndex(@event, document);
            }

            if (events.Count > 0)
            {
                await PostWhenAll(document);
            }
        }
    }

    // public async Task UpdateToVersion(VersionToken token)
    // {
    //     if (documentFactory == null || eventStreamFactory == null)
    //     {
    //         throw new Exception("documentFactory or eventStreamFactory is null");
    //     }
    //
    //     if (IsNewer(token) || token.TryUpdateToLatestVersion)
    //     {
    //         var startIdx = -1;
    //         if (VersionIndex != null && VersionIndex.TryGetValue(token.ObjectIdentifier, out var value))
    //         {
    //             startIdx = new VersionToken(token.ObjectIdentifier, value).Version + 1;
    //         }
    //
    //         var document = await documentFactory.GetAsync(token.ObjectName, token.ObjectId);
    //         var eventStream = eventStreamFactory.Create(document);
    //         var events = token.TryUpdateToLatestVersion ?
    //             await eventStream.ReadAsync(startIdx) :
    //             await eventStream.ReadAsync(startIdx, token.Version);
    //
    //         foreach (var @event in events)
    //         {
    //             var executionContext = new ExecutionContext(@event, null!); // TODO: context.,..
    //             await Fold(@event, document, executionContext);
    //             UpdateVersionIndex(@event, document);
    //         }
    //     }
    // }

    public async Task UpdateToLatestVersion(IExecutionContext? context = null)
    {
        foreach (var versionToken in Checkpoint)
        {
            await UpdateToVersion(new VersionToken(versionToken.Key, versionToken.Value).ToLatestVersion(), null, context);
        }
    }

    private void UpdateVersionIndex(IEvent @event, IObjectDocument document)
    {
        var idString = new VersionToken(@event, document);
        if (Checkpoint!.ContainsKey(idString.ObjectIdentifier))
        {
            Checkpoint[idString.ObjectIdentifier] = idString.VersionIdentifier;
        }
        else
        {
            Checkpoint.Add(idString.ObjectIdentifier, idString.VersionIdentifier);
        }

        CheckpointFingerprint = GenerateCheckpointFingerprint();
    }

    private string GenerateCheckpointFingerprint()
    {
        StringBuilder sb = new();
        Checkpoint!.OrderBy(i => i.Key).ToList().ForEach(i => sb.AppendLine($"{i.Key}|{i.Value}"));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        StringBuilder builder = new();
        foreach (var t in bytes)
        {
            builder.Append(t.ToString("x2"));
        }
        var checkpointFingerprint = builder.ToString();
        return checkpointFingerprint;
    }

    [JsonPropertyName("$checkpoint")]
    public abstract Checkpoint Checkpoint { get; set; }

    [JsonPropertyName("$checkpointFingerprint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CheckpointFingerprint { get; set; }
}
