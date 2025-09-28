using System;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when the configured blob container for document storage cannot be found.
/// Error Code: ELFAES-FILE-0002
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The blob container name is incorrect or the container does not exist.
///
/// Common causes:
/// - Misconfigured container name.
/// - The container was deleted or not created yet.
///
/// Recommended actions:
/// - Verify the container name in configuration.
/// - Ensure the container exists and the identity has permissions.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-file-0002.md
/// </remarks>
[Serializable]
public class BlobDocumentStoreContainerNotFoundException : EsException
{
    private const string Code = "ELFAES-FILE-0002";

    public BlobDocumentStoreContainerNotFoundException(string message)
        : base(Code, message)
    {
    }

    public BlobDocumentStoreContainerNotFoundException(string message, Exception innerException)
        : base(Code, message, innerException)
    {
    }

    protected BlobDocumentStoreContainerNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
