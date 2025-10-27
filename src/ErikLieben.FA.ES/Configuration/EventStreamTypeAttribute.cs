namespace ErikLieben.FA.ES.Configuration;

/// <summary>
/// Specifies the storage provider types to use for this aggregate.
/// This attribute is processed at compile-time by the code generator to create AOT-compatible factory code.
/// </summary>
/// <example>
/// <code>
/// [EventStreamType("blob")]  // Use blob for everything
/// public partial class MyAggregate : Aggregate { }
///
/// [EventStreamType(
///     streamType: "blob",
///     documentType: "cosmos",
///     documentTagType: "cosmos")]
/// public partial class MyAggregate : Aggregate { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventStreamTypeAttribute : Attribute
{
    /// <summary>
    /// Gets the storage provider type for the event stream.
    /// </summary>
    public string? StreamType { get; }

    /// <summary>
    /// Gets the storage provider type for document storage.
    /// </summary>
    public string? DocumentType { get; }

    /// <summary>
    /// Gets the storage provider type for document tags.
    /// </summary>
    public string? DocumentTagType { get; }

    /// <summary>
    /// Gets the storage provider type for event stream tags.
    /// </summary>
    public string? EventStreamTagType { get; }

    /// <summary>
    /// Gets the storage provider type for document references.
    /// </summary>
    public string? DocumentRefType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamTypeAttribute"/> class.
    /// Use same provider for all components.
    /// </summary>
    /// <param name="all">The provider type to use for all components (e.g., "blob", "cosmos").</param>
    public EventStreamTypeAttribute(string all)
    {
        StreamType = all;
        DocumentType = all;
        DocumentTagType = all;
        EventStreamTagType = all;
        DocumentRefType = all;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamTypeAttribute"/> class.
    /// Use different providers per component.
    /// </summary>
    /// <param name="streamType">The provider type for event streams.</param>
    /// <param name="documentType">The provider type for documents.</param>
    /// <param name="documentTagType">The provider type for document tags.</param>
    /// <param name="eventStreamTagType">The provider type for event stream tags.</param>
    /// <param name="documentRefType">The provider type for document references.</param>
    public EventStreamTypeAttribute(
        string? streamType = null,
        string? documentType = null,
        string? documentTagType = null,
        string? eventStreamTagType = null,
        string? documentRefType = null)
    {
        StreamType = streamType;
        DocumentType = documentType;
        DocumentTagType = documentTagType;
        EventStreamTagType = eventStreamTagType;
        DocumentRefType = documentRefType;
    }
}
