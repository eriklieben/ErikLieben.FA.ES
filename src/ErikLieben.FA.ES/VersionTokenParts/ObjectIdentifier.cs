using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES.VersionTokenParts;

[JsonConverter(typeof(ObjectIdentifierJsonConverter))]
public record ObjectIdentifier : IComparable<ObjectIdentifier>, IComparable
{
    public string ObjectName { get; private set; } = string.Empty;

    public string ObjectId { get; private set; } = string.Empty;

    public string Value => $"{ObjectName}__{ObjectId}";


    public int CompareTo(ObjectIdentifier? other)
    {
        return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }
    public int CompareTo(object? obj)
    {
       var other = obj as ObjectIdentifier;
       return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    public override string ToString() => Value;


    public string SchemaVersion { get; init; } = "v1";

    public ObjectIdentifier()
    {

    }

    public ObjectIdentifier(string objectIdentifierString)
    {
        ArgumentNullException.ThrowIfNull(objectIdentifierString);
        var parts = objectIdentifierString.Split("__").Where((s) => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"IdentifierString must consist out if 2 parts split by __, current token is '{objectIdentifierString}'");
        }

        ObjectName = parts[0];
        ObjectId = parts[1];
    }

    public ObjectIdentifier(string objectName, string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        ObjectName = objectName;
        ObjectId = objectId;
    }
}
