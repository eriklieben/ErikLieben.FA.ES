namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Defines methods for comparing version token strings to determine their temporal ordering.
/// </summary>
public interface IVersionTokenComparer : IComparer<string>
{
    /// <summary>
    /// Determines whether the new version token represents a newer version than the existing one.
    /// </summary>
    /// <param name="new">The new version token string to compare.</param>
    /// <param name="existing">The existing version token string to compare against, or null if no existing version.</param>
    /// <returns>True if the new version is newer than the existing version, or if existing is null; otherwise, false.</returns>
    bool IsNewer(string @new, string? existing);

    /// <summary>
    /// Determines whether the new version token represents an older version than the existing one.
    /// </summary>
    /// <param name="new">The new version token string to compare.</param>
    /// <param name="existing">The existing version token string to compare against.</param>
    /// <returns>True if the new version is older than the existing version; otherwise, false.</returns>
    bool IsOlder(string @new, string existing);
}