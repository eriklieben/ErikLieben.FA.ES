using Amazon.S3;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Factory for creating named <see cref="IAmazonS3"/> client instances.
/// </summary>
public interface IS3ClientFactory
{
    /// <summary>
    /// Creates or retrieves a cached <see cref="IAmazonS3"/> client by name.
    /// </summary>
    /// <param name="name">The logical client name used for identification and caching.</param>
    /// <returns>An <see cref="IAmazonS3"/> client instance.</returns>
    IAmazonS3 CreateClient(string name);
}
