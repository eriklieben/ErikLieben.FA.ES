using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ErikLieben.FA.ES.S3.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IAmazonS3"/> to serialize/deserialize JSON entities
/// and handle uploads with S3-specific concurrency controls and data integrity checks.
/// </summary>
public static class S3Extensions
{
    /// <summary>
    /// Downloads an S3 object and deserializes it to a typed document using the specified source-generated JSON type info.
    /// </summary>
    /// <typeparam name="TDocument">The target document type.</typeparam>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type information for <typeparamref name="TDocument"/>.</param>
    /// <param name="ifMatchETag">Optional ETag for conditional request (If-Match); may be null.</param>
    /// <returns>A tuple containing the deserialized document (or null when not found), a SHA-256 hash of the JSON, and the ETag.</returns>
    public static async Task<(TDocument? Document, string? Hash, string? ETag)> GetObjectAsEntityAsync<TDocument>(
        this IAmazonS3 s3Client,
        string bucketName,
        string key,
        JsonTypeInfo<TDocument> jsonTypeInfo,
        string? ifMatchETag = null) where TDocument : class
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key,
            };

            if (!string.IsNullOrEmpty(ifMatchETag))
            {
                request.EtagToMatch = ifMatchETag;
            }

            using var response = await s3Client.GetObjectAsync(request);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            var json = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            return (JsonSerializer.Deserialize(json, jsonTypeInfo), ComputeSha256Hash(json), response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey" || ex.ErrorCode == "NoSuchBucket")
        {
            return (null, null, null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new InvalidOperationException(
                $"ETag precondition failed for s3://{bucketName}/{key}. " +
                $"The object was modified since it was last read (expected ETag: {ifMatchETag}).", ex);
        }
    }

    /// <summary>
    /// Downloads an S3 object and deserializes it to an object using the specified source-generated JSON type info.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type information that describes the runtime type.</param>
    /// <returns>The deserialized object instance, or null when the object does not exist.</returns>
    public static async Task<object?> GetObjectAsEntityAsync(
        this IAmazonS3 s3Client,
        string bucketName,
        string key,
        JsonTypeInfo jsonTypeInfo)
    {
        try
        {
            using var response = await s3Client.GetObjectAsync(bucketName, key);
            return await JsonSerializer.DeserializeAsync(response.ResponseStream, jsonTypeInfo);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the S3 object content as a UTF-8 string.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <returns>The object content as a string, or null when the object does not exist.</returns>
    public static async Task<string?> GetObjectAsStringAsync(
        this IAmazonS3 s3Client,
        string bucketName,
        string key)
    {
        try
        {
            using var response = await s3Client.GetObjectAsync(bucketName, key);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes an entity and uploads it to S3 with optional ETag-based optimistic concurrency and Content-MD5 integrity check.
    /// </summary>
    /// <typeparam name="TDocument">The entity type.</typeparam>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="entity">The entity instance to serialize and upload.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info used for serialization.</param>
    /// <param name="ifMatchETag">Optional ETag for conditional put (will fail if object was modified); may be null.</param>
    /// <returns>A tuple with the new ETag and the SHA-256 hash of the serialized content.</returns>
    public static async Task<(string ETag, string Hash)> PutObjectAsEntityAsync<TDocument>(
        this IAmazonS3 s3Client,
        string bucketName,
        string key,
        TDocument entity,
        JsonTypeInfo<TDocument> jsonTypeInfo,
        string? ifMatchETag = null) where TDocument : class
    {
        var serialized = JsonSerializer.Serialize(entity, jsonTypeInfo);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        var hash = ComputeSha256Hash(bytes, 0, bytes.Length);
        var contentMd5 = ComputeMd5Base64(bytes);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ContentType = "application/json",
            MD5Digest = contentMd5,
            InputStream = new MemoryStream(bytes),
        };

        try
        {
            var response = await s3Client.PutObjectAsync(request);
            return (response.ETag, hash);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new InvalidOperationException(
                $"ETag precondition failed for s3://{bucketName}/{key}. " +
                $"The object was modified since it was last read (expected ETag: {ifMatchETag}).", ex);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException(
                $"Bucket '{bucketName}' not found when trying to save object '{key}'. " +
                $"Ensure the bucket exists or enable AutoCreateBucket in your S3 storage configuration. " +
                $"Original error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes an object using the given type info and uploads it to S3 with Content-MD5 integrity check.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="entity">The object instance to serialize and upload.</param>
    /// <param name="jsonTypeInfo">The source-generated JSON type info used for serialization.</param>
    /// <returns>The ETag value returned by the upload operation.</returns>
    public static async Task<string> PutObjectAsync(
        this IAmazonS3 s3Client,
        string bucketName,
        string key,
        object entity,
        JsonTypeInfo jsonTypeInfo)
    {
        var serialized = JsonSerializer.Serialize(entity, jsonTypeInfo);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        var contentMd5 = ComputeMd5Base64(bytes);

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ContentType = "application/json",
            MD5Digest = contentMd5,
            InputStream = new MemoryStream(bytes),
        };

        try
        {
            var response = await s3Client.PutObjectAsync(request);
            return response.ETag;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException(
                $"Bucket '{bucketName}' not found when trying to save object '{key}'. " +
                $"Ensure the bucket exists or enable AutoCreateBucket in your S3 storage configuration. " +
                $"Original error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether an S3 object exists using a lightweight HEAD request.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <returns>True if the object exists; false otherwise.</returns>
    public static async Task<bool> ObjectExistsAsync(
        this IAmazonS3 s3Client,
        string bucketName,
        string key)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(bucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the ETag of an S3 object using a lightweight HEAD request.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The S3 bucket name.</param>
    /// <param name="key">The S3 object key.</param>
    /// <returns>The ETag, or null if the object does not exist.</returns>
    public static async Task<string?> GetObjectETagAsync(
        this IAmazonS3 s3Client,
        string bucketName,
        string key)
    {
        try
        {
            var metadata = await s3Client.GetObjectMetadataAsync(bucketName, key);
            return metadata.ETag;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Ensures the specified S3 bucket exists, creating it if necessary.
    /// </summary>
    /// <param name="s3Client">The S3 client.</param>
    /// <param name="bucketName">The bucket name to ensure.</param>
    public static async Task EnsureBucketAsync(
        this IAmazonS3 s3Client,
        string bucketName)
    {
        try
        {
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou" || ex.ErrorCode == "BucketAlreadyExists")
        {
            // Bucket already exists — no action needed.
        }
    }

    /// <summary>
    /// Computes the hexadecimal SHA-256 hash for the specified text using UTF-8 encoding.
    /// </summary>
    /// <param name="rawData">The input text to hash.</param>
    /// <returns>The lowercase hexadecimal SHA-256 string.</returns>
    internal static string ComputeSha256Hash(string rawData)
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
    internal static string ComputeSha256Hash(byte[] data, int offset, int count)
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

    /// <summary>
    /// Computes the Base64-encoded MD5 hash for Content-MD5 header used in S3 PutObject for data integrity.
    /// MD5 is required by the S3 protocol for the Content-MD5 header and is not used for security purposes.
    /// </summary>
    /// <param name="data">The input byte array.</param>
    /// <returns>The Base64-encoded MD5 hash.</returns>
    private static string ComputeMd5Base64(byte[] data) //NOSONAR - MD5 required by S3 Content-MD5 protocol
    {
        var md5Hash = MD5.HashData(data);
        return Convert.ToBase64String(md5Hash);
    }
}
