# ELFAES-CFG-0003 — UnableToCreateEventStreamForStreamTypeException

Category: Configuration (CFG)

Summary
- The configured EventStream type cannot be created.

When does this happen?
- The configured EventStream type is not registered or cannot be resolved.
- The fallback EventStream type is also unavailable or invalid.

Common causes
- Missing DI registration or incorrect type mapping.
- Typos in configuration values for stream types.

Recommended actions
- Verify DI registrations and configuration keys for stream types.
- Ensure both the primary and fallback types are public and constructible.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.UnableToCreateEventStreamForStreamTypeException
- Error code: ELFAES-CFG-0003
- Source: src/ErikLieben.FA.ES/Exceptions/UnableToCreateEventStreamForStreamTypeException.cs
