namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a class or struct as an aggregate root in the event sourcing domain model.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class AggregateAttribute : Attribute
{
}
