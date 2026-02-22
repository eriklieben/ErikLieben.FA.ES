using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.JsonConverters;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES;

/// <summary>
/// Represents a compact identifier for a specific version within an object event stream.
/// </summary>
/// <remarks>
/// The token has the canonical form: "{ObjectName}__{ObjectId}__{StreamIdentifier}__{VersionString}" where VersionString is a zero-padded
/// numeric string created by <see cref="ToVersionTokenString(int?)"/>. It can also carry a flag to indicate an update to the latest version.
/// </remarks>
[JsonConverter(typeof(VersionTokenJsonConverter))]
public record VersionToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionToken"/> class with <see cref="Version"/> set to 0.
    /// </summary>
    public VersionToken()
    {
        Version = 0;
    }

    /// <summary>
    /// Initializes a new instance by parsing the full token string.
    /// </summary>
    /// <param name="versionTokenString">The full token string to parse.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="versionTokenString"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the format is invalid.</exception>
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

    /// <summary>
    /// Initializes a new instance from object and version identifier parts.
    /// </summary>
    /// <param name="objectIdentifierPart">The object identifier part: "{ObjectName}__{ObjectId}".</param>
    /// <param name="versionIdentifierPart">The version identifier part: "{StreamIdentifier}__{VersionString}".</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the combined format is invalid.</exception>
    public VersionToken(string objectIdentifierPart, string versionIdentifierPart)
    {
        ArgumentNullException.ThrowIfNull(objectIdentifierPart);
        ArgumentNullException.ThrowIfNull(versionIdentifierPart);

        Version = -1;
        Value = $"{objectIdentifierPart}__{versionIdentifierPart}";
        ParseFullString(Value);
    }

    /// <summary>
    /// Initializes a new instance from the given event and its document.
    /// </summary>
    /// <param name="event">The event containing the version and type.</param>
    /// <param name="document">The document containing the object and stream identifiers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="event"/> or <paramref name="document"/> is null.</exception>
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

    /// <summary>
    /// Initializes a new instance from explicit parts.
    /// </summary>
    /// <param name="objectName">The object type/name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="streamIdentifier">The stream identifier.</param>
    /// <param name="version">The zero-based version.</param>
    /// <exception cref="ArgumentException">Thrown when any string argument is null, empty, or whitespace.</exception>
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

    /// <summary>
    /// Initializes a new instance from strongly typed identifier parts.
    /// </summary>
    /// <param name="objectIdentifier">The object identifier.</param>
    /// <param name="versionIdentifier">The version identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public VersionToken(ObjectIdentifier objectIdentifier, VersionIdentifier versionIdentifier)
    {
        ArgumentNullException.ThrowIfNull(objectIdentifier);
        ArgumentNullException.ThrowIfNull(versionIdentifier);

        Version = -1;
        ParseFullString($"{objectIdentifier.Value}__{versionIdentifier.Value}");
    }

    /// <summary>
    /// Parses the full token string and fills all properties.
    /// </summary>
    /// <param name="value">The full token string to parse.</param>
    /// <exception cref="ArgumentException">Thrown when the input does not contain 4 parts separated by "__".</exception>
    protected void ParseFullString(string value)
    {
        ReadOnlySpan<char> span = value.AsSpan();
        const string separator = "__";

        int firstIdx = span.IndexOf(separator);
        if (firstIdx == -1)
        {
            throw new ArgumentException($"IdentifierString must consist out if 4 parts split by '__', current token is '{value}'");
        }

        int secondIdx = span[(firstIdx + 2)..].IndexOf(separator);
        if (secondIdx == -1)
        {
            throw new ArgumentException($"IdentifierString must consist out if 4 parts split by '__', current token is '{value}'");
        }
        secondIdx += firstIdx + 2;

        int thirdIdx = span[(secondIdx + 2)..].IndexOf(separator);
        if (thirdIdx == -1)
        {
            throw new ArgumentException($"IdentifierString must consist out if 4 parts split by '__', current token is '{value}'");
        }
        thirdIdx += secondIdx + 2;

        ObjectName = span[..firstIdx].ToString();
        ObjectId = span[(firstIdx + 2)..secondIdx].ToString();
        StreamIdentifier = span[(secondIdx + 2)..thirdIdx].ToString();
        VersionString = span[(thirdIdx + 2)..].ToString();
        Version = int.Parse(VersionString);
        Value = value;
    }

    /// <summary>
    /// Gets the full token string value.
    /// </summary>
    public string Value { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the strongly typed object identifier derived from this token.
    /// </summary>
    public ObjectIdentifier ObjectIdentifier => new(ObjectName, ObjectId);

    /// <summary>
    /// Gets the strongly typed version identifier derived from this token.
    /// </summary>
    public VersionIdentifier VersionIdentifier => new(StreamIdentifier, Version);

    /// <summary>
    /// Gets the object type/name.
    /// </summary>
    public string ObjectName { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the object identifier.
    /// </summary>
    public string ObjectId { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public string StreamIdentifier { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the zero-based version.
    /// </summary>
    public int Version { get; protected set; }

    /// <summary>
    /// Gets the zero-padded version string.
    /// </summary>
    public string VersionString { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the schema version of the token format.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1";

    /// <summary>
    /// Gets a value indicating whether reading operations should continue to the latest version.
    /// </summary>
    [JsonIgnore]
    public bool TryUpdateToLatestVersion { get; protected set; }

    /// <summary>
    /// Marks this token to indicate that the update should continue to the latest version.
    /// </summary>
    /// <returns>A copy of this token with <see cref="TryUpdateToLatestVersion"/> set to true.</returns>
    public VersionToken ToLatestVersion()
    {
        return this with { TryUpdateToLatestVersion = true };
    }

    /// <summary>
    /// Converts a numeric version to its zero-padded token string.
    /// </summary>
    /// <param name="version">The version to convert; null yields an empty string.</param>
    /// <returns>A zero-padded 20-character string or an empty string when null.</returns>
    public static string ToVersionTokenString(int? version)
    {
        return version?.ToString("00000000000000000000") ?? string.Empty;
    }

    /// <summary>
    /// Creates the full token string for the specified event and document.
    /// </summary>
    /// <param name="event">The event providing the version.</param>
    /// <param name="document">The document providing object and stream identifiers.</param>
    /// <returns>The full token string.</returns>
    public static string From(IEvent @event, IObjectDocument document)
    {
        return $"{document.ObjectName}__{document.ObjectId}__{document.Active.StreamIdentifier}__{ToVersionTokenString(@event.EventVersion)}";
    }
}
