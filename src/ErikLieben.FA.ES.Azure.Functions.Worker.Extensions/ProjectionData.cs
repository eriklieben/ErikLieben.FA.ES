using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Represents binding data for resolving and loading a projection in Azure Functions.
/// </summary>
public class ProjectionData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionData"/> class with default settings.
    /// </summary>
    [JsonConstructor]
    public ProjectionData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionData"/> class.
    /// </summary>
    /// <param name="blobName">The blob name to load the projection from.</param>
    public ProjectionData(string blobName)
    {
        BlobName = blobName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionData"/> class.
    /// </summary>
    /// <param name="blobName">The blob name to load the projection from.</param>
    /// <param name="createIfNotExists">Whether to create a new projection if it doesn't exist.</param>
    public ProjectionData(string blobName, bool createIfNotExists)
    {
        BlobName = blobName;
        CreateIfNotExists = createIfNotExists;
    }

    /// <summary>
    /// Gets or sets the optional blob name. If not provided, uses the default name based on the projection type.
    /// </summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a new projection if it doesn't exist.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;
}
