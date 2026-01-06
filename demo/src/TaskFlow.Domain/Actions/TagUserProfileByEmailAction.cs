using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;
using System.Security.Cryptography;
using System.Text;

namespace TaskFlow.Domain.Actions;

/// <summary>
/// Post-commit action that tags UserProfile documents with a hashed email for lookup purposes.
/// Emails are normalized (lowercased) and hashed using SHA256 before being set as document tags.
/// </summary>
public class TagUserProfileByEmailAction : IAsyncPostCommitAction
{
    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        if (document is not IObjectDocumentWithMethods docWithMethods)
        {
            return;
        }

        foreach (var jsonEvent in events)
        {
            // Only tag on creation - email is used for initial lookup
            // If email changes via UserProfileUpdated, the tag remains with the original email
            if (jsonEvent.EventType == "UserProfile.Created")
            {
                var created = JsonEvent.To(jsonEvent, UserProfileCreatedJsonSerializerContext.Default.UserProfileCreated);
                var emailHash = HashEmail(created.Email);
                await docWithMethods.SetTagAsync(emailHash, TagTypes.DocumentTag);
            }
        }
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
