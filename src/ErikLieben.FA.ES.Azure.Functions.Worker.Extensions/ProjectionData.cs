using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Represents binding data for resolving and loading a projection in Azure Functions.
/// </summary>
public class ProjectionData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionData"/> class.
    /// </summary>
    /// <param name="blobName">Optional blob name to load the projection from.</param>
    /// <param name="createIfNotExists">Whether to create a new projection if it doesn't exist.</param>
    public ProjectionData(string? blobName = null, bool createIfNotExists = true)
    {
        BlobName = blobName;
        CreateIfNotExists = createIfNotExists;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionData"/> class for JSON deserialization.
    /// </summary>
    [JsonConstructor]
    internal ProjectionData() { }

    /// <summary>
    /// Gets or sets the optional blob name. If not provided, uses the default name based on the projection type.
    /// </summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a new projection if it doesn't exist.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;
}
