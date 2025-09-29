using System.Text.Json.Serialization;
using ErikLieben.FA.ES.JsonConverters;

namespace ErikLieben.FA.ES.VersionTokenParts;

/// <summary>
/// Represents an object identifier composed of an object name and an object id.
/// </summary>
[JsonConverter(typeof(ObjectIdentifierJsonConverter))]
public record ObjectIdentifier : IComparable<ObjectIdentifier>, IComparable
{
    /// <summary>
    /// Gets the object name component.
    /// </summary>
    public string ObjectName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the object id component.
    /// </summary>
    public string ObjectId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the combined value in the form "{ObjectName}__{ObjectId}".
    /// </summary>
    public string Value => $"{ObjectName}__{ObjectId}";

    /// <summary>
    /// Compares this instance with another <see cref="ObjectIdentifier"/>.
    /// </summary>
    public int CompareTo(ObjectIdentifier? other)
    {
        return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Compares this instance with another object which should be an <see cref="ObjectIdentifier"/>.
    /// </summary>
    public int CompareTo(object? obj)
    {
       var other = obj as ObjectIdentifier;
       return string.Compare(Value, other?.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Gets the schema version of the identifier format.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1";

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectIdentifier"/> class.
    /// </summary>
    public ObjectIdentifier()
    {

    }

    /// <summary>
    /// Initializes a new instance by parsing a combined object identifier string.
    /// </summary>
    /// <param name="objectIdentifierString">The identifier string in the form "{ObjectName}__{ObjectId}".</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="objectIdentifierString"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the token cannot be parsed into exactly two parts.</exception>
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

    /// <summary>
    /// Initializes a new instance from explicit object name and id.
    /// </summary>
    /// <param name="objectName">The object name (type) value.</param>
    /// <param name="objectId">The object identifier value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="objectName"/> or <paramref name="objectId"/> is null, empty, or whitespace.</exception>
    public ObjectIdentifier(string objectName, string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        ObjectName = objectName;
        ObjectId = objectId;
    }
}
