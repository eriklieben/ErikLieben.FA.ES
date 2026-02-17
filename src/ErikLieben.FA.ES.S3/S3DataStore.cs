using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using Amazon.S3;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Exceptions;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.Extensions;
using ErikLieben.FA.ES.S3.Model;
using S3DataStoreDocumentContext = ErikLieben.FA.ES.S3.Model.S3DataStoreDocumentContext;

namespace ErikLieben.FA.ES.S3;

/// <summary>
/// Provides an S3-compatible storage-backed implementation of <see cref="IDataStore"/> for reading and appending event streams.
/// </summary>
public class S3DataStore : IDataStore, IDataStoreRecovery
{
    private static readonly ConcurrentDictionary<string, bool> VerifiedBuckets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cache of stream IDs that are known to be closed.
    /// Once a stream is closed, it remains closed forever, so we can cache this
    /// to avoid downloading the S3 object on every append attempt.
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> ClosedStreams = new(StringComparer.OrdinalIgnoreCase);

    private readonly IS3ClientFactory clientFactory;
    private readonly EventStreamS3Settings settings;

    /// <summary>
    /// Clears the verified buckets cache. Used by tests to prevent cross-test pollution.
    /// </summary>
    public static void ClearVerifiedBucketsCache() => VerifiedBuckets.Clear();

    /// <summary>
    /// Clears the closed streams cache. Primarily intended for testing scenarios.
    /// </summary>
    public static void ClearClosedStreamCache() => ClosedStreams.Clear();

    /// <summary>
    /// Initializes a new instance of the <see cref="S3DataStore"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory used to create <see cref="IAmazonS3"/> instances.</param>
    /// <param name="settings">The S3 storage settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clientFactory"/> or <paramref name="settings"/> is null.</exception>
    public S3DataStore(IS3ClientFactory clientFactory, EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        this.clientFactory = clientFactory;
        this.settings = settings;
    }

