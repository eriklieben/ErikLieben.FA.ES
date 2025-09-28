using System;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when an error occurs while processing data in the blob data store.
/// Error Code: ELFAES-EXT-0001
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - External storage errors during read/write operations.
///
/// Common causes:
/// - Transient network failures.
/// - Storage service limitations or throttling.
///
/// Recommended actions:
/// - Implement retries with backoff.
/// - Verify network connectivity and storage account status.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-ext-0001.md
/// </remarks>
[Serializable]
public class BlobDataStoreProcessingException : EsException
{
    private const string Code = "ELFAES-EXT-0001";

    public BlobDataStoreProcessingException(string message) : base(Code, message)
    {
    }

    public BlobDataStoreProcessingException(string message, Exception innerException) : base(Code, message, innerException)
    {
    }

    protected BlobDataStoreProcessingException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
