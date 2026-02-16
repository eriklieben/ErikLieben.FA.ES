using System.Text.RegularExpressions;

namespace ErikLieben.FA.ES.Validation;

/// <summary>
/// Validates object identifiers to prevent path traversal and injection attacks.
/// Object IDs are used in storage paths (blob names, S3 keys, table partition/row keys),
/// so they must be restricted to safe characters.
/// </summary>
public static partial class ObjectIdValidator
{
    /// <summary>
    /// Validates that the given object ID is safe for use in storage operations.
    /// Rejects IDs containing path traversal sequences or disallowed characters.
    /// </summary>
    /// <param name="objectId">The object ID to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the object ID contains invalid characters or sequences.</exception>
    public static void Validate(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        if (objectId.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Object ID '{objectId}' contains path traversal sequence '..' which is not allowed.",
                nameof(objectId));
        }

        if (!AllowedObjectIdRegex().IsMatch(objectId))
        {
            throw new ArgumentException(
                $"Object ID '{objectId}' contains invalid characters. " +
                "Only alphanumeric characters, hyphens, underscores, and dots are allowed.",
                nameof(objectId));
        }
    }

    /// <summary>
    /// Matches object IDs that contain only allowed characters:
    /// alphanumeric, hyphens, underscores, and single dots (not consecutive).
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9\-_.]+$")]
    private static partial Regex AllowedObjectIdRegex();
}
