#pragma warning disable S1192 // String literals should not be duplicated - error message suffix is intentionally repeated for clarity

using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Factory for creating document and stream tag store instances based on tag types.
/// </summary>
public class DocumentTagDocumentFactory : IDocumentTagDocumentFactory
{
    private readonly IDictionary<string, IDocumentTagDocumentFactory> documentTagFactories;
    private readonly EventStreamDefaultTypeSettings settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentTagDocumentFactory"/> class.
    /// </summary>
    /// <param name="eventStreamFactories">Dictionary of tag type-specific factories.</param>
    /// <param name="settings">Default type settings for fallback behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public DocumentTagDocumentFactory(
        IDictionary<string, IDocumentTagDocumentFactory> eventStreamFactories,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(eventStreamFactories);
        ArgumentNullException.ThrowIfNull(settings);

        documentTagFactories = eventStreamFactories;
        this.settings = settings;
    }

    /// <summary>
    /// Creates a document tag store using the default document tag type from settings.
    /// </summary>
    /// <returns>The created document tag store instance.</returns>
    /// <exception cref="UnableToFindDocumentTagFactoryException">Thrown when no factory is found for the default tag type.</exception>
    public IDocumentTagStore CreateDocumentTagStore()
    {
        return CreateDocumentTagStore(settings.DocumentTagType);
    }

    /// <summary>
    /// Creates a document tag store for the specified document.
    /// </summary>
    /// <param name="document">The object document for which to create the tag store.</param>
    /// <returns>The created document tag store instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the document or its tag type is null.</exception>
    /// <exception cref="UnableToFindDocumentTagFactoryException">Thrown when no factory is found for the tag type.</exception>
    public IDocumentTagStore CreateDocumentTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.DocumentTagType);

        // First try to find the DocumentTagStoreFactory based on the setting in the document
        if (documentTagFactories.TryGetValue(document.Active.DocumentTagType, out IDocumentTagDocumentFactory? streamFactory))
        {
            return streamFactory.CreateDocumentTagStore(document);
        }
        // If not found, try finding the DocumentTagStoreFactory based on the setting in the event stream settings
        else if (documentTagFactories.TryGetValue(settings.DocumentTagType, out IDocumentTagDocumentFactory? defaultStreamFactory))
        {
            return defaultStreamFactory.CreateDocumentTagStore(document);
        }

        throw new UnableToFindDocumentTagFactoryException(
            $"Unable to find store for DocumentTagType: {document.Active.DocumentTagType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Creates a document tag store for the specified tag type.
    /// </summary>
    /// <param name="type">The tag type for which to create the store.</param>
    /// <returns>The created document tag store instance.</returns>
    /// <exception cref="UnableToFindDocumentTagFactoryException">Thrown when no factory is found for the tag type.</exception>
    public IDocumentTagStore CreateDocumentTagStore(string type)
    {
        // First try to find the DocumentTagStoreFactory based on the setting in the document
        if (documentTagFactories.TryGetValue(type, out IDocumentTagDocumentFactory? factory))
        {
            return factory.CreateDocumentTagStore(type);
        }
        // If not found, try finding the DocumentTagStoreFactory based on the setting in the event stream settings
        else if (documentTagFactories.TryGetValue(settings.DocumentTagType, out IDocumentTagDocumentFactory? defaultFactory))
        {
            return defaultFactory.CreateDocumentTagStore(type);
        }

        throw new UnableToFindDocumentTagFactoryException(
            $"Unable to find store for DocumentTagType: {type}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Creates a stream tag store using the default event stream tag type from settings.
    /// </summary>
    /// <returns>The created stream tag store instance.</returns>
    /// <exception cref="UnableToFindDocumentTagFactoryException">Thrown when no factory is found for the default stream tag type.</exception>
    public IDocumentTagStore CreateStreamTagStore()
    {
        if (documentTagFactories.TryGetValue(settings.EventStreamTagType, out IDocumentTagDocumentFactory? factory))
        {
            return factory.CreateStreamTagStore();
        }

        throw new UnableToFindDocumentTagFactoryException(
            $"Unable to find store for EventStreamTagType: {settings.EventStreamTagType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Creates a stream tag store for the specified document.
    /// </summary>
    /// <param name="document">The object document for which to create the stream tag store.</param>
    /// <returns>The created stream tag store instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the document or its stream tag type is null.</exception>
    /// <exception cref="UnableToFindDocumentTagFactoryException">Thrown when no factory is found for the stream tag type.</exception>
    public IDocumentTagStore CreateStreamTagStore(IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.EventStreamTagType);

        // First try to find the StreamTagStoreFactory based on the setting in the document
        if (documentTagFactories.TryGetValue(document.Active.EventStreamTagType, out IDocumentTagDocumentFactory? factory))
        {
            return factory.CreateStreamTagStore(document);
        }
        // If not found, try finding the StreamTagStoreFactory based on the setting in the event stream settings
        else if (documentTagFactories.TryGetValue(settings.EventStreamTagType, out IDocumentTagDocumentFactory? defaultFactory))
        {
            return defaultFactory.CreateStreamTagStore(document);
        }

        throw new UnableToFindDocumentTagFactoryException(
            $"Unable to find store for EventStreamTagType: {document.Active.EventStreamTagType}." +
            " Are you sure it's properly registered in the configuration?");
    }
}

