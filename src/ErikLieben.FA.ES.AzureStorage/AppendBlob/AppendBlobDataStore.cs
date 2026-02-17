using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ErikLieben.FA.ES.AzureStorage.AppendBlob.Model;
using ErikLieben.FA.ES.AzureStorage.Blob.Model;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Azure;

namespace ErikLieben.FA.ES.AzureStorage.AppendBlob;

/// <summary>
/// Provides an Azure Append Blob Storage-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// Uses NDJSON format with commit markers for atomic O(1) appends and 2-phase concurrency control.
/// </summary>
/// <remarks>
/// <para>
/// <b>2-Phase Commit Protocol:</b>
/// Phase 1 updates the object document (with ETag for concurrency). Phase 2 validates the commit marker
/// hash chain and atomically appends events + a new commit marker using <c>IfAppendPositionEqual</c>.
/// </para>
/// <para>
/// <b>Commit Markers:</b> Each batch of appended events is followed by a commit marker line
/// <c>{"$m":"c","h":"...","ph":"...","v":N}</c> that records the document hash chain.
/// On read, marker lines are skipped. On write, the last marker is validated to ensure
/// no other writer's Phase 1 has advanced the document without completing Phase 2.
/// </para>
/// <para>
/// <b>Recovery:</b> Append blobs cannot remove events. <see cref="RemoveEventsForFailedCommitAsync"/>
/// returns 0 and the <see cref="ReadAsync"/> method de-duplicates by keeping the last occurrence of each version,
/// capped at <c>CurrentStreamVersion</c>.
/// </para>
/// </remarks>
public class AppendBlobDataStore : IDataStore, IDataStoreRecovery
{
    private static readonly ConcurrentDictionary<string, bool> VerifiedContainers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> ClosedStreams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Clears the verified containers cache. Primarily intended for testing scenarios.
    /// </summary>
    public static void ClearVerifiedContainersCache()
    {
        VerifiedContainers.Clear();
    }

    /// <summary>
    /// Clears the closed streams cache. Primarily intended for testing scenarios.
    /// </summary>
    public static void ClearClosedStreamCache()
    {
        ClosedStreams.Clear();
    }

    private readonly IAzureClientFactory<BlobServiceClient> clientFactory;
    private readonly bool autoCreateContainer;

    /// <summary>
    /// The block count threshold at which the append blob is considered full.
    /// Set slightly below the Azure hard limit of 50,000 to leave room for the
    /// close event and final commit marker.
    /// </summary>
    /// <summary>
    /// The block count threshold at which an append blob triggers continuation.
    /// </summary>
    public const int BlockCountThreshold = 49_990;

    /// <summary>
    /// The maximum number of bytes to range-read from the tail of the append blob
    /// when searching for the last commit marker.
    /// </summary>
    internal const int TailReadSize = 4096;

    /// <summary>
    /// Minimum blob size in bytes before attempting incremental reads.
    /// Below this threshold, a full download is cheaper than the extra API calls
    /// needed for marker discovery + range read.
    /// </summary>
    internal const int IncrementalReadThreshold = 32_768;

