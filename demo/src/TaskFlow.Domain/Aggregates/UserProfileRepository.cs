using System.Security.Cryptography;
using System.Text;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Extension methods for UserProfileRepository
/// </summary>
public partial interface IUserProfileRepository
{
    /// <summary>
    /// Finds a user profile by email address using tag-based lookup.
    /// The email is normalized (lowercased) and hashed with SHA256 before lookup.
    /// </summary>
    /// <param name="email">The email address to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user profile if found, null otherwise</returns>
    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extension implementation for UserProfileRepository
/// </summary>
public partial class UserProfileRepository
{
    /// <summary>
    /// Finds a user profile by email address using tag-based lookup.
    /// The email is normalized (lowercased) and hashed with SHA256 before lookup.
    /// </summary>
    /// <param name="email">The email address to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user profile if found, null otherwise</returns>
    public async Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // Hash the email the same way as TagUserProfileByEmailAction
        var emailHash = HashEmail(email);

        // Use the generated repository method to get by document tag
        return await GetFirstByDocumentTagAsync(emailHash, cancellationToken);
    }

    /// <summary>
    /// Hashes an email address using SHA256 for use as a document tag.
    /// Emails are lowercased before hashing to ensure case-insensitive lookups.
    /// </summary>
    private static string HashEmail(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var emailBytes = Encoding.UTF8.GetBytes(normalizedEmail);
        var hashBytes = SHA256.HashData(emailBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
