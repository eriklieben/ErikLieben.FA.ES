using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Indicates that a projection can be stored as JSON in Azure Blob Storage and specifies its blob path and optional connection name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class BlobJsonProjectionAttribute(string path) : Attribute
{
    /// <summary>
    /// Gets the blob path used to store or retrieve the projection JSON.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets or sets the Azure client connection name used to resolve the <see cref="Azure.Storage.Blobs.BlobServiceClient"/>.
    /// </summary>
    public string? Connection { get; init; }
}
