namespace ErikLieben.FA.ES.Configuration;

/// <summary>
/// Specifies which named Azure Blob Storage connections to use for this aggregate.
/// This attribute is processed at compile-time by the code generator to create AOT-compatible factory code.
/// </summary>
/// <example>
/// <code>
/// [EventStreamType("blob")]
/// [EventStreamBlobSettings("Store2")]  // Use Store2 for everything
/// public partial class MyAggregate : Aggregate { }
///
/// [EventStreamBlobSettings(
///     dataStore: "Store2",
///     documentStore: "Store1")]  // Different stores per component
/// public partial class MyAggregate : Aggregate { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventStreamBlobSettingsAttribute : Attribute
{
    /// <summary>
    /// Gets the named connection for event stream data storage.
    /// </summary>
    public string? DataStore { get; }

    /// <summary>
    /// Gets the named connection for document storage.
    /// </summary>
    public string? DocumentStore { get; }

    /// <summary>
    /// Gets the named connection for document tag storage.
    /// </summary>
    public string? DocumentTagStore { get; }

    /// <summary>
    /// Gets the named connection for stream tag storage.
    /// </summary>
    public string? StreamTagStore { get; }

    /// <summary>
    /// Gets the named connection for snapshot storage.
    /// </summary>
    public string? SnapShotStore { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamBlobSettingsAttribute"/> class.
    /// Use same named connection for all components.
    /// </summary>
    /// <param name="all">The named connection to use for all components.</param>
    public EventStreamBlobSettingsAttribute(string all)
    {
        DataStore = all;
        DocumentStore = all;
        DocumentTagStore = all;
        StreamTagStore = all;
        SnapShotStore = all;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamBlobSettingsAttribute"/> class.
    /// Use different named connections per component.
    /// </summary>
    /// <param name="dataStore">The named connection for event stream data.</param>
    /// <param name="documentStore">The named connection for documents.</param>
    /// <param name="documentTagStore">The named connection for document tags.</param>
    /// <param name="streamTagStore">The named connection for stream tags.</param>
    /// <param name="snapShotStore">The named connection for snapshots.</param>
    public EventStreamBlobSettingsAttribute(
        string? dataStore = null,
        string? documentStore = null,
        string? documentTagStore = null,
        string? streamTagStore = null,
        string? snapShotStore = null)
    {
        DataStore = dataStore;
        DocumentStore = documentStore;
        DocumentTagStore = documentTagStore;
        StreamTagStore = streamTagStore;
        SnapShotStore = snapShotStore;
    }
}
