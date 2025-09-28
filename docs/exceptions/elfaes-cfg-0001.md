# ELFAES-CFG-0001 — AggregateJsonTypeInfoNotSetException

Category: Configuration (CFG)

Summary
- The JSON TypeInfo for the aggregate type has not been configured.

When does this happen?
- Attempting to deserialize an aggregate without providing the required JsonTypeInfo.

Common causes
- Missing configuration of JsonSerializerContext for aggregate types.
- Incorrect DI setup that omits JsonTypeInfo registration.

Recommended actions
- Ensure the aggregate JsonTypeInfo is registered in the JsonSerializerContext.
- Verify your configuration/DI registration for serialization setup.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.AggregateJsonTypeInfoNotSetException
- Error code: ELFAES-CFG-0001
- Source: src/ErikLieben.FA.ES/Exceptions/AggregateJsonTypeInfoNotSetException.cs
