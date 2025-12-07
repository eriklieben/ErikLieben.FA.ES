namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

/// <summary>
/// Exception thrown when a projection cannot be found in storage
/// and <see cref="ProjectionAttribute.CreateIfNotExists"/> is <c>false</c>.
/// </summary>
[Serializable]
public sealed class ProjectionNotFoundException : Exception
{
    /// <summary>
    /// Gets the type of the projection that was not found.
    /// </summary>
    public Type? ProjectionType { get; }

    /// <summary>
    /// Gets the blob name that was searched for.
    /// </summary>
    public string? BlobName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionNotFoundException"/> class.
    /// </summary>
    public ProjectionNotFoundException()
        : base("The requested projection was not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionNotFoundException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ProjectionNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionNotFoundException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ProjectionNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionNotFoundException"/> class
    /// with projection details.
    /// </summary>
    /// <param name="projectionType">The type of the projection that was not found.</param>
    /// <param name="blobName">The blob name that was searched for.</param>
    public ProjectionNotFoundException(Type projectionType, string? blobName)
        : base($"Projection of type '{projectionType.Name}'" +
               (blobName != null ? $" with blob name '{blobName}'" : " with default blob name") +
               " was not found.")
    {
        ProjectionType = projectionType;
        BlobName = blobName;
    }
}
