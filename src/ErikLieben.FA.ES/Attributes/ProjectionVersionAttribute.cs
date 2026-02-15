namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies the schema version for a projection type.
/// </summary>
/// <remarks>
/// Use this attribute to indicate when a projection's schema has changed in a breaking way.
/// When the stored schema version differs from the code schema version, the projection
/// needs to be rebuilt to apply the new logic. If not specified, the schema version defaults to 1.
/// </remarks>
/// <example>
/// <code lang="csharp">
/// [BlobJsonProjection("projections")]
/// [ProjectionVersion(2)]
/// public partial class OrderDashboard : Projection
/// {
///     // New field added in v2
///     public decimal TotalRevenue { get; private set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class ProjectionVersionAttribute : Attribute
{
    /// <summary>
    /// The default schema version used when no attribute is specified.
    /// </summary>
    public const int DefaultVersion = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionVersionAttribute"/> class with the specified version.
    /// </summary>
    /// <param name="version">The schema version for the projection. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 1.</exception>
    public ProjectionVersionAttribute(int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Schema version must be at least 1.");
        }

        Version = version;
    }

    /// <summary>
    /// Gets the schema version for the projection.
    /// </summary>
    public int Version { get; init; }
}
