namespace ErikLieben.FA.ES.Attributes;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ObjectNameAttribute : Attribute
{
    public ObjectNameAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }

    public string Name { get; init; }
}
    