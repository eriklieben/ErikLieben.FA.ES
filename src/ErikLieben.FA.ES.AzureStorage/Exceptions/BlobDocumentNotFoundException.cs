namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

public class BlobDocumentNotFoundException : Exception
{
    public BlobDocumentNotFoundException(string message, Exception innerException) : base(message, innerException)
    {

    }
}