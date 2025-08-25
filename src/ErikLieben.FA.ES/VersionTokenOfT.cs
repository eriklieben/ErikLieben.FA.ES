using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

public abstract record VersionToken<T> : VersionToken
{
    protected VersionToken() { }

    protected VersionToken(string versionTokenString) : base(versionTokenString) { }

    protected VersionToken(IEvent @event, IObjectDocument document) : base(@event, document) { }

    public new T ObjectId
    {
        get => ToObjectOfT(base.ObjectId);
        set => base.ObjectId = FromObjectOfT(value);
    }

    protected abstract T ToObjectOfT(string objectId);
    protected abstract string FromObjectOfT(T objectId);
}
