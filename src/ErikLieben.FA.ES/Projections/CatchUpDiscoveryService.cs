using ErikLieben.FA.ES.Observability;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Default implementation of <see cref="ICatchUpDiscoveryService"/> that uses
/// <see cref="IObjectIdProvider"/> to enumerate object IDs for catch-up processing.
/// </summary>
public class CatchUpDiscoveryService : ICatchUpDiscoveryService
{
    private readonly IObjectIdProvider _objectIdProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatchUpDiscoveryService"/> class.
    /// </summary>
    /// <param name="objectIdProvider">The object ID provider to use for enumeration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="objectIdProvider"/> is null.</exception>
    public CatchUpDiscoveryService(IObjectIdProvider objectIdProvider)
    {
        _objectIdProvider = objectIdProvider ?? throw new ArgumentNullException(nameof(objectIdProvider));
    }

    /// <inheritdoc />
    public async Task<CatchUpDiscoveryResult> DiscoverWorkItemsAsync(
        string[] objectNames,
        int pageSize = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("CatchUp.Discover");

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.PageSize, pageSize);
            activity.SetTag(FaesSemanticConventions.HasContinuation, continuationToken != null);
        }

        ArgumentNullException.ThrowIfNull(objectNames);

        if (objectNames.Length == 0)
        {
            return new CatchUpDiscoveryResult([], null, 0);
        }

        // Parse continuation token to determine current position
        var state = ParseContinuationToken(continuationToken, objectNames);
        var workItems = new List<CatchUpWorkItem>();
        var remainingPageSize = pageSize;

        // Continue from current object type index
        for (var i = state.ObjectIndex; i < objectNames.Length && remainingPageSize > 0; i++)
        {
            var objectName = objectNames[i];
            var providerToken = i == state.ObjectIndex ? state.ProviderToken : null;

            var result = await _objectIdProvider.GetObjectIdsAsync(
                objectName,
                providerToken,
                remainingPageSize,
                cancellationToken);

            foreach (var objectId in result.Items)
            {
                workItems.Add(new CatchUpWorkItem(objectName, objectId));
            }

            remainingPageSize -= result.Items.Count;

            // If there are more items in this object type, return with continuation token
            if (result.HasNextPage)
            {
                var nextToken = CreateContinuationToken(i, result.ContinuationToken);

                if (activity?.IsAllDataRequested == true)
                {
                    activity.SetTag(FaesSemanticConventions.EventCount, workItems.Count);
                }

                return new CatchUpDiscoveryResult(workItems, nextToken, null);
            }
        }

        // Check if there are more object types to process
        var processedAllObjectTypes = state.ObjectIndex >= objectNames.Length - 1 ||
                                       workItems.Count < pageSize;

        string? finalToken = null;
        if (!processedAllObjectTypes && state.ObjectIndex < objectNames.Length - 1)
        {
            // Move to next object type
            finalToken = CreateContinuationToken(state.ObjectIndex + 1, null);
        }

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.EventCount, workItems.Count);
        }

        return new CatchUpDiscoveryResult(workItems, finalToken, null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CatchUpWorkItem> StreamWorkItemsAsync(
        string[] objectNames,
        int pageSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("CatchUp.Stream");
        long itemCount = 0;

        ArgumentNullException.ThrowIfNull(objectNames);

        foreach (var objectName in objectNames)
        {
            string? token = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _objectIdProvider.GetObjectIdsAsync(
                    objectName,
                    token,
                    pageSize,
                    cancellationToken);

                foreach (var objectId in result.Items)
                {
                    itemCount++;
                    yield return new CatchUpWorkItem(objectName, objectId);
                }

                token = result.ContinuationToken;
            } while (!string.IsNullOrEmpty(token));
        }

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.EventCount, itemCount);
        }
    }

    /// <inheritdoc />
    public async Task<long> EstimateTotalWorkItemsAsync(
        string[] objectNames,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("CatchUp.Estimate");

        ArgumentNullException.ThrowIfNull(objectNames);

        long total = 0;
        foreach (var objectName in objectNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += await _objectIdProvider.CountAsync(objectName, cancellationToken);
        }

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.TotalEstimate, total);
        }

        return total;
    }

    private static ContinuationState ParseContinuationToken(string? token, string[] objectNames)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new ContinuationState(0, null);
        }

        try
        {
            var bytes = Convert.FromBase64String(token);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var state = JsonSerializer.Deserialize(json, CatchUpJsonContext.Default.ContinuationTokenData);

            if (state is null)
            {
                return new ContinuationState(0, null);
            }

            // Validate object index is within bounds
            if (state.ObjectIndex < 0 || state.ObjectIndex >= objectNames.Length)
            {
                return new ContinuationState(0, null);
            }

            return new ContinuationState(state.ObjectIndex, state.ProviderToken);
        }
        catch
        {
            // Invalid token, start from beginning
            return new ContinuationState(0, null);
        }
    }

    private static string CreateContinuationToken(int objectIndex, string? providerToken)
    {
        var data = new ContinuationTokenData(objectIndex, providerToken);
        var json = JsonSerializer.Serialize(data, CatchUpJsonContext.Default.ContinuationTokenData);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    private record ContinuationState(int ObjectIndex, string? ProviderToken);
}

/// <summary>
/// Internal data structure for serializing catch-up continuation tokens.
/// </summary>
internal record ContinuationTokenData(int ObjectIndex, string? ProviderToken);

/// <summary>
/// Source-generated JSON context for catch-up serialization types.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ContinuationTokenData))]
internal partial class CatchUpJsonContext : JsonSerializerContext
{
}
