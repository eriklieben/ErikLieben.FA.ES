using System.Security.Cryptography;
using System.Text;

namespace ErikLieben.FA.ES.Uniqueness;

/// <summary>
/// Generates deterministic stream IDs from unique values for implementing uniqueness constraints.
/// </summary>
/// <remarks>
/// <para>
/// This utility enables uniqueness constraints in event sourcing by using a hash of the unique value
/// as the stream ID. Combined with <see cref="Constraint.New"/>, the event store enforces uniqueness
/// at the storage level.
/// </para>
/// <para>
/// Example: To ensure unique emails, hash the email to create a user ID:
/// <code>
/// var userId = UniqueIdGenerator.FromUniqueValue("user", email);
/// await user.Create(email, name); // Uses Constraint.New internally
/// </code>
/// If another user tries to register with the same email, the same stream ID is generated,
/// and <see cref="Constraint.New"/> throws a <see cref="Exceptions.ConstraintException"/>.
/// </para>
/// </remarks>
public static class UniqueIdGenerator
{
    /// <summary>
    /// Default hash length (16 characters = 64 bits of the hash).
    /// </summary>
    public const int DefaultHashLength = 16;

    /// <summary>
    /// Generates a deterministic stream ID from a unique value.
    /// </summary>
    /// <param name="prefix">The prefix for the stream ID (e.g., "user", "order").</param>
    /// <param name="uniqueValue">The unique value to hash (e.g., email, username).</param>
    /// <param name="hashLength">Number of hash characters to include (default: 16).</param>
    /// <returns>A deterministic stream ID in the format "{prefix}-{hash}".</returns>
    /// <exception cref="ArgumentNullException">Thrown when prefix or uniqueValue is null.</exception>
    /// <exception cref="ArgumentException">Thrown when prefix or uniqueValue is empty or whitespace.</exception>
    /// <example>
    /// <code>
    /// var userId = UniqueIdGenerator.FromUniqueValue("user", "user@example.com");
    /// // Result: "user-a1b2c3d4e5f67890"
    /// </code>
    /// </example>
    public static string FromUniqueValue(string prefix, string uniqueValue, int hashLength = DefaultHashLength)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(uniqueValue);

        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be empty or whitespace.", nameof(prefix));

        if (string.IsNullOrWhiteSpace(uniqueValue))
            throw new ArgumentException("Unique value cannot be empty or whitespace.", nameof(uniqueValue));

        if (hashLength < 8 || hashLength > 64)
            throw new ArgumentOutOfRangeException(nameof(hashLength), "Hash length must be between 8 and 64.");

        var normalized = uniqueValue.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hashString = Convert.ToHexString(hash).ToLowerInvariant();

        return $"{prefix}-{hashString[..hashLength]}";
    }

    /// <summary>
    /// Generates a deterministic stream ID from multiple unique values (composite key).
    /// </summary>
    /// <param name="prefix">The prefix for the stream ID.</param>
    /// <param name="uniqueValues">The unique values to hash together.</param>
    /// <param name="hashLength">Number of hash characters to include (default: 16).</param>
    /// <returns>A deterministic stream ID.</returns>
    /// <example>
    /// <code>
    /// // Create ID from tenant + email combination
    /// var userId = UniqueIdGenerator.FromCompositeKey("user", ["tenant-123", "user@example.com"]);
    /// </code>
    /// </example>
    public static string FromCompositeKey(string prefix, IEnumerable<string> uniqueValues, int hashLength = DefaultHashLength)
    {
        ArgumentNullException.ThrowIfNull(uniqueValues);

        var combined = string.Join(":", uniqueValues.Select(v => v?.Trim().ToLowerInvariant() ?? string.Empty));
        return FromUniqueValue(prefix, combined, hashLength);
    }

    /// <summary>
    /// Validates that a given ID was generated from the expected unique value.
    /// </summary>
    /// <param name="prefix">The prefix used when generating the ID.</param>
    /// <param name="id">The ID to validate.</param>
    /// <param name="expectedUniqueValue">The expected unique value.</param>
    /// <returns>True if the ID matches the expected unique value; otherwise, false.</returns>
    public static bool ValidateId(string prefix, string id, string expectedUniqueValue)
    {
        var expectedId = FromUniqueValue(prefix, expectedUniqueValue);
        return string.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase);
    }
}
