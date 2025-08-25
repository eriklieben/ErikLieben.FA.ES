// using System.Text;
// using Azure.Storage.Blobs;
// using ErikLieben.FA.ES.AzureStorage.Blob.Model;
// using ErikLieben.FA.ES.Projections;
// using Microsoft.Extensions.Azure;
//
// namespace ErikLieben.FA.ES.AzureStorage.Blob;
//
//
// public abstract class BlobProjectionFactory<T> where T : Projection {
//
//     private readonly BlobContainerClient containerClient;
//
//     protected abstract bool HasExternalVersionIndex { get; }
//
//     protected BlobProjectionFactory(
//         IAzureClientFactory<BlobServiceClient> blobServiceClientFactory,
//         string connection,
//         string containerPath)
//     {
//         var blobServiceClient = string.IsNullOrWhiteSpace(connection)
//             ? blobServiceClientFactory.CreateClient("Default")
//             : blobServiceClientFactory.CreateClient(connection);
//
//         containerClient = blobServiceClient.GetBlobContainerClient(containerPath);
//     }
//
//     public async Task<T> Get(string filePath)
//     {
//         var blobClient = containerClient.GetBlobClient(filePath);
//
//         var projection = New();
//         if (await blobClient.ExistsAsync())
//         {
//             var content = await blobClient.DownloadContentAsync();
//             var jsonString = content.Value.Content.ToString();
//             projection.LoadFromJson(jsonString);
//         }
//
//         if (!HasExternalVersionIndex)
//         {
//             return projection;
//         }
//
//         var rootFilePath = filePath[..filePath.LastIndexOf('/')];
//         var fileName = filePath.Substring(filePath.LastIndexOf('/') + 1);
//
//         // Get version index as well
//         var lastVersionIndexFilePath = $"{rootFilePath}/{fileName}-versionIndexes/{projection.CheckpointFingerprint}.json";
//         var versionIndexBlobClient = containerClient.GetBlobClient(lastVersionIndexFilePath);
//         if (!(await versionIndexBlobClient.ExistsAsync()))
//         {
//             return projection;
//         }
//
//         var versionIndexContent = await versionIndexBlobClient.DownloadContentAsync();
//         var versionIndexJsonString = versionIndexContent.Value.Content.ToString();
//         var idx = BlobVersionIndexDocument.FromJson(versionIndexJsonString);
//         projection.Checkpoint = idx.VersionIndex;
//         return projection;
//     }
//
//     public async Task Set(T projection, string filePath)
//     {
//         ArgumentNullException.ThrowIfNull(projection);
//
//         var jsonString = projection.ToJson();
//
//         if (HasExternalVersionIndex)
//         {
//             var versionIndexDoc = BlobVersionIndexDocument.ToJson(projection.Checkpoint);
//             if (!string.IsNullOrWhiteSpace(versionIndexDoc))
//             {
//                 var rootFilePath = filePath[..filePath.LastIndexOf('/')];
//
//                 var fileName = filePath.Substring(filePath.LastIndexOf('/') + 1);
//                 var lastVersionIndexFilePath = $"{rootFilePath}/{fileName}-versionIndexes/{projection.CheckpointFingerprint}.json";
//                 var versionIndexBlobClient = containerClient.GetBlobClient(lastVersionIndexFilePath);
//                 using var versionIndexStream = new MemoryStream(Encoding.UTF8.GetBytes(versionIndexDoc));
//                 await versionIndexBlobClient.UploadAsync(versionIndexStream, overwrite: true);
//             }
//         }
//
//         var blobClient = containerClient.GetBlobClient(filePath);
//         using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
//         await blobClient.UploadAsync(stream, overwrite: true);
//     }
//
//     protected abstract T New();
// }
