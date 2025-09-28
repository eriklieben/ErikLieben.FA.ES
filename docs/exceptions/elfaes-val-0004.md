# ELFAES-VAL-0004 — VersionTokenStreamMismatchException

Category: Validation (VAL)

Summary
- Two version tokens being compared refer to different streams/object identifiers.

When does this happen?
- Comparing version tokens that belong to different object identifiers.

Common causes
- Mixing tokens from different streams inadvertently.
- Incorrectly constructed comparison inputs.

Recommended actions
- Ensure both version tokens refer to the same stream/object identifier before comparing.
- Validate inputs prior to calling comparison methods.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.VersionTokenStreamMismatchException
- Error code: ELFAES-VAL-0004
- Source: src/ErikLieben.FA.ES/Exceptions/VersionTokenStreamMismatchException.cs