    /// <summary>
    /// Reads events for the specified document from S3-compatible storage.
    /// </summary>
    /// <param name="document">The document whose event stream is read.</param>
    /// <param name="startVersion">The zero-based version to start reading from (inclusive).</param>
    /// <param name="untilVersion">The final version to read up to (inclusive); null to read to the end.</param>
    /// <param name="chunk">The chunk identifier to read from when chunking is enabled; null when not chunked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A sequence of events ordered by version, or null when the stream does not exist.</returns>
    public async Task<IEnumerable<IEvent>?> ReadAsync(
        IObjectDocument document,
        int startVersion = 0,
        int? untilVersion = null,
        int? chunk = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3DataStore.Read");
        var timer = activity != null ? FaesMetrics.StartTimer() : null;

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.StartVersion, startVersion);
        }

        var (bucketName, key) = GetDataStoreLocation(document!, chunk);
        var s3Client = CreateS3Client(document!);

        S3DataStoreDocument? dataDocument;
        try
        {
            var result = await s3Client.GetObjectAsEntityAsync(
                bucketName,
                key,
                S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            dataDocument = result.Document;
            if (dataDocument == null)
            {
                return null;
            }
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            FaesInstrumentation.RecordException(activity, ex);
            throw new InvalidOperationException(
                $"The bucket '{bucketName}' was not found. " +
                "Create the bucket in your deployment or enable AutoCreateBucket in your S3 storage configuration.",
                ex);
        }

        var events = dataDocument.Events
            .Where(e => e.EventVersion >= startVersion && (!untilVersion.HasValue || e.EventVersion <= untilVersion))
            .ToList();

        // Record metrics
        if (timer != null)
        {
            var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
            FaesMetrics.RecordStorageReadDuration(durationMs, "s3", document!.ObjectName);
        }
        activity?.SetTag(FaesSemanticConventions.EventCount, events.Count);

        return events;
    }

    /// <summary>
    /// Reads events for the specified document as a streaming async enumerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method yields events one at a time after loading the S3 object.
    /// While S3 requires downloading the entire object upfront,
    /// streaming the results allows consumers to process events incrementally and supports
    /// early cancellation without holding references to the full list.
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
        using var activity = FaesInstrumentation.Storage.StartActivity("S3DataStore.ReadAsStream");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationRead);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        var (bucketName, key) = GetDataStoreLocation(document!, chunk);
        var s3Client = CreateS3Client(document!);

        S3DataStoreDocument? dataDocument;
        try
        {
            var result = await s3Client.GetObjectAsEntityAsync(
                bucketName,
                key,
                S3DataStoreDocumentContext.Default.S3DataStoreDocument);

            dataDocument = result.Document;
            if (dataDocument == null)
            {
                yield break;
            }
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            yield break;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
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
    /// Appends the specified events to the event stream of the given document in S3-compatible storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    public Task AppendAsync(IObjectDocument document, CancellationToken cancellationToken, params IEvent[] events)
        => AppendAsync(document, preserveTimestamp: false, cancellationToken, events);

    /// <summary>
    /// Appends the specified events to the event stream of the given document in S3-compatible storage.
    /// </summary>
    /// <param name="document">The document whose event stream is appended to.</param>
    /// <param name="preserveTimestamp">When true, preserves the original timestamp from S3JsonEvent sources (useful for migrations).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="events">The events to append in order; must contain at least one event.</param>
    /// <returns>A task that represents the asynchronous append operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or its stream identifier is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no events are provided.</exception>
    /// <exception cref="InvalidOperationException">Thrown when optimistic concurrency or persistence operations fail.</exception>
    public async Task AppendAsync(IObjectDocument document, bool preserveTimestamp, CancellationToken cancellationToken, params IEvent[] events)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3DataStore.Append");
        var timer = activity != null ? FaesMetrics.StartTimer() : null;

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationWrite);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
            activity.SetTag(FaesSemanticConventions.EventCount, events?.Length ?? 0);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);
        ArgumentNullException.ThrowIfNull(events);

        var s3Doc = S3EventStreamDocument.From(document);
        ArgumentNullException.ThrowIfNull(s3Doc);

        if (events.Length == 0)
        {
            throw new ArgumentException("No events provided to store.");
        }

        // Fast path: if this stream is known to be closed, skip all I/O
        if (ClosedStreams.ContainsKey(document.Active.StreamIdentifier))
        {
            throw new EventStreamClosedException(
                document.Active.StreamIdentifier,
                $"Cannot append events to closed stream '{document.Active.StreamIdentifier}'. " +
                $"The stream was closed and may have a continuation stream. Please retry on the active stream.");
        }

        var (bucketName, key) = GetDataStoreLocation(document, chunk: null, forAppend: true);
        var s3Client = CreateS3Client(document);

        // Cache bucket existence to avoid a PutBucket round-trip on every append.
        // Buckets don't disappear in normal operation, so a per-process cache is safe.
        if (settings.AutoCreateBucket && VerifiedBuckets.TryAdd(bucketName, true))
        {
            await s3Client.EnsureBucketAsync(bucketName);
        }

        // Attempt to download the object directly instead of checking ObjectExistsAsync first.
        // GetObjectAsEntityAsync already returns (null, null, null) on 404, and provides
        // the ETag in its response — eliminating both ObjectExistsAsync and GetObjectETagAsync.
        var downloadResult = await s3Client.GetObjectAsEntityAsync(
            bucketName,
            key,
            S3DataStoreDocumentContext.Default.S3DataStoreDocument);

        if (downloadResult.Document == null)
        {
            // Object does not exist — create a new one
            var newDoc = new S3DataStoreDocument
            {
                ObjectId = document.ObjectId,
                ObjectName = document.ObjectName,
                LastObjectDocumentHash = s3Doc.Hash ?? "*"
            };
            newDoc.Events.AddRange(events.Select(e => S3JsonEvent.From(e, preserveTimestamp))!);

            try
            {
                await s3Client.PutObjectAsEntityAsync(
                    bucketName,
                    key,
                    newDoc,
                    S3DataStoreDocumentContext.Default.S3DataStoreDocument,
                    ifNoneMatch: settings.SupportsConditionalWrites ? "*" : null);
            }
            catch (InvalidOperationException) when (settings.SupportsConditionalWrites)
            {
                // Conditional write conflict: another writer created this stream concurrently.
                // Re-throw as a concurrency exception so the caller can retry.
                throw new InvalidOperationException(
                    $"Concurrent stream creation conflict for '{document.ObjectName.ToLowerInvariant()}/{key}'. " +
                    "Another writer created this stream simultaneously. Please retry the operation.");
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
            {
                FaesInstrumentation.RecordException(activity, ex);
                throw new InvalidOperationException(
                    $"The bucket '{bucketName}' was not found. " +
                    "Please create it or enable AutoCreateBucket in your S3 storage configuration.",
                    ex);
            }

            // Record metrics for new object
            if (timer != null)
            {
                var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
                FaesMetrics.RecordStorageWriteDuration(durationMs, "s3", document.ObjectName);
            }
            return;
        }

        var doc = downloadResult.Document;
        var etag = downloadResult.ETag;

        // Check if the stream is closed - if the last event is EventStream.Closed, reject new events
        if (doc.Events.Count > 0)
        {
            var lastEvent = doc.Events[^1];
            if (lastEvent.EventType == "EventStream.Closed")
            {
                // Cache this — closed streams never reopen
                ClosedStreams.TryAdd(document.Active.StreamIdentifier, true);

                throw new EventStreamClosedException(
                    document.Active.StreamIdentifier,
                    $"Cannot append events to closed stream '{document.Active.StreamIdentifier}'. " +
                    $"The stream was closed and may have a continuation stream. Please retry on the active stream.");
            }
        }

        if (doc.LastObjectDocumentHash != "*" && doc.LastObjectDocumentHash != s3Doc.PrevHash)
        {
            throw new InvalidOperationException($"Optimistic concurrency check failed: document hash mismatch for '{document.ObjectName.ToLowerInvariant()}/{key}'.");
        }
        doc.LastObjectDocumentHash = s3Doc.Hash ?? "*";

        doc.Events.AddRange(events.Select(e => S3JsonEvent.From(e, preserveTimestamp))!);
        await s3Client.PutObjectAsEntityAsync(
            bucketName,
            key,
            doc,
            S3DataStoreDocumentContext.Default.S3DataStoreDocument,
            ifMatchETag: settings.SupportsConditionalWrites ? etag : null);

        // Record metrics for existing object update
        if (timer != null)
        {
            var durationMs = FaesMetrics.StopAndGetElapsedMs(timer);
            FaesMetrics.RecordStorageWriteDuration(durationMs, "s3", document.ObjectName);
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3DataStore.RemoveEventsForFailedCommit");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.DbOperation, FaesSemanticConventions.DbOperationDelete);
            activity.SetTag(FaesSemanticConventions.ObjectName, document?.ObjectName);
            activity.SetTag(FaesSemanticConventions.ObjectId, document?.ObjectId);
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Active.StreamIdentifier);

        // Determine object key based on chunking
        var (bucketName, key) = GetDataStoreLocation(document, chunk: null, forAppend: true);
        var s3Client = CreateS3Client(document);

        try
        {
            // Get current ETag for optimistic concurrency
            var etag = await s3Client.GetObjectETagAsync(bucketName, key);
            if (etag == null)
            {
                // Object doesn't exist - nothing to clean up
                return 0;
            }

            var downloadResult = await s3Client.GetObjectAsEntityAsync(
                bucketName,
                key,
                S3DataStoreDocumentContext.Default.S3DataStoreDocument,
                etag);

            var doc = downloadResult.Document;

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
                // Rewrite object with filtered events
                await s3Client.PutObjectAsEntityAsync(
                    bucketName,
                    key,
                    doc,
                    S3DataStoreDocumentContext.Default.S3DataStoreDocument,
                    etag);
            }

            activity?.SetTag(FaesSemanticConventions.EventCount, removed);
            return removed;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            // Object doesn't exist - nothing to clean up
            return 0;
        }
    }

    /// <summary>
    /// Creates an S3 client using the data store name from the document.
    /// </summary>
    private IAmazonS3 CreateS3Client(IObjectDocument objectDocument)
    {
        ArgumentNullException.ThrowIfNull(objectDocument.ObjectName);

        var connectionName = !string.IsNullOrWhiteSpace(objectDocument.Active.DataStore)
            ? objectDocument.Active.DataStore
            : settings.DefaultDataStore;

        return clientFactory.CreateClient(connectionName);
    }

    /// <summary>
    /// Determines the S3 bucket name and object key for a data store document.
    /// </summary>
    /// <param name="document">The object document.</param>
    /// <param name="chunk">The chunk identifier, or null when not chunked.</param>
    /// <param name="forAppend">When true, uses the last chunk for append operations.</param>
    /// <returns>A tuple of (bucketName, key).</returns>
    private static (string BucketName, string Key) GetDataStoreLocation(IObjectDocument document, int? chunk, bool forAppend = false)
    {
        var bucketName = document.ObjectName.ToLowerInvariant();

        string documentPath;
        if (forAppend && document.Active.ChunkingEnabled())
        {
            var chunks = document.Active.StreamChunks;
            var lastChunk = chunks[chunks.Count - 1];
            documentPath = $"{document.Active.StreamIdentifier}-{lastChunk.ChunkIdentifier:d10}.json";
        }
        else if (document.Active.ChunkingEnabled() && chunk.HasValue)
        {
            documentPath = $"{document.Active.StreamIdentifier}-{chunk:d10}.json";
        }
        else if (document.Active.ChunkingEnabled())
        {
            documentPath = $"{document.Active.StreamIdentifier}-{chunk:d10}.json";
        }
        else
        {
            documentPath = $"{document.Active.StreamIdentifier}.json";
        }

        return (bucketName, documentPath);
    }
}
