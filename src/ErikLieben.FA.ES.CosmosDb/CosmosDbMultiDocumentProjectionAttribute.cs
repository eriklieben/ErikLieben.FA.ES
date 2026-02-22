namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Marks a projection class for storage as multiple documents in Azure CosmosDB.
/// Each event processed can create a new document, enabling audit logs and event-based projections.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CosmosDbMultiDocumentProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the CosmosDB container where projection documents are stored.
    /// </summary>
    public string ContainerName { get; }

    /// <summary>
    /// Gets or sets the partition key path for the container.
    /// Defaults to "/partitionKey".
    /// </summary>
    public string PartitionKeyPath { get; set; } = "/partitionKey";

    /// <summary>
    /// Gets or sets a value indicating whether the container should be created automatically if it doesn't exist.
    /// Defaults to true.
    /// </summary>
    public bool AutoCreateContainer { get; set; } = true;

    /// <summary>
    /// Gets or sets the CosmosDB connection name. If not set, uses the default connection.
    /// </summary>
    public string? Connection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbMultiDocumentProjectionAttribute"/> class.
    /// </summary>
    /// <param name="containerName">The name of the CosmosDB container where projection documents are stored.</param>
    public CosmosDbMultiDocumentProjectionAttribute(string containerName)
    {
        ArgumentNullException.ThrowIfNull(containerName);
        ContainerName = containerName;
    }
}
