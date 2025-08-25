using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Documents;

public class DocumentTagDocumentFactory : IDocumentTagDocumentFactory
{
    private readonly IDictionary<string, IDocumentTagDocumentFactory> documentTagFactories;
    private readonly EventStreamDefaultTypeSettings settings;

    public DocumentTagDocumentFactory(
        IDictionary<string, IDocumentTagDocumentFactory> eventStreamFactories,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(eventStreamFactories);
        ArgumentNullException.ThrowIfNull(settings);

        documentTagFactories = eventStreamFactories;
        this.settings = settings;
    }

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

    public IDocumentTagStore CreateDocumentTagStore()
    {
        throw new NotImplementedException();
    }


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
}

