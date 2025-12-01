using System.Diagnostics.CodeAnalysis;

namespace ErikLieben.FA.ES.Documents;

/// <summary>
/// Defines operations for creating, retrieving, and saving object documents.
/// Storage-agnostic interface that can be implemented by various backends.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Creates a new object document with default stream metadata if it does not already exist.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to create.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The created or existing <see cref="IObjectDocument"/> instance.</returns>
    [return: MaybeNull]
    Task<IObjectDocument> CreateAsync(
        string name,
        string objectId,
        string? store = null);

    /// <summary>
    /// Retrieves an existing object document from storage.
    /// </summary>
    /// <param name="name">The object type/name used to determine the container and path.</param>
    /// <param name="objectId">The identifier of the object to retrieve.</param>
    /// <param name="store">Optional store name override. If not provided, uses the default document store.</param>
    /// <returns>The requested <see cref="IObjectDocument"/>.</returns>
    Task<IObjectDocument> GetAsync(
        string name,
        string objectId,
        string? store = null);

    /// <summary>
    /// Persists the specified object document to storage.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SetAsync(IObjectDocument document);
}
