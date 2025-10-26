namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a projection that uses an external checkpoint mechanism for tracking progress.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ProjectionWithExternalCheckpointAttribute : Attribute
{
}
