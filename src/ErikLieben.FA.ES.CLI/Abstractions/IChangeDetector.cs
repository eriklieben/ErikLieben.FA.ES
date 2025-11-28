namespace ErikLieben.FA.ES.CLI.Abstractions;

/// <summary>
/// Represents a detected change in the solution definition.
/// </summary>
public record DetectedChange(
    ChangeType Type,
    ChangeCategory Category,
    string EntityType,
    string EntityName,
    string Description,
    string? Details = null);

/// <summary>
/// The type of change detected.
/// </summary>
public enum ChangeType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// The category of entity that changed.
/// </summary>
public enum ChangeCategory
{
    Aggregate,
    Projection,
    InheritedAggregate,
    RoutedProjection,
    Event,
    Property,
    JsonSerializable,
    WhenMethod,
    Command,
    Constructor,
    PostWhen,
    StreamAction,
    VersionToken,
    BlobSettings,
    EventStreamType
}

/// <summary>
/// Detects changes between solution definitions.
/// </summary>
public interface IChangeDetector
{
    /// <summary>
    /// Compares two solution definitions and returns detected changes.
    /// </summary>
    IReadOnlyList<DetectedChange> DetectChanges(
        Model.SolutionDefinition? previous,
        Model.SolutionDefinition current);
}