    /// <inheritdoc cref="BlobDataStore(IAzureClientFactory{BlobServiceClient}, bool)"/>
    public AppendBlobDataStore(IAzureClientFactory<BlobServiceClient> clientFactory, bool autoCreateContainer)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);

        this.clientFactory = clientFactory;
        this.autoCreateContainer = autoCreateContainer;
    }

    /// <summary>
    /// Reads events for the specified document from the NDJSON append blob.
    /// De-duplicates by keeping the last occurrence of each version, capped at <c>CurrentStreamVersion</c>.
    /// </summary>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("AppendBlobDataStore.Read");
        var timer = activity != null ? FaesMetrics.StartTimer() : null;

        ArgumentNullException.ThrowIfNull(document);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureAppendBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
            activity.SetTag(FaesSemanticConventions.StartVersion, startVersion);
        }

        var documentPath = GetDocumentPathForRead(document, chunk);
        var appendBlob = await CreateAppendBlobClientAsync(document, documentPath);

        List<BlobJsonEvent> events;
        try
        {
            // Try incremental read when startVersion > 0 to avoid downloading the entire blob
            if (startVersion > 0)
            {
                var incremental = await TryDownloadIncrementalAsync(appendBlob, startVersion, cancellationToken);
                events = incremental ?? await DownloadAndParseNdjsonAsync(appendBlob, cancellationToken);
            }
            else
            {
                events = await DownloadAndParseNdjsonAsync(appendBlob, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }

        var maxVersion = untilVersion ?? document.Active.CurrentStreamVersion;

        // De-duplicate: single-pass dictionary keeps last occurrence per version
        var dedup = new Dictionary<int, IEvent>();
        foreach (var e in events)
        {
            if (e.EventVersion >= startVersion && e.EventVersion <= maxVersion)
            {
                dedup[e.EventVersion] = e;
            }
        }
        var result = dedup.Values.OrderBy(e => e.EventVersion).ToList();

        if (timer != null)
        {
            var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
            FaesMetrics.RecordStorageReadDuration(durationMs, FaesSemanticConventions.StorageProviderAppendBlob, document.ObjectName);
        }
        activity?.SetTag(FaesSemanticConventions.EventCount, result.Count);

        return result;
    }

    /// <summary>
    /// Reads events as a streaming async enumerable from the NDJSON append blob.
    /// </summary>
    public IAsyncEnumerable<IEvent> ReadAsStreamAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ReadAsStreamAsyncCore(document, startVersion, untilVersion, chunk, cancellationToken);
    }

    private async IAsyncEnumerable<IEvent> ReadAsStreamAsyncCore(
        IObjectDocument document,
        int startVersion,
        int? untilVersion,
        int? chunk,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("AppendBlobDataStore.ReadAsStream");

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureAppendBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document.ObjectId);
        }

        var documentPath = GetDocumentPathForRead(document, chunk);
        var appendBlob = await CreateAppendBlobClientAsync(document, documentPath);

        List<BlobJsonEvent> events;
        try
        {
            if (startVersion > 0)
            {
                var incremental = await TryDownloadIncrementalAsync(appendBlob, startVersion, cancellationToken);
                events = incremental ?? await DownloadAndParseNdjsonAsync(appendBlob, cancellationToken);
            }
            else
            {
                events = await DownloadAndParseNdjsonAsync(appendBlob, cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            yield break;
        }

        var maxVersion = untilVersion ?? document.Active.CurrentStreamVersion;

        // De-duplicate: single-pass dictionary keeps last occurrence per version
        var dedup = new Dictionary<int, IEvent>();
        foreach (var e in events)
        {
            if (e.EventVersion >= startVersion && e.EventVersion <= maxVersion)
            {
                dedup[e.EventVersion] = e;
            }
        }

        foreach (var evt in dedup.Values.OrderBy(e => e.EventVersion))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    /// <summary>
    /// Appends events to the NDJSON append blob with commit marker validation.
    /// </summary>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, cancellationToken, events);

    /// <summary>
    /// Appends events to the NDJSON append blob with commit marker validation.
    /// Phase 2 of the 2-phase commit: validates the commit marker hash chain, then atomically
    /// appends events + new commit marker using <c>IfAppendPositionEqual</c>.
    /// </summary>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("AppendBlobDataStore.Append");
        var timer = activity != null ? FaesMetrics.StartTimer() : null;

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureAppendBlob);
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.EventCount, events?.Length ?? 0);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        var appendBlobDoc = AppendBlobEventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(appendBlobDoc);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        // Fast path: if this stream is known to be closed, skip all I/O
        if (ClosedStreams.ContainsKey(document.Active.StreamIdentifier))
        {
            ThrowIfStreamClosed(document.Active.StreamIdentifier, isStreamClosed: true);
        }

        var documentPath = GetDocumentPathForAppend(document);
        var appendBlob = await CreateAppendBlobClientAsync(document, documentPath);

        // Get current blob properties to determine append position
        BlobProperties properties;
        try
        {
            properties = (await appendBlob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob doesn't exist yet - this shouldn't happen if GetOrCreateAsync was called first
            // (which creates the initial marker). Create now as fallback.
            await CreateInitialAppendBlobAsync(appendBlob, appendBlobDoc, cancellationToken);
            properties = (await appendBlob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        }

        var contentLength = properties.ContentLength;

        // Check Azure hard limit: throw before reaching 50,000 blocks to leave room for close event + marker
        if (properties.BlobCommittedBlockCount >= BlockCountThreshold)
        {
            var continuationStreamId = ComputeContinuationStreamId(document.Active.StreamIdentifier);
            throw new EventStreamClosedException(
                document.Active.StreamIdentifier,
                continuationStreamId,
                continuationStreamType: "appendblob",
                continuationDataStore: document.Active.DataStore,
                continuationDocumentStore: document.Active.DocumentStore,
                reason: "Azure append blob 50,000 block limit reached");
        }

        // Phase 2 Step 1: Range-read tail to find last commit marker and stream closed state
        var (lastMarker, isStreamClosed) = await ReadLastCommitMarkerAsync(appendBlob, contentLength, cancellationToken);

        // Phase 2 Step 2: Validate hash chain (with orphan and hash-drift recovery)
        var effectivePrevHash = appendBlobDoc.PrevHash ?? "*";
        if (lastMarker != null && lastMarker.Hash != appendBlobDoc.PrevHash)
        {
            var maxEventVersion = events.Max(e => e.EventVersion);
            var minEventVersion = events.Min(e => e.EventVersion);

            if (lastMarker.Version >= maxEventVersion)
            {
                // ORPHAN RECOVERY: A previous writer's Phase 2 succeeded server-side but the
                // client received a timeout. Recovery rolled back the document and changed its
                // hash, but the events + commit marker are already in the blob. Write a repair
                // marker to re-anchor the hash chain to the current document hash, and skip
                // writing duplicate events.
                await WriteRepairMarkerAsync(appendBlob, appendBlobDoc, lastMarker, contentLength, cancellationToken);
                RecordWriteMetrics(timer, document.ObjectName);
                return;
            }

            if (lastMarker.Version == minEventVersion - 1)
            {
                // HASH DRIFT: The commit marker is at the correct base version but the document
                // hash changed because recovery re-saved the document (adding rollback history).
                // No orphaned events exist — safe to proceed using the marker's hash as the
                // effective PrevHash to re-anchor the chain.
                effectivePrevHash = lastMarker.Hash;
            }
            else
            {
                // CONCURRENT WRITER: another writer's Phase 1 updated the document
                // but Phase 2 hasn't committed events yet.
                throw new BlobDataStoreProcessingException(
                    $"Optimistic concurrency check failed: commit marker hash mismatch for '{document.ObjectName?.ToLowerInvariant()}/{documentPath}'. " +
                    $"Expected marker hash '{appendBlobDoc.PrevHash}', found '{lastMarker.Hash}'. " +
                    "Another writer's Phase 1 updated the document but Phase 2 hasn't committed events yet.");
            }
        }

        // Phase 2 Step 3: Check if stream is closed
        ThrowIfStreamClosed(document.Active.StreamIdentifier, isStreamClosed);

        // Phase 2 Step 4: Serialize events + new commit marker as NDJSON
        var batchClosesStream = events.Any(e => e.EventType == "EventStream.Closed");
        var newMarker = new AppendBlobCommitMarker
        {
            Hash = appendBlobDoc.Hash ?? "*",
            PrevHash = effectivePrevHash,
            Version = events.Max(e => e.EventVersion),
            Offset = contentLength,
            Closed = batchClosesStream ? true : null
        };

        using var ndjsonStream = SerializeEventsAndMarker(events, newMarker, preserveTimestamp);

        // Phase 2 Step 5: Atomic append with position check
        await appendBlob.AppendBlockAsync(
            ndjsonStream,
            new AppendBlobAppendBlockOptions
            {
                Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = contentLength }
            },
            cancellationToken);

        RecordWriteMetrics(timer, document.ObjectName);
    }

    /// <summary>
    /// Creates the initial append blob with an initial commit marker.
    /// Used by AppendBlobObjectDocumentFactory.GetOrCreateAsync after creating the object document.
    /// </summary>
    internal async Task CreateInitialAppendBlobAsync(
        AppendBlobClient appendBlob,
        AppendBlobEventStreamDocument document,
        CancellationToken cancellationToken)
    {
        await appendBlob.CreateIfNotExistsAsync(
            new AppendBlobCreateOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" }
            },
            cancellationToken);

        var marker = new AppendBlobCommitMarker
        {
            Hash = document.Hash ?? "*",
            PrevHash = "*",
            Version = 0,
            Offset = 0
        };

        var markerLine = JsonSerializer.Serialize(marker, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker) + "\n";
        var markerBytes = Encoding.UTF8.GetBytes(markerLine);

        try
        {
            using var markerStream = new MemoryStream(markerBytes);
            await appendBlob.AppendBlockAsync(
                markerStream,
                new AppendBlobAppendBlockOptions
                {
                    Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = 0 }
                },
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Another writer created the initial marker first - this is fine
        }
    }

    /// <summary>
    /// Creates an AppendBlobClient for the given document and path.
    /// Can be used by AppendBlobDocumentStore to create initial append blobs.
    /// </summary>
    internal async Task<AppendBlobClient> CreateAppendBlobClientAsync(IObjectDocument objectDocument, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

#pragma warning disable CS0618 // Type or member is obsolete
        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DataStore)
            ? objectDocument.Active.DataStore
            : objectDocument.Active.StreamConnectionName;
