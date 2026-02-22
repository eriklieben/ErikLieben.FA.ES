using Azure.Data.Tables;

namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Represents a pending operation to be executed against Azure Table Storage.
/// </summary>
public sealed class TableOperation
{
    /// <summary>
    /// Gets the type of the operation.
    /// </summary>
    public TableOperationType Type { get; private init; }

    /// <summary>
    /// Gets the partition key for the operation.
    /// </summary>
    public string PartitionKey { get; private init; } = string.Empty;

    /// <summary>
    /// Gets the row key for the operation.
    /// </summary>
    public string RowKey { get; private init; } = string.Empty;

    /// <summary>
    /// Gets the entity for upsert operations.
    /// </summary>
    public ITableEntity? Entity { get; private init; }

    private TableOperation() { }

    /// <summary>
    /// Creates an upsert operation for the specified entity.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>A new upsert operation.</returns>
    public static TableOperation Upsert<TEntity>(TEntity entity) where TEntity : ITableEntity
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new TableOperation
        {
            Type = TableOperationType.Upsert,
            PartitionKey = entity.PartitionKey,
            RowKey = entity.RowKey,
            Entity = entity
        };
    }

    /// <summary>
    /// Creates a delete operation for the specified entity keys.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="rowKey">The row key.</param>
    /// <returns>A new delete operation.</returns>
    public static TableOperation Delete(string partitionKey, string rowKey)
    {
        ArgumentNullException.ThrowIfNull(partitionKey);
        ArgumentNullException.ThrowIfNull(rowKey);
        return new TableOperation
        {
            Type = TableOperationType.Delete,
            PartitionKey = partitionKey,
            RowKey = rowKey
        };
    }
}

/// <summary>
/// Specifies the type of table operation.
/// </summary>
public enum TableOperationType
{
    /// <summary>
    /// Insert or update an entity.
    /// </summary>
    Upsert,

    /// <summary>
    /// Delete an entity.
    /// </summary>
    Delete
}
