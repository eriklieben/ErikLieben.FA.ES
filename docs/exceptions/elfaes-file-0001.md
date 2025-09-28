# ELFAES-FILE-0001 — BlobDocumentNotFoundException

Category: File (FILE)

Summary
- The requested blob document could not be found.

When does this happen?
- A blob corresponding to the requested document identifier does not exist.

Common causes
- Incorrect blob name or path.
- The document was deleted or never uploaded.

Recommended actions
- Verify the identifier and container configuration.
- Ensure the blob exists and the application has access.

Code reference
- Exception class: ErikLieben.FA.ES.AzureStorage.Exceptions.BlobDocumentNotFoundException
- Error code: ELFAES-FILE-0001
- Source: src/ErikLieben.FA.ES.AzureStorage/Exceptions/BlobDocumentNotFoundException.cs
