namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Indicates that a projection is a routed projection that forwards events to partition projections.
/// Used by the source generator to create routing and partition management code.
/// </summary>
/// <param name="routerType">The type of the partition router (must implement IPartitionRouter&lt;TPartition&gt;).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class RoutedProjectionAttribute(Type routerType) : Attribute
{
    /// <summary>
    /// Gets the type of the partition router.
    /// </summary>
    public Type RouterType { get; } = routerType;
}
