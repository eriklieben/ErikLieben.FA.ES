using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES.VersionTokenParts;

[JsonConverter(typeof(VersionIdentifierJsonConverter))]
public record VersionIdentifier : IComparable<VersionIdentifier>, IComparable
{
    public string StreamIdentifier { get; private set; } = string.Empty;
    public string VersionString { get; private set; } = string.Empty;


    public int CompareTo(VersionIdentifier? other)
    {
        return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }
    public int CompareTo(object? obj)
    {
       var other = obj as VersionIdentifier;
       return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    public string Value => $"{StreamIdentifier}__{VersionString}";
    public override string ToString() => Value;

    public string SchemaVersion { get; init; } = "v1";

    public VersionIdentifier()
    {

    }

    public VersionIdentifier(string streamIdentifier,  int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamIdentifier);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(version, -1, nameof(version));

        StreamIdentifier = streamIdentifier;
        VersionString = version.ToString(("D20"));
    }

    public VersionIdentifier(string versionTokenString)
    {
        ArgumentNullException.ThrowIfNull(versionTokenString);
        var parts = versionTokenString.Split("__").Where((s) => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"IdentifierString must consist out if 2 parts split by __, current token is '{versionTokenString}'");
        }

        StreamIdentifier = parts[0];
        VersionString = parts[1];
    }


}
