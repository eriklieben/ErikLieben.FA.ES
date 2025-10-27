namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Provides comparison logic for version token strings based on their object identifier and version identifier components.
/// </summary>
public class VersionTokenComparer : IVersionTokenComparer
{
    /// <summary>
    /// Compares two version token strings.
    /// </summary>
    /// <param name="x">The first version token string to compare.</param>
    /// <param name="y">The second version token string to compare.</param>
    /// <returns>A value less than zero if x is older than y, zero if they are equal, or greater than zero if x is newer than y.</returns>
    /// <exception cref="Exceptions.VersionTokenStreamMismatchException">Thrown when comparing version tokens from different object streams.</exception>
    public int Compare(string? x, string? y)
    {
        x = x?.ToLowerInvariant();
        y = y?.ToLowerInvariant();

        if (x == y)
        {
            return 0;
        }

        if (x == null && y != null)
        {
            return -1;
        }

        if (x != null && y == null)
        {
            return 1;
        }

        var xIdentifier = new VersionToken(x!);
        var yIdentifier = new VersionToken(y!);

        if (xIdentifier.ObjectIdentifier != yIdentifier.ObjectIdentifier)
        {
            throw new Exceptions.VersionTokenStreamMismatchException(xIdentifier.ObjectIdentifier.Value, yIdentifier.ObjectIdentifier.Value);
        }

        return string.Compare(xIdentifier.VersionIdentifier.Value, yIdentifier.VersionIdentifier.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the new version token represents a newer version than the existing one.
    /// </summary>
    /// <param name="new">The new version token string to compare.</param>
    /// <param name="existing">The existing version token string to compare against, or null if no existing version.</param>
    /// <returns>True if the new version is newer than the existing version, or if existing is null; otherwise, false.</returns>
    public bool IsNewer(string @new, string? existing)
    {
        if (existing == null)
        {
            return true;
        }

        //   Greater than zero, x is greater than y.
        return Compare(@new, existing) > 0;
    }

    /// <summary>
    /// Determines whether the new version token represents an older version than the existing one.
    /// </summary>
    /// <param name="new">The new version token string to compare.</param>
    /// <param name="existing">The existing version token string to compare against.</param>
    /// <returns>True if the new version is older than the existing version; otherwise, false.</returns>
    public bool IsOlder(string @new, string? existing)
    {
        //   Greater than zero, x is greater than y.
        return Compare(@new, existing) < 0;
    }
}
