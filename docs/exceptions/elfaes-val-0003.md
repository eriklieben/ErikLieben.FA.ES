# ELFAES-VAL-0003 — InvalidContentTypeException

Category: Validation (VAL)

Summary
- An invalid content-type is provided to an Azure Functions Worker binding.

When does this happen?
- The provided request/content type does not match the supported type(s).

Common causes
- Client sends an unexpected Content-Type header.
- Binding or attribute expects a different content type.

Recommended actions
- Ensure the request Content-Type matches the expected value(s).
- Update binding configuration or client request accordingly.

Code reference
- Exception class: ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions.InvalidContentTypeException
- Error code: ELFAES-VAL-0003
- Source: src/ErikLieben.FA.ES.Azure.Functions.Worker.Extensions/Exceptions/InvalidContentTypeException.cs
