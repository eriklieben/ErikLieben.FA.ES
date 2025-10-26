namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a class or struct as a factory for creating aggregate instances.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class AggregateFactoryAttribute : Attribute
{
}
