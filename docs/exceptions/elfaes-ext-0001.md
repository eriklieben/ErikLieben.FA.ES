# ELFAES-EXT-0001 — BlobDataStoreProcessingException

Category: External (EXT)

Summary
- An error occurred while processing data in the blob data store.

When does this happen?
- External storage errors during read/write operations.

Common causes
- Transient network failures.
- Storage service limitations or throttling.

Recommended actions
- Implement retries with backoff.
- Verify network connectivity and storage account status.

Code reference
- Exception class: ErikLieben.FA.ES.AzureStorage.Exceptions.BlobDataStoreProcessingException
- Error code: ELFAES-EXT-0001
- Source: src/ErikLieben.FA.ES.AzureStorage/Exceptions/BlobDataStoreProcessingException.cs
