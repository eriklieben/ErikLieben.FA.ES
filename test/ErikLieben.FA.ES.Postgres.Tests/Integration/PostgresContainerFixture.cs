using ErikLieben.FA.ES.Postgres;
using ErikLieben.FA.ES.Postgres.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

/// <summary>
/// Shared xUnit collection fixture starting a single Postgres container for the test run.
/// Each test class resets state by truncating tables in <see cref="ResetAsync"/>.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container;

    public PostgresContainerFixture()
    {
        container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("faes_test")
            .WithUsername("faes")
            .WithPassword("faes")
            .Build();
    }

    public string ConnectionString => container.GetConnectionString();
    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public EventStreamPostgresSettings Settings { get; private set; } = null!;
    public PostgresSchemaBootstrapper Bootstrapper { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        Settings = new EventStreamPostgresSettings
        {
            ConnectionString = ConnectionString,
            AutoCreate = SchemaBootstrapMode.CreateOrUpdate,
            AutoCreatePartitionsPerObjectName = true,
        };

        var builder = new NpgsqlDataSourceBuilder(ConnectionString);
        DataSource = builder.Build();

        Bootstrapper = new PostgresSchemaBootstrapper(DataSource, Settings);
        await Bootstrapper.EnsureSchemaAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }
        await container.DisposeAsync();
    }

    /// <summary>
    /// Truncates all faes_* tables and resets the global event sequence. Call from test setup
    /// for isolation between tests within a class that shares this collection fixture.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "TRUNCATE faes_events, faes_documents, faes_snapshots, faes_projection_progress, faes_projection_status RESTART IDENTITY CASCADE",
            connection);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
}
