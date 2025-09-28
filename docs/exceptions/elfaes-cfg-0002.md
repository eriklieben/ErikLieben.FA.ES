# ELFAES-CFG-0002 — SnapshotJsonTypeInfoNotSetException

Category: Configuration (CFG)

Summary
- The JSON TypeInfo for the snapshot type has not been configured.

When does this happen?
- Attempting to deserialize a snapshot without providing the required JsonTypeInfo.

Common causes
- Missing configuration of JsonSerializerContext for snapshot types.
- Incorrect DI setup that omits JsonTypeInfo registration.

Recommended actions
- Ensure the snapshot JsonTypeInfo is registered in the JsonSerializerContext.
- Verify your configuration/DI registration for serialization setup.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.SnapshotJsonTypeInfoNotSetException
- Error code: ELFAES-CFG-0002
- Source: src/ErikLieben.FA.ES/Exceptions/SnapshotJsonTypeInfoNotSetException.cs
