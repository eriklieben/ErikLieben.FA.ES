# ELFAES-CFG-0005 — UnableToFindDocumentTagFactoryException

Category: Configuration (CFG)

Summary
- A document tag factory implementation cannot be found or resolved.

When does this happen?
- The requested document tag factory type is not registered.
- The configuration points to a non-existent or invalid tag factory.

Common causes
- Missing DI registration for the document tag factory.
- Incorrect configuration values or type names.

Recommended actions
- Register the required tag factory implementation in DI.
- Verify configuration keys and type mappings.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.UnableToFindDocumentTagFactoryException
- Error code: ELFAES-CFG-0005
- Source: src/ErikLieben.FA.ES/Exceptions/UnableToFindDocumentTagFactoryException.cs
