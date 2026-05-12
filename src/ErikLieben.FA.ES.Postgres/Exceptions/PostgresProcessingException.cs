namespace ErikLieben.FA.ES.Postgres.Exceptions;

/// <summary>
/// Thrown when a Postgres operation fails for non-concurrency reasons that the provider chooses to surface.
/// </summary>
public class PostgresProcessingException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public PostgresProcessingException(string message) : base(message) { }

    /// <summary>Initializes a new instance with an inner exception.</summary>
    public PostgresProcessingException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when the schema bootstrap fails or the schema is missing.
/// </summary>
public class PostgresSchemaException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public PostgresSchemaException(string message) : base(message) { }

    /// <summary>Initializes a new instance with an inner exception.</summary>
    public PostgresSchemaException(string message, Exception inner) : base(message, inner) { }
}
