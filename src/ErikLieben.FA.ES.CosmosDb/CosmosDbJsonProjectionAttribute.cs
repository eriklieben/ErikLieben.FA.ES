namespace ErikLieben.FA.ES.CosmosDb;

/// <summary>
/// Indicates that a projection can be stored as JSON in Azure CosmosDB and specifies its container and optional connection name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class CosmosDbJsonProjectionAttribute(string container) : Attribute
{
    /// <summary>
    /// Gets the CosmosDB container name used to store or retrieve the projection JSON.
    /// </summary>
    public string Container { get; } = container;

    /// <summary>
    /// Gets or sets the partition key path for the projection document.
    /// Defaults to "/projectionName" if not specified.
    /// </summary>
    public string PartitionKeyPath { get; init; } = "/projectionName";

    /// <summary>
    /// Gets or sets the Azure CosmosDB client connection name.
    /// </summary>
    public string? Connection { get; init; }
}
