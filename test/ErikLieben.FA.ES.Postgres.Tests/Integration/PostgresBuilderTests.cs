using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Postgres.Builder;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

[Collection("Postgres")]
public class PostgresBuilderTests(PostgresContainerFixture fixture)
{
    [Fact]
    public void UsePostgres_with_settings_wires_the_full_provider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAggregateFactory, NoopAggregateFactory>();
        services.AddFaes(faes => faes
            .UseDefaultStorage("postgres")
            .UsePostgres(new EventStreamPostgresSettings
            {
                ConnectionString = fixture.ConnectionString,
            })
            .WithPostgresStreamMetadataProvider());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<EventStreamPostgresSettings>());
        Assert.NotNull(provider.GetRequiredKeyedService<IObjectDocumentFactory>("postgres"));
        Assert.NotNull(provider.GetRequiredKeyedService<IEventStreamFactory>("postgres"));
        Assert.NotNull(provider.GetRequiredKeyedService<IDocumentTagDocumentFactory>("postgres"));
        Assert.NotNull(provider.GetRequiredKeyedService<IObjectIdProvider>("postgres"));
        Assert.IsType<PostgresStreamMetadataProvider>(provider.GetRequiredService<IStreamMetadataProvider>());
    }

    [Fact]
    public async Task WithPostgresHealthCheck_resolves_and_reports_healthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAggregateFactory, NoopAggregateFactory>();
        services.AddFaes(faes => faes
            .UseDefaultStorage("postgres")
            .UsePostgres(s => s.ConnectionString = fixture.ConnectionString)
            .WithPostgresHealthCheck());

        using var provider = services.BuildServiceProvider();
        var hc = provider.GetRequiredService<HealthCheckService>();

        var report = await hc.CheckHealthAsync();
        var postgresEntry = report.Entries.Single(e => e.Key == "postgres");
        Assert.Equal(HealthStatus.Healthy, postgresEntry.Value.Status);
    }
}

internal sealed class NoopAggregateFactory : IAggregateFactory
{
    public IAggregateFactory<T>? GetFactory<T>() where T : IBase => null;
    public IAggregateCovarianceFactory<IBase>? GetFactory(Type type) => null;
}
