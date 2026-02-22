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
/// Provides an S3-backed implementation of <see cref="IDocumentTagStore"/> for associating and querying document tags.
/// </summary>
public partial class S3DocumentTagStore : IDocumentTagStore
{
    private readonly IS3ClientFactory clientFactory;
    private readonly string defaultStoreName;
    private readonly EventStreamS3Settings s3Settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3DocumentTagStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
    /// <param name="defaultDocumentTagType">The default tag provider type (e.g., "s3").</param>
    /// <param name="defaultStoreName">The default store name used when building S3 clients.</param>
    /// <param name="s3Settings">The S3 storage settings.</param>
    public S3DocumentTagStore(
        IS3ClientFactory clientFactory,
        string defaultDocumentTagType,
        string defaultStoreName,
        EventStreamS3Settings s3Settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.defaultStoreName = defaultStoreName;
        this.s3Settings = s3Settings;
    }

    /// <summary>
    /// Associates the specified tag with the given document by storing a tag document in S3.
    /// </summary>
    /// <param name="document">The document to tag.</param>
    /// <param name="tag">The tag value to associate with the document.</param>
    /// <returns>A task that represents the asynchronous tagging operation.</returns>
    public async Task SetAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Active.StreamIdentifier);
        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/document/{filename}.json";
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
                ObjectIds = [document.ObjectId]
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

        var (doc, _, _) = await client.GetObjectAsEntityAsync(
            bucketName,
            key,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);

        if (doc == null)
        {
            throw new InvalidOperationException(
                $"Unable to find tag document '{bucketName}/{key}' while processing save.");
        }

        if (doc.ObjectIds.All(d => d != document.ObjectId))
        {
            doc.ObjectIds.Add(document.ObjectId);
        }

        await client.PutObjectAsEntityAsync(
            bucketName,
            key,
            doc,
            S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
    }

    /// <summary>
    /// Gets the identifiers of documents that have the specified tag within the given object scope.
    /// </summary>
    /// <param name="objectName">The object name (bucket scope) to search within.</param>
    /// <param name="tag">The tag value to match.</param>
    /// <returns>An enumerable of document identifiers; empty when the tag document does not exist.</returns>
    public async Task<IEnumerable<string>> GetAsync(string objectName, string tag)
    {
        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/document/{filename}.json";
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
    /// Removes the specified tag from the given document by updating or deleting the tag document in S3.
    /// </summary>
    /// <param name="document">The document to remove the tag from.</param>
    /// <param name="tag">The tag value to remove.</param>
    /// <returns>A task that represents the asynchronous removal operation.</returns>
    public async Task RemoveAsync(IObjectDocument document, string tag)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var filename = ValidS3KeyRegex().Replace(tag.ToLowerInvariant(), string.Empty);
        var key = $"tags/document/{filename}.json";
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

        doc.ObjectIds.Remove(document.ObjectId);

        if (doc.ObjectIds.Count == 0)
        {
            // No more documents with this tag, delete the object
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
            // Update the object with the remaining documents
            await client.PutObjectAsEntityAsync(
                bucketName,
                key,
                doc,
                S3DocumentTagStoreDocumentContext.Default.S3DocumentTagStoreDocument);
        }
    }

    /// <summary>
    /// Creates an <see cref="IAmazonS3"/> client for the given document's tag store configuration.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the connection name.</param>
    /// <returns>An <see cref="IAmazonS3"/> client configured for tag operations.</returns>
    private IAmazonS3 CreateS3Client(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(objectDocument.ObjectName);

        var storeName = !string.IsNullOrWhiteSpace(objectDocument.Active.DocumentTagStore)
            ? objectDocument.Active.DocumentTagStore
            : defaultStoreName;

        return clientFactory.CreateClient(storeName);
    }

    [GeneratedRegex(@"[\\\/*?<>|""\r\n]")]
    private static partial Regex ValidS3KeyRegex();
}
