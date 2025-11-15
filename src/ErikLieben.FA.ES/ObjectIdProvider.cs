using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Exceptions;
using System.Diagnostics;

namespace ErikLieben.FA.ES;

/// <summary>
/// Resolves and delegates object ID operations to the appropriate provider based on configured defaults.
/// </summary>
public class ObjectIdProvider : IObjectIdProvider
{
    private readonly IDictionary<string, IObjectIdProvider> objectIdProviders;
    private readonly EventStreamDefaultTypeSettings settings;
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES");

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectIdProvider"/> class.
    /// </summary>
    /// <param name="objectIdProviders">A keyed collection of underlying providers by store type.</param>
    /// <param name="settings">Default type settings used when resolving providers.</param>
    public ObjectIdProvider(
        IDictionary<string, IObjectIdProvider> objectIdProviders,
        EventStreamDefaultTypeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(objectIdProviders);
        ArgumentNullException.ThrowIfNull(settings);

        this.objectIdProviders = objectIdProviders;
        this.settings = settings;
    }

    /// <summary>
    /// Gets a page of object IDs for the specified object type using continuation tokens.
    /// </summary>
    /// <param name="objectName">The object type name (e.g., "project", "workItem").</param>
    /// <param name="continuationToken">Optional continuation token from previous page. Pass null for first page.</param>
    /// <param name="pageSize">Number of items to return per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result with object IDs and continuation token for the next page.</returns>
    public Task<PagedResult<string>> GetObjectIdsAsync(
        string objectName,
        string? continuationToken,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"ObjectIdProvider.{nameof(GetObjectIdsAsync)}");
        activity?.AddTag("ObjectName", objectName);
        activity?.AddTag("PageSize", pageSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var providerType = settings.DocumentType.ToLowerInvariant();
        if (objectIdProviders.TryGetValue(providerType, out var provider))
        {
            return provider.GetObjectIdsAsync(objectName, continuationToken, pageSize, cancellationToken);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find object ID provider for DocumentType: {providerType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Checks if an object document exists for the given ID.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the object exists, false otherwise.</returns>
    public Task<bool> ExistsAsync(
        string objectName,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"ObjectIdProvider.{nameof(ExistsAsync)}");
        activity?.AddTag("ObjectName", objectName);
        activity?.AddTag("ObjectId", objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var providerType = settings.DocumentType.ToLowerInvariant();
        if (objectIdProviders.TryGetValue(providerType, out var provider))
        {
            return provider.ExistsAsync(objectName, objectId, cancellationToken);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find object ID provider for DocumentType: {providerType}." +
            " Are you sure it's properly registered in the configuration?");
    }

    /// <summary>
    /// Gets the total count of objects for the given type.
    /// Warning: This may be expensive for large datasets as it requires enumerating all items.
    /// </summary>
    /// <param name="objectName">The object type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of unique object IDs.</returns>
    public Task<long> CountAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"ObjectIdProvider.{nameof(CountAsync)}");
        activity?.AddTag("ObjectName", objectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var providerType = settings.DocumentType.ToLowerInvariant();
        if (objectIdProviders.TryGetValue(providerType, out var provider))
        {
            return provider.CountAsync(objectName, cancellationToken);
        }

        throw new UnableToFindDocumentFactoryException(
            $"Unable to find object ID provider for DocumentType: {providerType}." +
            " Are you sure it's properly registered in the configuration?");
    }
}