#pragma warning restore CS0618
        var client = clientFactory.CreateClient(connectionName);
        var container = client.GetBlobContainerClient(objectDocument.ObjectName.ToLowerInvariant());

        var containerName = objectDocument.ObjectName.ToLowerInvariant();
        var cacheKey = $"{connectionName}:{containerName}";
        if (autoCreateContainer && VerifiedContainers.TryAdd(cacheKey, true))
        {
            await container.CreateIfNotExistsAsync();
        }

        var appendBlob = container.GetAppendBlobClient(documentPath)
            ?? throw new DocumentConfigurationException("Unable to create AppendBlobClient.");
        return appendBlob;
    }

    /// <inheritdoc />
    public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        // Append blobs cannot remove data. Return 0 to indicate no events were removed.
        // Recovery is handled by de-duplicating on read and rolling back the object document version.
        return Task.FromResult(0);
    }

    /// <summary>
    /// Writes a repair marker to re-anchor the hash chain after an orphan scenario.
    /// The repair marker bridges from the orphaned marker's hash to the current document hash.
    /// </summary>
    private static async Task WriteRepairMarkerAsync(
        AppendBlobClient appendBlob,
        AppendBlobEventStreamDocument document,
        AppendBlobCommitMarker orphanedMarker,
        long contentLength,
        CancellationToken cancellationToken)
    {
        var repairMarker = new AppendBlobCommitMarker
        {
            Hash = document.Hash ?? "*",
            PrevHash = orphanedMarker.Hash,
            Version = orphanedMarker.Version,
            Offset = orphanedMarker.Offset
        };

        var markerLine = JsonSerializer.Serialize(repairMarker, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker) + "\n";
        var markerBytes = Encoding.UTF8.GetBytes(markerLine);

        using var markerStream = new MemoryStream(markerBytes);
        await appendBlob.AppendBlockAsync(
            markerStream,
            new AppendBlobAppendBlockOptions
            {
                Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = contentLength }
            },
            cancellationToken);
    }

    /// <summary>
    /// Downloads the NDJSON blob via streaming and parses event lines (skipping commit markers).
    /// Uses streaming download to avoid buffering the entire blob into memory before parsing.
    /// </summary>
    private static async Task<List<BlobJsonEvent>> DownloadAndParseNdjsonAsync(
        AppendBlobClient appendBlob,
        CancellationToken cancellationToken)
    {
        var response = await appendBlob.DownloadStreamingAsync(
            options: null, cancellationToken: cancellationToken);

        var events = new List<BlobJsonEvent>();
        using var reader = new StreamReader(response.Value.Content, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip commit markers (lines starting with "$m" marker type)
            if (line.StartsWith("{\"$m\":"))
                continue;

            var evt = JsonSerializer.Deserialize(line, BlobDataStoreDocumentContext.Default.BlobJsonEvent);
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        return events;
    }

    /// <summary>
    /// Attempts an incremental read by using commit marker byte offsets to skip earlier events.
    /// Returns null if incremental read is not possible (blob too small, not enough markers in tail,
    /// or startVersion is too old for the markers available). The caller should fall back to a full download.
    /// </summary>
    private static async Task<List<BlobJsonEvent>?> TryDownloadIncrementalAsync(
        AppendBlobClient appendBlob,
        int startVersion,
        CancellationToken cancellationToken)
    {
        // Step 1: Get blob properties for content length
        BlobProperties properties;
        try
        {
            var propertiesResponse = await appendBlob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (propertiesResponse?.Value == null)
            {
                return null;
            }
            properties = propertiesResponse.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null; // Blob doesn't exist, let caller handle
        }

        var contentLength = properties.ContentLength;
        if (contentLength <= IncrementalReadThreshold)
        {
            return null; // Blob is small, full download is cheaper
        }

        // Step 2: Range-read tail to find commit markers with offsets
        var tailSize = (int)Math.Min(contentLength, TailReadSize);
        var tailRange = new HttpRange(contentLength - tailSize, tailSize);

        var tailResponse = await appendBlob.DownloadStreamingAsync(
            new BlobDownloadOptions { Range = tailRange },
            cancellationToken);

        using var tailStream = new MemoryStream();
        await tailResponse.Value.Content.CopyToAsync(tailStream, cancellationToken);
        var tailText = Encoding.UTF8.GetString(tailStream.ToArray());

        // Parse all markers from the tail
        var markers = new List<AppendBlobCommitMarker>();
        foreach (var line in tailText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("{\"$m\":"))
                continue;

            var marker = JsonSerializer.Deserialize(line, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker);
            if (marker?.Offset != null)
            {
                markers.Add(marker);
            }
        }

        if (markers.Count < 2)
        {
            return null; // Need at least 2 markers to safely determine batch boundaries
        }

        // Step 3: Sort by version and find the pair where Mi.version < startVersion <= Mi+1.version
        markers.Sort((a, b) => a.Version.CompareTo(b.Version));

        long? readOffset = null;
        for (var i = 0; i < markers.Count - 1; i++)
        {
            if (markers[i].Version < startVersion && startVersion <= markers[i + 1].Version)
            {
                readOffset = markers[i + 1].Offset;
                break;
            }
        }

        // If startVersion is beyond the last marker, there are no events to read
        if (readOffset == null && startVersion > markers[^1].Version)
        {
            return [];
        }

        if (readOffset == null)
        {
            return null; // startVersion is too old for the markers in the tail, fall back
        }

        // Step 4: Range-read from the determined offset to end of blob
        var dataResponse = await appendBlob.DownloadStreamingAsync(
            new BlobDownloadOptions { Range = new HttpRange(readOffset.Value) },
            cancellationToken);

        var events = new List<BlobJsonEvent>();
        using var reader = new StreamReader(dataResponse.Value.Content, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("{\"$m\":"))
                continue;

            var evt = JsonSerializer.Deserialize(line, BlobDataStoreDocumentContext.Default.BlobJsonEvent);
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        return events;
    }

    /// <summary>
    /// Reads the last commit marker and checks for stream closed state from the tail of the append blob.
    /// </summary>
    /// <returns>A tuple of the last commit marker (if found) and whether the stream is closed.</returns>
    internal static async Task<(AppendBlobCommitMarker? LastMarker, bool IsStreamClosed)> ReadLastCommitMarkerAsync(
        AppendBlobClient appendBlob,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength == 0)
            return (null, false);

        var tailSize = (int)Math.Min(contentLength, TailReadSize);
        var range = new HttpRange(contentLength - tailSize, tailSize);

        var response = await appendBlob.DownloadStreamingAsync(
            new BlobDownloadOptions { Range = range },
            cancellationToken);

        using var stream = new MemoryStream();
        await response.Value.Content.CopyToAsync(stream, cancellationToken);
        var tailText = Encoding.UTF8.GetString(stream.ToArray());

        // Find the last commit marker line and check for stream closed events.
        // Primary detection uses the marker's Closed field (set by new writers).
        // Fallback uses string matching for pre-upgrade markers that lack the field.
        AppendBlobCommitMarker? lastMarker = null;
        var isStreamClosed = false;
        var closedViaStringMatch = false;

        foreach (var line in tailText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("{\"$m\":"))
            {
                var marker = JsonSerializer.Deserialize(line, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker);
                if (marker != null)
                {
                    lastMarker = marker;
                    if (marker.Closed == true)
                    {
                        isStreamClosed = true;
                    }
                }
            }
            else if (line.Contains("\"EventStream.Closed\""))
            {
                closedViaStringMatch = true;
            }
        }

        // Use string match as fallback for pre-upgrade markers
        if (!isStreamClosed && closedViaStringMatch)
        {
            isStreamClosed = true;
        }

        return (lastMarker, isStreamClosed);
    }

    /// <summary>
    /// Checks whether the stream has been closed by examining event lines in the tail data.
    /// Throws <see cref="EventStreamClosedException"/> if the last event in the tail is a stream close event.
    /// </summary>
    private static void ThrowIfStreamClosed(string streamIdentifier, bool isStreamClosed)
    {
        if (isStreamClosed)
        {
            ClosedStreams.TryAdd(streamIdentifier, true);
            throw new EventStreamClosedException(
                streamIdentifier,
                $"Cannot append events to closed stream '{streamIdentifier}'. " +
                $"The stream was closed and may have a continuation stream. Please retry on the active stream.");
        }
    }

    /// <summary>
    /// Computes the continuation stream ID by incrementing the 10-digit numeric suffix.
    /// Stream identifiers follow the format: {prefix}-{10-digit-number}.
    /// </summary>
    public static string ComputeContinuationStreamId(string streamIdentifier)
    {
        var dashIndex = streamIdentifier.LastIndexOf('-');
        if (dashIndex < 0 || dashIndex >= streamIdentifier.Length - 1)
        {
            throw new InvalidOperationException(
                $"Stream identifier '{streamIdentifier}' does not follow the expected format '{{prefix}}-{{10-digit-suffix}}'.");
        }

        var prefix = streamIdentifier[..dashIndex];
        var suffixStr = streamIdentifier[(dashIndex + 1)..];

        if (!long.TryParse(suffixStr, out var suffix))
        {
            throw new InvalidOperationException(
                $"Stream identifier suffix '{suffixStr}' is not a valid number.");
        }

        return $"{prefix}-{suffix + 1:d10}";
    }

    /// <summary>
    /// Computes the blob path for reading events, optionally from a specific chunk.
    /// </summary>
    public static string GetDocumentPathForRead(IObjectDocument document, int? chunk = null)
    {
        if (document.Active.ChunkingEnabled())
        {
            if (chunk.HasValue)
                return $"{document.Active.StreamIdentifier}-{chunk.Value:d10}.ndjson";

            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            return $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.ndjson";
        }

        return $"{document.Active.StreamIdentifier}.ndjson";
    }

    /// <summary>
    /// Computes the blob path for appending events (always targets the last chunk).
    /// </summary>
    public static string GetDocumentPathForAppend(IObjectDocument document)
    {
        if (document.Active.ChunkingEnabled())
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            return $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.ndjson";
        }

        return $"{document.Active.StreamIdentifier}.ndjson";
    }

    private static readonly byte[] NewLine = "\n"u8.ToArray();

    /// <summary>
    /// Serializes events and a commit marker as NDJSON directly into a <see cref="MemoryStream"/>.
    /// Avoids the intermediate StringBuilder → string → byte[] triple allocation.
    /// </summary>
    private static MemoryStream SerializeEventsAndMarker(IEvent[] events, AppendBlobCommitMarker marker, bool preserveTimestamp)
    {
        var stream = new MemoryStream();

        foreach (var evt in events)
        {
            var blobEvent = BlobJsonEvent.From(evt, preserveTimestamp);
            if (blobEvent != null)
            {
                JsonSerializer.Serialize(stream, blobEvent, BlobDataStoreDocumentContext.Default.BlobJsonEvent);
                stream.Write(NewLine);
            }
        }

        JsonSerializer.Serialize(stream, marker, AppendBlobCommitMarkerContext.Default.AppendBlobCommitMarker);
        stream.Write(NewLine);

        stream.Position = 0;
        return stream;
    }

    private static void RecordWriteMetrics(System.Diagnostics.Stopwatch? timer, string? objectName)
    {
        if (timer != null)
        {
            var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
            FaesMetrics.RecordStorageWriteDuration(durationMs, FaesSemanticConventions.StorageProviderAppendBlob, objectName!);
        }
    }
}
