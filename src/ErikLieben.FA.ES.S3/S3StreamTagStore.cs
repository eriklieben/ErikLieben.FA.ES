using System.Net;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.S3.Model;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Provides an S3-backed store for associating tags with event streams.
/// Tags are stored by tag name at <c>tags/stream-by-tag/{tag}.json</c>, containing a list of stream identifiers.
/// </summary>
public partial class S3StreamTagStore : IDocumentTagStore
{
    private readonly IS3ClientFactory clientFactory;
    private readonly string defaultStoreName;
    private readonly EventStreamS3Settings s3Settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3StreamTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
    /// <param name="defaultStoreName">The default store name used when building S3 clients.</param>
    /// <param name="s3Settings">The S3 storage settings.</param>
    public S3StreamTagStore(
        IS3ClientFactory clientFactory,
        string defaultStoreName,
        EventStreamS3Settings s3Settings)
    {
        this.clientFactory = clientFactory;
        this.defaultStoreName = defaultStoreName;
        this.s3Settings = s3Settings;
    }

    /// <summary>
    /// Associates the specified tag with the stream of the given document.
    /// </summary>
    /// <param name="document">The document whose stream is tagged.</param>
    /// <param name="tag">The tag value to associate.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/stream-by-tag/{filename}.json";
        var bucketName = document.ObjectName.ToLowerInvariant();
        var client = CreateS3Client(document);

        if (s3Settings.AutoCreateBucket)
        {
            await client.EnsureBucketAsync(bucketName);
        }

        var exists = await client.ObjectExistsAsync(bucketName, key);

        if (!exists)
        {
            var newDoc = new S3DocumentTagStoreDocument
            {
                Tag = tag,
                ObjectIds = [document.Active.StreamIdentifier]
            };

            try
            {
                await client.PutObjectAsEntityAsync(
                    bucketName,
                    key,
                    newDoc,
                    S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Object was created between existence check and put call
                // Fall through to update logic below
            }
        }

        var (doc, _, etag) = await client.GetObjectAsEntityAsync(
            bucketName,
            key,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);

        if (doc == null)
        {
            throw new InvalidOperationException(
                $"Unable to find tag document '{bucketName}/{key}' while processing save.");
        }

        if (doc.ObjectIds.All(d => d != document.Active.StreamIdentifier))
        {
            doc.ObjectIds.Add(document.Active.StreamIdentifier);
        }

        await client.PutObjectAsEntityAsync(
            bucketName,
            key,
            doc,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
    }

    /// <summary>
    /// Gets the identifiers of streams that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (bucket scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of stream identifiers; empty when the tag document does not exist.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/stream-by-tag/{filename}.json";
        var bucketName = objectName.ToLowerInvariant();

        var client = clientFactory.CreateClient(defaultStoreName);

        var (doc, _, _) = await client.GetObjectAsEntityAsync(
            bucketName,
            key,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);

        if (doc == null)
        {
            return [];
        }

        return doc.ObjectIds;
    }

    /// <summary>
    /// Removes the specified tag from the stream of the given document.
    /// </summary>
    /// <param name="document">The document whose stream tag should be removed.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A task that represents the asynchronous removal operation.</returns>
    public async Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/stream-by-tag/{filename}.json";
        var bucketName = document.ObjectName.ToLowerInvariant();
        var client = CreateS3Client(document);

        var (doc, _, _) = await client.GetObjectAsEntityAsync(
            bucketName,
            key,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);

        if (doc == null)
        {
            return;
        }

        doc.ObjectIds.Remove(document.Active.StreamIdentifier);

        if (doc.ObjectIds.Count == 0)
        {
            // No more streams with this tag, delete the object
            try
            {
                await client.DeleteObjectAsync(bucketName, key);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Already deleted, nothing to do
            }
        }
        else
        {
            // Update the object with the remaining streams
            await client.PutObjectAsEntityAsync(
                bucketName,
                key,
                doc,
                S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
        }
    }

    /// <summary>
    /// Creates an <see cref="IAmazonS3"/> client for the given document's data store configuration.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the connection name.</param>
    /// <returns>An <see cref="IAmazonS3"/> client configured for stream tag operations.</returns>
    private IAmazonS3 CreateS3Client(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

        var storeName = !string.IsNullOrWhiteSpace(objectDocument.Active.DataStore)
            ? objectDocument.Active.DataStore
            : defaultStoreName;

        return clientFactory.CreateClient(storeName);
    }

    [GeneratedRegex(@"[\\\/*?<>|""\r\n]")]
    private static partial Regex ValidS3KeyRegex();
}
