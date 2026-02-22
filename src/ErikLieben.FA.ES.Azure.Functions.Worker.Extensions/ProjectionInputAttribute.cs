using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Specifies that a parameter binds to a projection loaded from storage.
/// </summary>
/// <remarks>
/// The attribute uses the configured <see cref="ProjectionConverter"/> to load or create the target projection
/// based on the projection type and optional blob name.
/// </remarks>
[InputConverter(typeof(ProjectionConverter))]
[ConverterFallbackBehavior(ConverterFallbackBehavior.Default)]
[AttributeUsage(AttributeTargets.Parameter)]
public class ProjectionInputAttribute : InputBindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionInputAttribute"/> class.
    /// </summary>
    public ProjectionInputAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionInputAttribute"/> class with a specific blob name.
    /// </summary>
    /// <param name="blobName">The blob name to load the projection from.</param>
    public ProjectionInputAttribute(string blobName)
    {
        BlobName = blobName;
    }

    /// <summary>
    /// Gets or sets the optional blob name. If not provided, uses the default name based on the projection type.
    /// </summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create a new projection if it doesn't exist.
    /// Defaults to true.
    /// </summary>
    public bool CreateIfNotExists { get; set; } = true;
}
