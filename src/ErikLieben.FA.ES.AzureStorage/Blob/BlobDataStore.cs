using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ErikLieben.FA.ES.AzureStorage.Blob.Extensions;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.Azure;
using BlobDataStoreDocumentContext = ErikLieben.FA.ES.AzureStorage.Blob.Model.BlobDataStoreDocumentContext;

namespace ErikLieben.FA.ES.AzureStorage.Blob;

/// <summary>
/// Provides an Azure Blob Storage-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// </summary>
public class BlobDataStore : IDataStore, IDataStoreRecovery
{
    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.AzureStorage");

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobDataStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory used to create <see cref="BlobServiceClient"/> instances.</param>
    /// <param name="autoCreateContainer">A value indicating whether the target blob container is created automatically when missing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clientFactory"/> is null.</exception>
    public BlobDataStore(IAzureClientFactory<BlobServiceClient> clientFactory, bool autoCreateContainer)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
    }

    /// <summary>
    /// Reads events for the specified document from Azure Blob Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier to read from when chunking is enabled; null when not chunked.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    /// <exception cref="BlobDocumentStoreContainerNotFoundException">Thrown when the configured container does not exist.</exception>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.ReadAsync");

        string? documentPath = null;
        if (document.Active.ChunkingEnabled())
        {
            documentPath = $"{document.Active.StreamIdentifier}-{chunk:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }
        var blob = CreateBlobClient(document, documentPath);



        BlobDataStoreDocument? dataDocument = null;
        try
        {
            dataDocument = (await blob.AsEntityAsync(BlobDataStoreDocumentContext.Default.BlobDataStoreDocument)).Item1;
            if (dataDocument == null)
            {
                return null;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            throw new BlobDocumentStoreContainerNotFoundException(
                $"The container by the name '{document.ObjectName?.ToLowerInvariant()}' is not found. " +
                "Create a container in your deployment or create it by hand, it's not created for you.",
            ex);
        }
        return dataDocument.Events
                    .Where(e => e.EventVersion >= startVersion && (!untilVersion.HasValue || e.EventVersion <= untilVersion))
                    .ToList();
    }

    /// <summary>
    /// Reads events for the specified document as a streaming async enumerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method yields events one at a time after loading the blob document.
    /// While blob storage requires downloading the entire document upfront (unlike CosmosDB/Table),
    /// streaming the results allows consumers to process events incrementally and supports
    /// early cancellation without holding references to the full list.
    /// </para>
    /// <para>
    /// For very large event streams, consider using CosmosDB or Table storage which support
    /// true server-side pagination.
    /// </para>
    /// </remarks>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier to read from when chunking is enabled; null when not chunked.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the streaming operation.</param>
    /// <returns>An async enumerable of events ordered by version.</returns>
    public async IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.ReadAsStreamAsync");

        string? documentPath;
        if (document.Active.ChunkingEnabled())
        {
            documentPath = $"{document.Active.StreamIdentifier}-{chunk:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }

        var blob = CreateBlobClient(document, documentPath);

        BlobDataStoreDocument? dataDocument;
        try
        {
            dataDocument = (await blob.AsEntityAsync(BlobDataStoreDocumentContext.Default.BlobDataStoreDocument)).Item1;
            if (dataDocument == null)
            {
                yield break;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
        {
            yield break;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            yield break;
        }

        // Yield events one at a time to allow early cancellation and reduce memory pressure
        foreach (var evt in dataDocument.Events
            .Where(e => e.EventVersion >= startVersion && (!untilVersion.HasValue || e.EventVersion <= untilVersion)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Blob Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or its stream identifier is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no events are provided.</exception>
    /// <exception cref="BlobDataStoreProcessingException">Thrown when optimistic concurrency or persistence operations fail.</exception>
    public Task AppendAsync(IObjectDocument document, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, events);

    /// <summary>
    /// Appends the specified events to the event stream of the given document in Azure Blob Storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from BlobJsonEvent sources (useful for migrations).</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or its stream identifier is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no events are provided.</exception>
    /// <exception cref="BlobDataStoreProcessingException">Thrown when optimistic concurrency or persistence operations fail.</exception>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.AppendAsync");
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var blobDoc = BlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(blobDoc);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        string? documentPath = null;
        if (document.Active.ChunkingEnabled())
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            documentPath = $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }

        var blob = CreateBlobClient(document, documentPath);

        if (!await blob.ExistsAsync())
        {
            var newDoc = new BlobDataStoreDocument {
                ObjectId = document.ObjectId,
                ObjectName = document.ObjectName,
                LastObjectDocumentHash = blobDoc.Hash ?? "*"
            };
            newDoc.Events.AddRange(events.Select(e => BlobJsonEvent.From(e, preserveTimestamp))!);
            newDoc.LastObjectDocumentHash = blobDoc.Hash ?? "*";

            try
            {
                await blob.SaveEntityAsync(
                    newDoc,
                    BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
                    new BlobRequestConditions { IfNoneMatch = new ETag("*") });
            }
            catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == "ContainerNotFound")
            {
                var containerName = blob.BlobContainerName;
                throw new BlobDocumentStoreContainerNotFoundException(
                    $"The container by the name '{containerName}' is not found. " +
                    $"Please create it or adjust the config setting: 'EventStream:Blob:DefaultDocumentContainerName'",
                ex);
            }
            return;
        }

        var properties = await blob.GetPropertiesAsync();
        var etag = properties.Value.ETag;

        // Download the document with the same tag, so that we're sure it's not overriden in the meantime
        var doc = (await blob.AsEntityAsync(
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag })).Item1
            ?? throw new BlobDataStoreProcessingException($"Unable to find document '{document.ObjectName.ToLowerInvariant()}/{documentPath}' while processing save.");

        // Check if the stream is closed - if the last event is EventStream.Closed, reject new events
        if (doc.Events.Count > 0)
        {
            var lastEvent = doc.Events[^1];
            if (lastEvent.EventType == "EventStream.Closed")
            {
                throw new EventStreamClosedException(
                    document.Active.StreamIdentifier,
                    $"Cannot append events to closed stream '{document.Active.StreamIdentifier}'. " +
                    $"The stream was closed and may have a continuation stream. Please retry on the active stream.");
            }
        }

        if (doc.LastObjectDocumentHash != "*" && doc.LastObjectDocumentHash != blobDoc.PrevHash)
        {
            throw new BlobDataStoreProcessingException($"Optimistic concurrency check failed: document hash mismatch for '{document.ObjectName.ToLowerInvariant()}/{documentPath}'.");
        }
        doc.LastObjectDocumentHash = blobDoc.Hash ?? "*";

        doc.Events.AddRange(events.Select(e => BlobJsonEvent.From(e, preserveTimestamp))!);
        await blob.SaveEntityAsync(doc,
            BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
            new BlobRequestConditions { IfMatch = etag });
    }

    private BlobClient CreateBlobClient(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        // Use DataStore, falling back to deprecated StreamConnectionName for backwards compatibility
#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DataStore)
            ? objectDocument.Active.DataStore
            : objectDocument.Active.StreamConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        if (autoCreateContainer)
        {
            container.CreateIfNotExists();
        }

        var blob = container.GetBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create blobClient.");
        return blob!;
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        using var activity = ActivitySource.StartActivity("BlobDataStore.RemoveEventsForFailedCommitAsync");

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        // Determine blob path based on chunking
        string documentPath;
        if (document.Active.ChunkingEnabled() && document.Active.StreamChunks.Count > 0)
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            documentPath = $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }

        var blob = CreateBlobClient(document, documentPath);

        try
        {
            // Get current blob with ETag for optimistic concurrency
            var properties = await blob.GetPropertiesAsync();
            var etag = properties.Value.ETag;

            var doc = (await blob.AsEntityAsync(
                BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
                new BlobRequestConditions { IfMatch = etag })).Item1;

            if (doc?.Events == null || doc.Events.Count == 0)
            {
                return 0;
            }

            var originalCount = doc.Events.Count;

            // Filter out events in the failed version range
            doc.Events = doc.Events
                .Where(e => e.EventVersion < fromVersion || e.EventVersion > toVersion)
                .ToList();

            var removed = originalCount - doc.Events.Count;

            if (removed > 0)
            {
                // Rewrite blob with filtered events
                await blob.SaveEntityAsync(
                    doc,
                    BlobDataStoreDocumentContext.Default.BlobDataStoreDocument,
                    new BlobRequestConditions { IfMatch = etag });
            }

            activity?.SetTag("RemovedCount", removed);
            return removed;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob doesn't exist - nothing to clean up
            return 0;
        }
    }
}
