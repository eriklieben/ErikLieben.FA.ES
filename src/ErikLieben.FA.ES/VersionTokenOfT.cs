using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a <see cref="VersionToken"/> whose object identifier is exposed as a strongly typed value.
/// </summary>
/// <typeparam name="T">The CLR type of the object identifier (for example, a Guid).</typeparam>
public abstract record VersionToken<T> : VersionToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionToken{T}"/> class with <see cref="VersionToken.Version"/> set to 0.
    /// </summary>
    protected VersionToken() { }

    /// <summary>
    /// Initializes a new instance by parsing the full token string.
    /// </summary>
    /// <param name="versionTokenString">The full token string to parse.</param>
    protected VersionToken(string versionTokenString) : base(versionTokenString) { }

    /// <summary>
    /// Initializes a new instance from the given event and its document.
    /// </summary>
    /// <param name="event">The event containing the version and type.</param>
    /// <param name="document">The document containing the object and stream identifiers.</param>
    protected VersionToken(IEvent @event, IObjectDocument document) : base(@event, document) { }

    /// <summary>
    /// Gets or sets the strongly typed object identifier for this token.
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="VersionToken.ObjectId"/> string is converted to and from <typeparamref name="T"/>
    /// using the abstract conversion methods implemented by derived types.
    /// </remarks>
    public new T ObjectId
    {
        get => ToObjectOfT(base.ObjectId);
        set => base.ObjectId = FromObjectOfT(value);
    }

    /// <summary>
    /// Converts the string object identifier to the strongly typed <typeparamref name="T"/>.
    /// </summary>
    /// <param name="objectId">The string representation of the object identifier.</param>
    /// <returns>The converted <typeparamref name="T"/> value.</returns>
    protected abstract T ToObjectOfT(string objectId);

    /// <summary>
    /// Converts a strongly typed object identifier to its string representation.
    /// </summary>
    /// <param name="objectId">The strongly typed object identifier.</param>
    /// <returns>The string representation of <paramref name="objectId"/>.</returns>
    protected abstract string FromObjectOfT(T objectId);
}
