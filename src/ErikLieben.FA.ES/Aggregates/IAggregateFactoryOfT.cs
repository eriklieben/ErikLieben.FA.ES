using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

/// <summary>
/// Provides covariance factory operations for creating aggregates from different sources.
/// </summary>
/// <typeparam name="T">The aggregate base type.</typeparam>
public interface IAggregateCovarianceFactory<out T> where T : IBase
{
    /// <summary>
    /// Gets the logical object name for the aggregate type.
    /// </summary>
    /// <returns>The object name used to identify the aggregate type.</returns>
    string GetObjectName();

    /// <summary>
    /// Creates a new aggregate instance from the specified event stream.
    /// </summary>
    /// <param name="eventStream">The event stream used to construct the aggregate.</param>
    /// <returns>A new aggregate instance.</returns>
    T Create(IEventStream eventStream);

    /// <summary>
    /// Creates a new aggregate instance from the specified document.
    /// </summary>
    /// <param name="document">The object document backing the aggregate.</param>
    /// <returns>A new aggregate instance loaded from the document.</returns>
    T Create(IObjectDocument document);
}

/// <summary>
/// Defines asynchronous factory operations for an aggregate type using string identifiers.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public interface IAggregateFactory<T> : IAggregateCovarianceFactory<T> where T : IBase
{
    /// <summary>
    /// Creates a new aggregate with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns the created aggregate.</returns>
    Task<T> CreateAsync(string id);

    /// <summary>
    /// Gets an existing aggregate by identifier.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns the aggregate instance.</returns>
    Task<T> GetAsync(string id);

    /// <summary>
    /// Gets the first aggregate tagged with the specified document tag.
    /// </summary>
    /// <param name="tag">The document tag value.</param>
    /// <returns>A task that returns the aggregate instance, or null if not found.</returns>
    Task<T?> GetFirstByDocumentTag(string tag);

    /// <summary>
    /// Gets all aggregates tagged with the specified document tag.
    /// </summary>
    /// <param name="tag">The document tag value.</param>
    /// <returns>A task that returns the matching aggregate instances.</returns>
    Task<IEnumerable<T>> GetAllByDocumentTag(string tag);

    /// <summary>
    /// Gets the aggregate together with its backing document.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns a tuple with the aggregate and its document.</returns>
    Task<(T, IObjectDocument)> GetWithDocumentAsync(string id);
}

/// <summary>
/// Defines asynchronous factory operations for an aggregate type using strong typed identifiers.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
public interface IAggregateFactory<T,TId> : IAggregateCovarianceFactory<T> where T : IBase
{
    /// <summary>
    /// Creates a new aggregate with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns the created aggregate.</returns>
    Task<T> CreateAsync(TId id);

    /// <summary>
    /// Gets an existing aggregate by identifier.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns the aggregate instance.</returns>
    Task<T> GetAsync(TId id);

    /// <summary>
    /// Gets the first aggregate tagged with the specified document tag.
    /// </summary>
    /// <param name="tag">The document tag value.</param>
    /// <returns>A task that returns the aggregate instance, or null if not found.</returns>
    Task<T?> GetFirstByDocumentTag(string tag);

    /// <summary>
    /// Gets all aggregates tagged with the specified document tag.
    /// </summary>
    /// <param name="tag">The document tag value.</param>
    /// <returns>A task that returns the matching aggregate instances.</returns>
    Task<IEnumerable<T>> GetAllByDocumentTag(string tag);

    /// <summary>
    /// Gets the aggregate together with its backing document.
    /// </summary>
    /// <param name="id">The identifier of the aggregate.</param>
    /// <returns>A task that returns a tuple with the aggregate and its document.</returns>
    Task<(T, IObjectDocument)> GetWithDocumentAsync(TId id);
}
