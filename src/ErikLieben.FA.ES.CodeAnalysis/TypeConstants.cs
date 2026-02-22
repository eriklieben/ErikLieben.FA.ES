namespace ErikLieben.FA.ES.CodeAnalysis;

/// <summary>
/// Shared constants for type names used across code analysis tools.
/// </summary>
public static class TypeConstants
{
    /// <summary>
    /// The root namespace for the ErikLieben.FA.ES framework.
    /// </summary>
    public const string FrameworkNamespace = "ErikLieben.FA.ES";

    /// <summary>
    /// The namespace containing framework attributes.
    /// </summary>
    public const string FrameworkAttributesNamespace = "ErikLieben.FA.ES.Attributes";

    /// <summary>
    /// Full type name for the Aggregate base class.
    /// </summary>
    public const string AggregateFullName = "ErikLieben.FA.ES.Processors.Aggregate";

    /// <summary>
    /// Full type name for the Projection base class.
    /// </summary>
    public const string ProjectionFullName = "ErikLieben.FA.ES.Projections.Projection";

    /// <summary>
    /// Full type name for the RoutedProjection base class.
    /// </summary>
    public const string RoutedProjectionFullName = "ErikLieben.FA.ES.Projections.RoutedProjection";

    /// <summary>
    /// Full type name for the IEventStream interface.
    /// </summary>
    public const string IEventStreamFullName = "ErikLieben.FA.ES.IEventStream";

    /// <summary>
    /// Namespace containing StreamAction attributes.
    /// </summary>
    public const string StreamActionAttributeNamespace = "ErikLieben.FA.ES.Attributes";

    /// <summary>
    /// Namespace containing When attributes.
    /// </summary>
    public const string WhenAttributeNamespace = "ErikLieben.FA.ES.Attributes";

    /// <summary>
    /// Stream action interface names used for registration.
    /// </summary>
    public static readonly string[] StreamActionInterfaceNames =
    [
        "IAsyncPostCommitAction",
        "IPostAppendAction",
        "IPostReadAction",
        "IPreAppendAction",
        "IPreReadAction"
    ];
}
