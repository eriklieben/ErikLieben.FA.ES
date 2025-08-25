using ErikLieben.FA.ES.Processors;
using System.Text.Json.Serialization.Metadata;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobSnapShotStore(
    IAzureClientFactory<BlobServiceClient> clientFactory,
    EventStreamBlobSettings settings)
    : ISnapShotStore
{
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
