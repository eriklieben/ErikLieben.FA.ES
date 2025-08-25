using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Extensions;

public static class BlobExtensions
{
    public static async Task<(Document?, string?)> AsEntityAsync<Document>(
        this BlobClient blobClient,
        JsonTypeInfo<Document> jsonTypeInfo,
        BlobRequestConditions? requestOptions = null) where Document : class
    {
        try
        {
            using MemoryStream s = new();
            await blobClient.DownloadToAsync(s, requestOptions);
            var json = Encoding.UTF8.GetString(s.ToArray());
            return (JsonSerializer.Deserialize(json, jsonTypeInfo), ComputeSha256Hash(json));
        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == BlobErrorCode.BlobNotFound || ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            return (null,null);
        }
    }

    public static async Task<object?> AsEntityAsync(
        this BlobClient blobClient,
        JsonTypeInfo jsonTypeInfo,
        BlobRequestConditions? requestOptions = null)
    {
        try
        {
            await using var stream = await blobClient.OpenReadAsync(
                new BlobOpenReadOptions(false) { Conditions = requestOptions });
            return await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo);
        }
        catch (RequestFailedException ex)
        when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
    }

    public static async Task<string?> AsString(
        this BlobClient blobJson,
        BlobRequestConditions? requestOptions = null)
    {
        try
        {
            using MemoryStream s = new();
            await blobJson.DownloadToAsync(s, requestOptions);
            return Encoding.UTF8.GetString(s.ToArray());

        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
    }

    public static async Task<string> Save(
        this BlobClient blobJson,
        object @object,
        JsonTypeInfo jsonTypeInfo,
        BlobRequestConditions requestOptions = null!,
        Dictionary<string, string> metadata = null!,
        Dictionary<string, string> tags = null!)
    {
        var info = await blobJson.UploadAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@object, jsonTypeInfo))),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                },
                Conditions = requestOptions,
                Tags = tags,
                Metadata = metadata
            });

        return info.Value.ETag.ToString();
    }

    public static async Task<(string, string)> SaveEntityAsync<Document>(
        this BlobClient blobClient,
        Document entity,
        JsonTypeInfo<Document> jsonTypeInfo,
        BlobRequestConditions requestOptions = null!,
        Dictionary<string, string> metadata = null!,
        Dictionary<string, string> tags = null!) where Document : class
    {
        var serialized = JsonSerializer.Serialize(entity, jsonTypeInfo);
        var hash = ComputeSha256Hash(serialized);

        var info = await blobClient.UploadAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(serialized)),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                },
                Conditions = requestOptions,
                Tags = tags,
                Metadata = metadata
            });

        return (info.Value.ETag.ToString(), hash);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
