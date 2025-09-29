using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Represents binding data for resolving and constructing an event stream aggregate in Azure Functions.
/// </summary>
public class EventStreamData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamData"/> class.
    /// </summary>
    /// <param name="objectId">The identifier of the domain object to load; may be null when deserialization fails.</param>
    /// <param name="objectType">The domain object type/name (e.g., "Order").</param>
    /// <param name="connection">The connection name to use for the event stream store.</param>
    /// <param name="documentType">The document type or store alias to use when resolving documents.</param>
    /// <param name="defaultStreamType">The default event stream type to use when not specified elsewhere.</param>
    /// <param name="defaultStreamConnection">The default connection name for the default stream type.</param>
    /// <param name="createEmtpyObjectWhenNonExisting">True to create an empty object when it does not exist; otherwise, false.</param>
    public EventStreamData(
        string objectId,
        string objectType,
        string connection,
        string documentType,
        string defaultStreamType,
        string defaultStreamConnection,
        bool createEmtpyObjectWhenNonExisting)
    {
        ObjectId = objectId;
        ObjectType = objectType;
        Connection = connection;
        DocumentType = documentType;
        DefaultStreamType = defaultStreamType;
        DefaultStreamConnection = defaultStreamConnection;
        CreateEmptyObjectWhenNonExistent = createEmtpyObjectWhenNonExisting;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamData"/> class for JSON deserialization.
    /// </summary>
    [JsonConstructor]
    internal EventStreamData() { }

    /// <summary>
    /// Gets or sets the identifier of the domain object to load.
    /// </summary>
    /// <remarks>May be null when the binding payload is malformed; callers should validate non-null before use.</remarks>
    public string? ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the domain object type/name (e.g., "Order").
    /// </summary>
    public string? ObjectType { get; set; }

    /// <summary>
    /// Gets or sets the connection name to use for the event stream store.
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Gets or sets the document type or store alias to use when resolving documents.
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Gets or sets the default event stream type to use when not specified elsewhere.
    /// </summary>
    public string? DefaultStreamType { get; set; }

    /// <summary>
    /// Gets or sets the default connection name for the default stream type.
    /// </summary>
    public string? DefaultStreamConnection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create an empty object when it does not exist.
    /// </summary>
    public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
}
