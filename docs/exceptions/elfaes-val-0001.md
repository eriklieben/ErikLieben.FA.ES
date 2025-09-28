# ELFAES-VAL-0001 — UnableToDeserializeInTransitEventException

Category: Validation (VAL)

Summary
- An in-transit event cannot be deserialized due to invalid or null payload or mismatched TypeInfo.

When does this happen?
- Attempting to deserialize an event from a null or invalid payload.

Common causes
- The serialized event value is null or empty.
- Mismatched or missing JsonTypeInfo for the event type.

Recommended actions
- Validate inputs before deserialization.
- Ensure JsonTypeInfo is provided and matches the expected event type.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.UnableToDeserializeInTransitEventException
- Error code: ELFAES-VAL-0001
- Source: src/ErikLieben.FA.ES/Exceptions/UnableToDeserializeInTransitEventException.cs
