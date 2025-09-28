# ELFAES-CFG-0004 — UnableToFindDocumentFactoryException

Category: Configuration (CFG)

Summary
- A document factory implementation cannot be found or resolved.

When does this happen?
- The requested document factory type is not registered.
- The configuration points to a non-existent or invalid factory.

Common causes
- Missing DI registration for the document factory.
- Incorrect configuration values or type names.

Recommended actions
- Register the required factory implementation in DI.
- Verify configuration keys and type mappings.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.UnableToFindDocumentFactoryException
- Error code: ELFAES-CFG-0004
- Source: src/ErikLieben.FA.ES/Exceptions/UnableToFindDocumentFactoryException.cs
