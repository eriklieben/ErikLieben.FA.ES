namespace ErikLieben.FA.ES.CLI.Model;

public record PostgresProjectionDefinition
{
    /// <summary>
    /// Explicit table name from the attribute, or <c>null</c> to derive from the projection class name.
    /// </summary>
    public string? Table { get; init; }

    /// <summary>
    /// Schema name; <c>null</c> means the default schema configured on the event store.
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// Named <c>NpgsqlDataSource</c> key; <c>null</c> means the default singleton data source.
    /// </summary>
    public string? Connection { get; init; }
}
