namespace ErikLieben.FA.ES.AspNetCore.MinimalApis;

/// <summary>
/// Marks a parameter for automatic projection binding in Minimal API endpoints.
/// The projection will be loaded from storage or created if it doesn't exist.
/// </summary>
/// <remarks>
/// <para>
/// Usage examples:
/// <code>
/// // Load projection with default blob name
/// app.MapGet("/dashboard", async ([Projection] DashboardProjection dashboard) => { });
///
/// // Routed projection with blob name from route parameter
/// app.MapGet("/orders/{id}/summary", async ([Projection("{id}")] OrderSummaryProjection summary) => { });
///
/// // Projection that must exist
/// app.MapGet("/stats", async ([Projection(CreateIfNotExists = false)] StatsProjection stats) => { });
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the blob name pattern for routed projections.
    /// </summary>
    /// <remarks>
    /// Supports route parameter substitution using curly braces.
    /// For example, "{id}" will be replaced with the value of the "id" route parameter.
    /// If <c>null</c>, the default blob name for the projection type is used.
    /// </remarks>
    public string? BlobNamePattern { get; }

    /// <summary>
    /// Gets or sets whether to create a new projection if it doesn't exist.
    /// </summary>
    /// <remarks>
    /// When <c>true</c> (default), a new projection is created if it doesn't exist.
    /// When <c>false</c>, an exception is thrown if the projection doesn't exist.
    /// </remarks>
    public bool CreateIfNotExists { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionAttribute"/> class
    /// using the default blob name for the projection type.
    /// </summary>
    public ProjectionAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionAttribute"/> class
    /// with the specified blob name pattern.
    /// </summary>
    /// <param name="blobNamePattern">
    /// The blob name pattern. Supports route parameter substitution (e.g., "{id}").
    /// </param>
    public ProjectionAttribute(string blobNamePattern)
    {
        BlobNamePattern = blobNamePattern;
    }
}
