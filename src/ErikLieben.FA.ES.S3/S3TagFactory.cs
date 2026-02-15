using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Creates S3-backed document and stream tag stores.
/// </summary>
public class S3TagFactory : IDocumentTagDocumentFactory
{
    private readonly EventStreamDefaultTypeSettings settings;
    private readonly EventStreamS3Settings s3Settings;
    private readonly IS3ClientFactory clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3TagFactory"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="Amazon.S3.IAmazonS3"/> instances.</param>
    /// <param name="settings">The default type settings used to resolve tag store types.</param>
    /// <param name="s3Settings">The S3 storage settings controlling default stores and auto-creation.</param>
    public S3TagFactory(
        IS3ClientFactory clientFactory,
        EventStreamDefaultTypeSettings settings,
        EventStreamS3Settings s3Settings)
    {
        this.settings = settings;
        this.s3Settings = s3Settings;
        this.clientFactory = clientFactory;
    }

    /// <summary>
    /// Creates a document tag store for the specified object document using its configured tag type.
    /// </summary>
    /// <param name="document">The document whose tag configuration is used.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by S3.</returns>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);
        return new S3DocumentTagStore(clientFactory, settings.DocumentTagType, s3Settings.DefaultDocumentTagStore, s3Settings);
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by S3.</returns>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return new S3DocumentTagStore(clientFactory, settings.DocumentTagType, s3Settings.DefaultDocumentTagStore, s3Settings);
    }

    /// <summary>
    /// Creates a document tag store for the specified tag provider type.
    /// </summary>
    /// <param name="type">The tag provider type (e.g., "s3").</param>
    /// <returns>An <see cref="IDocumentTagStore"/> backed by the specified provider.</returns>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        return new S3DocumentTagStore(clientFactory, type, s3Settings.DefaultDocumentTagStore, s3Settings);
    }

    /// <summary>
    /// Creates a stream tag store for the specified document using the configured stream tag provider type.
    /// </summary>
    /// <param name="document">The document whose stream tag store is requested.</param>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.EventStreamTagType);
        return new S3StreamTagStore(clientFactory, s3Settings.DefaultDocumentTagStore, s3Settings);
    }

    /// <summary>
    /// Creates a stream tag store using the default stream tag type from settings.
    /// </summary>
    /// <returns>An <see cref="IDocumentTagStore"/> for stream tags.</returns>
    public IDocumentTagStore CreateStreamTagStore()
    {
        return new S3StreamTagStore(clientFactory, s3Settings.DefaultDocumentTagStore, s3Settings);
    }
}
