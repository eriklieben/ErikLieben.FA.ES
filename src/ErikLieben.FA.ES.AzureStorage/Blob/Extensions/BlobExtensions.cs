using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.AzureStorage.Blob.Extensions;

/// <summary>
/// Provides extension methods for <see cref="BlobClient"/> to serialize/deserialize JSON entities and handle uploads with metadata/tags.
/// </summary>
public static class BlobExtensions
{
    /// <summary>
    /// Downloads the blob content and deserializes it to a typed document using the specified source-generated JSON type info.
    /// </summary>
    /// <typeparam name="Document">The target document type.</typeparam>
    /// <param name="blobClient">The blob client that points to the JSON document.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type information for <typeparamref name="Document"/>.</param>
    /// <param name="requestOptions">Optional conditional headers for the request; may be null.</param>
    /// <returns>A tuple containing the deserialized document (or null when not found) and a SHA-256 hash of the JSON.</returns>
    public static async Task<(Document?, string?)> AsEntityAsync<Document>(
        this BlobClient blobClient,
        JsonTypeInfo<Document> jsonTypeInfo,
        BlobRequestConditions? requestOptions = null) where Document : class
    {
        try
        {
            using MemoryStream s = new();
            await blobClient.DownloadToAsync(s, requestOptions);
            var json = Encoding.UTF8.GetString(s.GetBuffer(), 0, (int)s.Length);
            return (JsonSerializer.Deserialize(json, jsonTypeInfo), ComputeSha256Hash(json));
        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == BlobErrorCode.BlobNotFound || ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            return (null,null);
        }
    }

    /// <summary>
    /// Downloads the blob content and deserializes it to an object using the specified source-generated JSON type info.
    /// </summary>
    /// <param name="blobClient">The blob client that points to the JSON document.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type information that describes the runtime type.</param>
    /// <param name="requestOptions">Optional conditional headers for the request; may be null.</param>
    /// <returns>The deserialized object instance, or null when the blob does not exist.</returns>
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

    /// <summary>
    /// Downloads the blob content as a UTF-8 string.
    /// </summary>
    /// <param name="blobJson">The blob client that points to the JSON document.</param>
    /// <param name="requestOptions">Optional conditional headers for the request; may be null.</param>
    /// <returns>The blob content as a string, or null when the blob does not exist.</returns>
    public static async Task<string?> AsString(
        this BlobClient blobJson,
        BlobRequestConditions? requestOptions = null)
    {
        try
        {
            using MemoryStream s = new();
            await blobJson.DownloadToAsync(s, requestOptions);
            return Encoding.UTF8.GetString(s.GetBuffer(), 0, (int)s.Length);

        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes an object using the given type info and uploads it to the blob with optional conditions, metadata and tags.
    /// </summary>
    /// <param name="blobJson">The blob client pointing to the destination blob.</param>
    /// <param name="object">The object instance to serialize and upload.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info used for serialization.</param>
    /// <param name="requestOptions">Optional request conditions for optimistic concurrency; may be null.</param>
    /// <param name="metadata">Optional blob metadata dictionary; may be null.</param>
    /// <param name="tags">Optional blob tags dictionary; may be null.</param>
    /// <returns>The ETag value returned by the upload operation.</returns>
    public static async Task<string> Save(
        this BlobClient blobJson,
        object @object,
        JsonTypeInfo jsonTypeInfo,
        BlobRequestConditions requestOptions = null!,
        Dictionary<string, string> metadata = null!,
        Dictionary<string, string> tags = null!)
    {
        var serialized = JsonSerializer.Serialize(@object, jsonTypeInfo);
        var bytes = Encoding.UTF8.GetBytes(serialized);

        try
        {
            var info = await blobJson.UploadAsync(
                new MemoryStream(bytes),
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
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // Extract container and blob information for better error messaging
            var containerName = blobJson.BlobContainerName;
            var blobName = blobJson.Name;
            var accountName = blobJson.AccountName;
            var uri = blobJson.Uri;

            throw new RequestFailedException(
                ex.Status,
                $"Container '{containerName}' not found when trying to save blob '{blobName}'. " +
                $"Storage Account: {accountName}, URI: {uri}. " +
                $"Ensure the container exists or enable autoCreateContainer in your storage configuration. " +
                $"Original error: {ex.Message}",
                ex.ErrorCode,
                ex);
        }
    }

    /// <summary>
    /// Serializes an entity using the given type info, uploads it, and returns the resulting ETag and content hash.
    /// </summary>
    /// <typeparam name="Document">The entity type.</typeparam>
    /// <param name="blobClient">The blob client pointing to the destination blob.</param>
    /// <param name="entity">The entity instance to serialize and upload.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info used for serialization.</param>
    /// <param name="requestOptions">Optional request conditions for optimistic concurrency; may be null.</param>
    /// <param name="metadata">Optional blob metadata dictionary; may be null.</param>
    /// <param name="tags">Optional blob tags dictionary; may be null.</param>
    /// <returns>A tuple with the ETag and the SHA-256 hash of the serialized content.</returns>
    public static async Task<(string, string)> SaveEntityAsync<Document>(
        this BlobClient blobClient,
        Document entity,
        JsonTypeInfo<Document> jsonTypeInfo,
        BlobRequestConditions requestOptions = null!,
        Dictionary<string, string> metadata = null!,
        Dictionary<string, string> tags = null!) where Document : class
    {
        var serialized = JsonSerializer.Serialize(entity, jsonTypeInfo);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        var hash = ComputeSha256Hash(bytes, 0, bytes.Length);

        try
        {
            var info = await blobClient.UploadAsync(
                new MemoryStream(bytes),
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
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // Extract container and blob information for better error messaging
            var containerName = blobClient.BlobContainerName;
            var blobName = blobClient.Name;
            var accountName = blobClient.AccountName;
            var uri = blobClient.Uri;

            throw new RequestFailedException(
                ex.Status,
                $"Container '{containerName}' not found when trying to save blob '{blobName}'. " +
                $"Storage Account: {accountName}, URI: {uri}. " +
                $"Ensure the container exists or enable autoCreateContainer in your storage configuration. " +
                $"Original error: {ex.Message}",
                ex.ErrorCode,
                ex);
        }
    }

    /// <summary>
    /// Computes the hexadecimal SHA-256 hash for the specified text using UTF-8 encoding.
    /// </summary>
    /// <param name="rawData">The input text to hash.</param>
    /// <returns>The lowercase hexadecimal SHA-256 string.</returns>
    private static string ComputeSha256Hash(string rawData)
    {
        var inputBytes = Encoding.UTF8.GetBytes(rawData);
        return ComputeSha256Hash(inputBytes, 0, inputBytes.Length);
    }

    /// <summary>
    /// Computes the hexadecimal SHA-256 hash for the specified byte array.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <param name="offset">The offset in the array.</param>
    /// <param name="count">The number of bytes to hash.</param>
    /// <returns>The lowercase hexadecimal SHA-256 string.</returns>
    private static string ComputeSha256Hash(byte[] data, int offset, int count)
    {
        ReadOnlySpan<byte> dataSpan = data.AsSpan(offset, count);
        var bytes = SHA256.HashData(dataSpan);
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i].TryFormat(chars.Slice(i * 2, 2), out _, "x2");
        }
        return new string(chars);
    }
}
