namespace ErikLieben.FA.ES.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class IgnoreAttribute : Attribute
{
    public IgnoreAttribute(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; init; }
}
