namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a class or struct as a custom object serializer.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ObjectSerializerAttribute : Attribute { }