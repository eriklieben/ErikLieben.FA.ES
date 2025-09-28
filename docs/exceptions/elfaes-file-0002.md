# ELFAES-FILE-0002 — BlobDocumentStoreContainerNotFoundException

Category: File (FILE)

Summary
- The configured blob container for document storage cannot be found.

When does this happen?
- The blob container name is incorrect or the container does not exist.

Common causes
- Misconfigured container name.
- The container was deleted or not created yet.

Recommended actions
- Verify the container name in configuration.
- Ensure the container exists and the identity has permissions.

Code reference
- Exception class: ErikLieben.FA.ES.AzureStorage.Exceptions.BlobDocumentStoreContainerNotFoundException
- Error code: ELFAES-FILE-0002
- Source: src/ErikLieben.FA.ES.AzureStorage/Exceptions/BlobDocumentStoreContainerNotFoundException.cs
