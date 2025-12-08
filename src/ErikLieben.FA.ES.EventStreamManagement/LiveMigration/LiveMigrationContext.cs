namespace ErikLieben.FA.ES.EventStreamManagement.LiveMigration;

using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.EventStreamManagement.Transformation;

/// <summary>
/// Context containing all configuration for a live migration operation.
/// </summary>
public sealed class LiveMigrationContext
{
    /// <summary>
    /// Gets or sets the unique identifier for this migration.
    /// </summary>
    public Guid MigrationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the source object document.
    /// </summary>
    public required IObjectDocument SourceDocument { get; set; }

    /// <summary>
    /// Gets or sets the source stream identifier.
    /// </summary>
    public required string SourceStreamId { get; set; }

    /// <summary>
    /// Gets or sets the target stream identifier.
    /// </summary>
    public required string TargetStreamId { get; set; }

    /// <summary>
    /// Gets or sets the target object document (for writing events).
    /// </summary>
    public required IObjectDocument TargetDocument { get; set; }

    /// <summary>
    /// Gets or sets the data store for reading and writing events.
    /// </summary>
    public required IDataStore DataStore { get; set; }

    /// <summary>
    /// Gets or sets the document store for updating object documents.
    /// </summary>
    public required IDocumentStore DocumentStore { get; set; }

    /// <summary>
    /// Gets or sets the live migration options.
    /// </summary>
    public required LiveMigrationOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the optional event transformer for transforming events during migration.
    /// </summary>
    public IEventTransformer? Transformer { get; set; }
}
