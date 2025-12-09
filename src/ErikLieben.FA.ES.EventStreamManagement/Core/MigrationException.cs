namespace ErikLieben.FA.ES.EventStreamManagement.Core;

/// <summary>
/// Exception thrown when migration operations fail.
/// </summary>
public class MigrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationException"/> class.
    /// </summary>
    public MigrationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationException"/> class.
    /// </summary>
    public MigrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
