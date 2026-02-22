namespace ErikLieben.FA.ES.AzureStorage.Table;

/// <summary>
/// Marks a projection class for storage in Azure Table Storage.
/// Each event is stored as a separate table row, enabling query-optimized projections.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TableProjectionAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the Azure Table where projection data is stored.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets or sets the name of the Azure client connection.
    /// When null, uses the default connection.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the table should be created automatically if it doesn't exist.
    /// Defaults to true.
    /// </summary>
    public bool AutoCreateTable { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableProjectionAttribute"/> class.
    /// </summary>
    /// <param name="tableName">The name of the Azure Table where projection data is stored.</param>
    public TableProjectionAttribute(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        TableName = tableName;
    }
}
