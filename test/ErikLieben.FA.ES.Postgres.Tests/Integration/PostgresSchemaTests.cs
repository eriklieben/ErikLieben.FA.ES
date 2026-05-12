using Npgsql;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresSchemaTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task Schema_install_is_idempotent()
    {
        // Running the bootstrapper a second time must not throw.
        await fixture.Bootstrapper.EnsureSchemaAsync();
        await fixture.Bootstrapper.EnsureSchemaAsync();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('faes_events','faes_documents','faes_snapshots','faes_projection_progress')",
            connection);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Faes_append_function_is_installed()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM pg_proc WHERE proname = 'faes_append'",
            connection);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }
}
