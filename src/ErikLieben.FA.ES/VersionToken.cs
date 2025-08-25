using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES;

[JsonConverter(typeof(VersionTokenJsonConverter))]
public record VersionToken
{
    public VersionToken()
    {
        Version = 0;
    }
    public VersionToken(string versionTokenString)
    {
        ArgumentNullException.ThrowIfNull(versionTokenString);

        Version = 0;
        Value = versionTokenString;
        if (!string.IsNullOrWhiteSpace(versionTokenString))
        {
            ParseFullString(versionTokenString);
        }
    }

    public VersionToken(string objectIdentifierPart, string versionIdentifierPart)
    {
        ArgumentNullException.ThrowIfNull(objectIdentifierPart);
        ArgumentNullException.ThrowIfNull(versionIdentifierPart);

        Version = -1;
        Value = $"{objectIdentifierPart}__{versionIdentifierPart}";
        ParseFullString(Value);
    }

    public VersionToken(IEvent @event, IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(document);

        ObjectId = document.ObjectId;
        ObjectName = document.ObjectName;
        StreamIdentifier = document.Active.StreamIdentifier;
        Version = @event.EventVersion;
        VersionString = ToVersionTokenString(@event.EventVersion);

        var objectIdentifierPart = $"{ObjectName}__{ObjectId}";
        var versionIdentifierPart = $"{StreamIdentifier}__{VersionString}";
        Value = $"{objectIdentifierPart}__{versionIdentifierPart}";
    }

    public VersionToken(string objectName, string objectId, string streamIdentifier, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamIdentifier);

        ObjectId = objectId;
        ObjectName = objectName;
        StreamIdentifier = streamIdentifier;
        Version = version;
        VersionString = ToVersionTokenString(version);

        var objectIdentifierPart = $"{ObjectName}__{ObjectId}";
        var versionIdentifierPart = $"{StreamIdentifier}__{VersionString}";
        Value = $"{objectIdentifierPart}__{versionIdentifierPart}";
    }

    public VersionToken(ObjectIdentifier objectIdentifier, VersionIdentifier versionIdentifier)
    {
        ArgumentNullException.ThrowIfNull(objectIdentifier);
        ArgumentNullException.ThrowIfNull(versionIdentifier);

        Version = -1;
        ParseFullString($"{objectIdentifier.Value}__{versionIdentifier.Value}");
    }

    protected void ParseFullString(string value)
    {
        var parts = value.Split("__");
        if (parts.Length != 4)
        {
            throw new ArgumentException($"IdentifierString must consist out if 4 parts split by '__', current token is '{value}'");
        }

        ObjectName = parts[0];
        ObjectId = parts[1];
        StreamIdentifier = parts[2];
        Version = int.Parse(parts[3]);
        VersionString = parts[3];
        Value = value;
    }

    public string Value { get; protected set; } = string.Empty;

    public ObjectIdentifier ObjectIdentifier => new(ObjectName, ObjectId);
    public VersionIdentifier VersionIdentifier => new(StreamIdentifier, Version);


    public string ObjectName { get; protected set; } = string.Empty;

    public string ObjectId { get; protected set; } = string.Empty;

    public string StreamIdentifier { get; protected set; } = string.Empty;

    public int Version { get; protected set; }

    public string VersionString { get; protected set; } = string.Empty;

    public string SchemaVersion { get; init; } = "v1";

    [JsonIgnore]
    public bool TryUpdateToLatestVersion { get; protected set; }

    public VersionToken ToLatestVersion()
    {
        return this with { TryUpdateToLatestVersion = true };
    }

    public static string ToVersionTokenString(int? version)
    {
        return version?.ToString("00000000000000000000") ?? string.Empty;
    }

    public static string From(IEvent @event, IObjectDocument document)
    {
        return $"{document.ObjectName}__{document.ObjectId}__{document.Active.StreamIdentifier}__{ToVersionTokenString(@event.EventVersion)}";
    }
}
