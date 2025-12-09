namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

/// <summary>
/// Data transfer object for projection binding data passed between host and worker.
/// </summary>
public class ProjectionAttributeData
{
    /// <summary>
    /// Gets or sets the optional blob name for the projection.
    /// </summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a new projection if it doesn't exist.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string? Connection { get; set; }
}
