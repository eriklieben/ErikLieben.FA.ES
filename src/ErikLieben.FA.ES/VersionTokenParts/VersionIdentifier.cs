using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES.VersionTokenParts;

/// <summary>
/// Represents a version identifier composed of a stream identifier and a zero-padded version string.
/// </summary>
[JsonConverter(typeof(VersionIdentifierJsonConverter))]
public record VersionIdentifier : IComparable<VersionIdentifier>, IComparable
{
    /// <summary>
    /// Gets the stream identifier part of the version identifier.
    /// </summary>
    public string StreamIdentifier { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the zero-padded version string (20 digits).
    /// </summary>
    public string VersionString { get; private set; } = string.Empty;

    /// <summary>
    /// Compares this instance with another <see cref="VersionIdentifier"/>.
    /// </summary>
    /// <param name="other">The other version identifier to compare with.</param>
    /// <returns>A value less than zero if this instance precedes <paramref name="other"/>; zero if equal; greater than zero if it follows.</returns>
    public int CompareTo(VersionIdentifier? other)
    {
        return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares this instance with another object which should be a <see cref="VersionIdentifier"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>A value less than zero, zero, or greater than zero depending on the relative order.</returns>
    public int CompareTo(object? obj)
    {
       var other = obj as VersionIdentifier;
       return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the combined value string in the form "{StreamIdentifier}__{VersionString}".
    /// </summary>
    public string Value => $"{StreamIdentifier}__{VersionString}";

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Gets the schema version of the identifier format.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1";

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionIdentifier"/> class.
    /// </summary>
    public VersionIdentifier()
    {

    }

    /// <summary>
    /// Initializes a new instance from explicit stream identifier and version.
    /// </summary>
    /// <param name="streamIdentifier">The stream identifier string.</param>
    /// <param name="version">The non-negative version number.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="streamIdentifier"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 0.</exception>
    public VersionIdentifier(string streamIdentifier,  int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamIdentifier);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(version, -1);

        StreamIdentifier = streamIdentifier;
        VersionString = version.ToString(("D20"));
    }

    /// <summary>
    /// Initializes a new instance by parsing a combined version token string.
    /// </summary>
    /// <param name="versionTokenString">The token string in the form "{StreamIdentifier}__{VersionString}".</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="versionTokenString"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the token cannot be parsed into exactly two parts.</exception>
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
