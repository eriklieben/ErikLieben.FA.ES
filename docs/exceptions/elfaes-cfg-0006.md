# ELFAES-CFG-0006 — DocumentConfigurationException

Category: Configuration (CFG)

Summary
- Document-related configuration is invalid or missing.

When does this happen?
- Required configuration values for Azure Storage documents are missing or invalid.

Common causes
- Misconfigured connection strings, container names, or paths.
- Typographical errors in configuration keys.

Recommended actions
- Validate configuration values during startup.
- Use the provided helper guards to validate inputs.

Code reference
- Exception class: ErikLieben.FA.ES.AzureStorage.Exceptions.DocumentConfigurationException
- Error code: ELFAES-CFG-0006
- Source: src/ErikLieben.FA.ES.AzureStorage/Exceptions/DocumentConfigurationException.cs
