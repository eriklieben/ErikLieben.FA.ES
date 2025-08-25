namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

public class BlobDocumentStoreContainerNotFoundException : Exception
{
    public BlobDocumentStoreContainerNotFoundException(string message, Exception innerException) : base(message, innerException)
    {

    }
}
