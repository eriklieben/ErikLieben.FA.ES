namespace ErikLieben.FA.ES.CLI.Model;

/// <summary>
/// Definition of a destination projection discovered during analysis.
/// Destination projections are regular Projection classes used within RoutedProjection.
/// </summary>
public record DestinationProjectionDefinition : ProjectionDefinition
{
    /// <summary>
    /// Indicates this is a destination projection (used within a RoutedProjection).
    /// </summary>
    public bool IsDestinationProjection { get; set; }
}
