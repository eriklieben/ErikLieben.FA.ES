using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides an Azure Blob Storage-backed implementation of <see cref="ISnapShotStore"/> for persisting and retrieving aggregate snapshots.
/// </summary>
/// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
/// <param name="settings">The Blob settings controlling container creation and defaults.</param>
public class BlobSnapShotStore(
    IAzureClientFactory<BlobServiceClient> clientFactory,
    EventStreamBlobSettings settings)
    : ISnapShotStore
{
    /// <summary>
    /// Persists a snapshot of the aggregate to Blob Storage using the supplied JSON type info.
    /// </summary>
    /// <param name="object">The aggregate instance to snapshot.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info describing the aggregate type.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot is taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task SetAsync(IBase @object, JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null)
    {
        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        await blob.Save(@object, jsonTypeInfo);
    }

    /// <summary>
    /// Retrieves a snapshot of the aggregate at the specified version using the supplied JSON type info.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="jsonTypeInfo">The source-generated JSON type info for <typeparamref name="T"/>.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task<T?> GetAsync<T>(JsonTypeInfo<T> jsonTypeInfo, IObjectDocument document, int version, string? name = null) where T : class, IBase
    {
        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        var (a,_) = await blob.AsEntityAsync(jsonTypeInfo);
        return a;
    }

    /// <summary>
    /// Retrieves a snapshot as an untyped object at the specified version using the supplied JSON type info.
    /// </summary>
    /// <param name="jsonTypeInfo">The source-generated JSON type info representing the runtime type of the snapshot.</param>
    /// <param name="document">The object document whose stream the snapshot belongs to.</param>
    /// <param name="version">The stream version the snapshot was taken at.</param>
    /// <param name="name">An optional name or version discriminator for the snapshot format.</param>
    /// <returns>The deserialized snapshot instance when found; otherwise null.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the snapshot blob client cannot be created.</exception>
    public async Task<object?> GetAsync(JsonTypeInfo jsonTypeInfo, IObjectDocument document, int version, string? name = null)
    {
        var documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}.json";
        if (!string.IsNullOrWhiteSpace(name))
        {
            documentPath = $"snapshot/{document.Active.StreamIdentifier}-{version:d20}_{name}.json";
        }
        var blob = await CreateBlobClient(document, documentPath);
        var x = await blob.AsEntityAsync(jsonTypeInfo);
        return x;
    }

    /// <summary>
    /// Creates a <see cref="BlobClient"/> for the given document and snapshot path, ensuring the container exists when configured.
    /// </summary>
    /// <param name="objectDocument">The object document that provides the container scope and connection name.</param>
    /// <param name="documentPath">The blob path of the snapshot document.</param>
    /// <returns>A <see cref="BlobClient"/> configured for the snapshot path.</returns>
    /// <exception cref="DocumentConfigurationException">Thrown when the blob client cannot be created.</exception>
    private async Task<BlobClient> CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        var client = clientFactory.CreateClient(objectDocument.Active.SnapShotConnectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (settings.AutoCreateContainer)
        {
            await container.CreateIfNotExistsAsync();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }
}
