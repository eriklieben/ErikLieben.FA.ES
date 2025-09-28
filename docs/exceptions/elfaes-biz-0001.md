# ELFAES-BIZ-0001 — ConstraintException

Category: Business (BIZ)

Summary
- A business constraint is violated.

When does this happen?
- A new event violates an existing constraint on a stream.
- A new constraint conflicts with the current stream state.

Common causes
- Incorrect precondition checks before appending events.
- Concurrency or state validation issues.

Recommended actions
- Validate constraints before committing changes.
- Inspect the Constraint for details and resolve conflicts.

Code reference
- Exception class: ErikLieben.FA.ES.Exceptions.ConstraintException
- Error code: ELFAES-BIZ-0001
- Source: src/ErikLieben.FA.ES/Exceptions/ConstraintException.cs
