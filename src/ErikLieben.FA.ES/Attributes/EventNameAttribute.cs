namespace ErikLieben.FA.ES.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class EventNameAttribute : Attribute
{
    public EventNameAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }

    public string Name { get; init; }
}