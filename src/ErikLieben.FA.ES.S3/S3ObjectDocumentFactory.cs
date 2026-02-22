using System.Diagnostics;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.S3.Configuration;

namespace ErikLieben.FA.ES.S3;

#pragma warning disable CS8602 // Dereference of possibly null reference - s3DocumentStore is always initialized in constructors
/// <summary>
/// Provides an S3-compatible storage-backed implementation of <see cref="IObjectDocumentFactory"/>.
/// </summary>
public class S3ObjectDocumentFactory : IObjectDocumentFactory
{
    private readonly IS3DocumentStore s3DocumentStore;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.S3");

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ObjectDocumentFactory"/> class using S3 services and settings.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create S3 client instances.</param>
    /// <param name="documentTagStore">The factory used to access document tag storage.</param>
    /// <param name="settings">The default event stream type settings.</param>
    /// <param name="s3Settings">The S3 storage settings used for buckets and chunking.</param>
    public S3ObjectDocumentFactory(
        IS3ClientFactory clientFactory,
        IDocumentTagDocumentFactory documentTagStore,
        EventStreamDefaultTypeSettings settings,
        EventStreamS3Settings s3Settings)
    {
        this.s3DocumentStore = new S3DocumentStore(clientFactory, documentTagStore, s3Settings, settings);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ObjectDocumentFactory"/> class using a pre-configured <see cref="IS3DocumentStore"/>.
    /// </summary>
    /// <param name="s3DocumentStore">The S3 document store to delegate operations to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s3DocumentStore"/> is null.</exception>
    public S3ObjectDocumentFactory(IS3DocumentStore s3DocumentStore)
    {
        ArgumentNullException.ThrowIfNull(s3DocumentStore);
        this.s3DocumentStore = s3DocumentStore;
    }

    /// <summary>
    /// Retrieves an object document or creates a new one when it does not exist.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the key prefix.</param>
    /// <param name="objectId">The identifier of the object to retrieve or create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for S3ObjectDocumentFactory (already S3-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The existing or newly created <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetOrCreateAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(GetOrCreateAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var objectNameLower = objectName.ToLowerInvariant();
        var result = await this.s3DocumentStore.CreateAsync(objectNameLower, objectId, store);
        if (result is null)
        {
            throw new InvalidOperationException("S3DocumentStore.CreateAsync returned null document.");
        }
        return result;
    }

    /// <summary>
    /// Retrieves an existing object document from S3-compatible storage.
    /// </summary>
    /// <param name="objectName">The object type/name used to determine the key prefix.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <param name="documentType">Ignored for S3ObjectDocumentFactory (already S3-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded <see cref="IObjectDocument"/>.</returns>
    public async Task<IObjectDocument> GetAsync(string objectName, string objectId, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(GetAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var result = await this.s3DocumentStore.GetAsync(objectName.ToLowerInvariant(), objectId, store);
        if (result is null)
        {
            throw new InvalidOperationException("S3DocumentStore.GetAsync returned null document.");
        }
        return result;
    }

    /// <summary>
    /// Retrieves all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByDocumentTagAsync(string objectName, string objectDocumentTag)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(GetByDocumentTagAsync)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDocumentTag);
        return (await s3DocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag))
               ?? [];
    }

    /// <summary>
    /// Gets the first object document that has the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the document. If not provided, uses the default document store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The first matching document or null when none is found.</returns>
    public Task<IObjectDocument?> GetFirstByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(GetFirstByObjectDocumentTag)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDocumentTag);
        return s3DocumentStore.GetFirstByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store);
    }

    /// <summary>
    /// Gets all object documents that have the specified document tag.
    /// </summary>
    /// <param name="objectName">The object name (scope) to search within.</param>
    /// <param name="objectDocumentTag">The document tag value to match.</param>
    /// <param name="documentTagStore">Optional document tag store name. If not provided, uses the default document tag store.</param>
    /// <param name="store">Optional store name for loading the documents. If not provided, uses the default document store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An enumerable of matching documents; empty when none found.</returns>
    public async Task<IEnumerable<IObjectDocument>> GetByObjectDocumentTag(string objectName, string objectDocumentTag, string? documentTagStore = null, string? store = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(GetByObjectDocumentTag)}");
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDocumentTag);
        return (await s3DocumentStore.GetByDocumentByTagAsync(objectName, objectDocumentTag, documentTagStore, store))
               ?? [];
    }

    /// <summary>
    /// Persists the provided object document to S3-compatible storage.
    /// </summary>
    /// <param name="document">The object document to save.</param>
    /// <param name="store">Unused in this implementation.</param>
    /// <param name="documentType">Ignored for S3ObjectDocumentFactory (already S3-specific).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public Task SetAsync(IObjectDocument document, string? store = null, string? documentType = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"S3ObjectDocumentFactory.{nameof(SetAsync)}");
        ArgumentNullException.ThrowIfNull(document);
        return s3DocumentStore.SetAsync(document);
    }
}
