using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

/// <summary>
/// Specifies that a parameter binds to a projection loaded from storage in WebJobs isolated worker.
/// </summary>
[Binding]
[ConnectionProvider(typeof(StorageAccountAttribute))]
[AttributeUsage(AttributeTargets.Parameter)]
public class ProjectionAttribute : Attribute, IConnectionProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionAttribute"/> class.
    /// </summary>
    public ProjectionAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionAttribute"/> class with a specific blob name.
    /// </summary>
    /// <param name="blobName">The blob name to load the projection from.</param>
    public ProjectionAttribute(string blobName)
    {
        BlobName = blobName;
    }

    /// <summary>
    /// Gets or sets the optional blob name. If not provided, uses the default name based on the projection type.
    /// </summary>
    [AutoResolve]
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a new projection if it doesn't exist.
    /// Defaults to true.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the connection configuration used to access the storage backend.
    /// </summary>
    public string? Connection { get; set; }
}
