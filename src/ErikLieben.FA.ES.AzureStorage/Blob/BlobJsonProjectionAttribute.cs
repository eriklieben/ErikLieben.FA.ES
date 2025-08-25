using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

public class BlobJsonProjectionAttribute(string Path) : Attribute
{
    public string? Connection { get; init; }
}

// public interface IBlobJsonProjectionFactory<T> where T : class
// {
//     public T? Get(string container, string path, string connectionName);
//
//     public Task Set(T projection, string container, string path, string connectionName = "");
// }
//
// public abstract class BlobJsonProjectionFactory<T>(IAzureClientFactory<BlobServiceClient> clientFactory)
//     : IBlobJsonProjectionFactory<T>
//     where T : class
// {
//     protected abstract T DeSerialize(Stream stream);
//
//     protected abstract void Serialize(T projection, Stream stream);
//
//     public T Get(string container, string path, string connectionName = "")
//     {
//         // Create a client connection using connectionName, if provided
//         var client = clientFactory.CreateClient(connectionName);
//         var containerClient = client.GetBlobContainerClient(container);
//         var blockBlobClient = containerClient.GetBlockBlobClient(path);
//
//         using var stream = blockBlobClient.OpenRead();
//         return stream == null ? null! : DeSerialize(stream);
//     }
//
//     public async Task Set(T projection, string container, string path, string connectionName = "")
//     {
//         var client = clientFactory.CreateClient(connectionName);
//         var containerClient = client.GetBlobContainerClient(container);
//         var blockBlobClient = containerClient.GetBlockBlobClient(path);
//
//         using var stream = new MemoryStream();
//         Serialize(projection, stream);
//         stream.Position = 0;
//
//         await blockBlobClient.UploadAsync(stream, new BlobUploadOptions
//         {
//            Conditions = null
//         });
//     }
// }
