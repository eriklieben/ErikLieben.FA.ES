namespace ErikLieben.FA.ES.CLI.Model;

/// <summary>
/// Definition of a routed projection discovered during analysis.
/// </summary>
public record RoutedProjectionDefinition : ProjectionDefinition
{
    public bool IsRoutedProjection { get; set; }
    public string? RouterType { get; set; }
    public string? DestinationType { get; set; }
    public string? PathTemplate { get; set; }

    /// <summary>
    /// Maps destination type names to their [BlobJsonProjection] path templates.
    /// </summary>
    public Dictionary<string, string> DestinationPathTemplates { get; set; } = new();

    /// <summary>
    /// Set of destination type names that have [ProjectionWithExternalCheckpoint] attribute.
    /// </summary>
    public HashSet<string> DestinationsWithExternalCheckpoint { get; set; } = [];
}
