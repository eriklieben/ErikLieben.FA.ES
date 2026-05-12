// AOT smoke test: publish this with `dotnet publish -p:PublishAot=true` to verify the
// ErikLieben.FA.ES.Postgres provider compiles cleanly under NativeAOT (no trim/AOT warnings).
//
// Optional runtime exercise: set FAES_PG_CONNSTR to a reachable connection string
// (e.g. "Host=localhost;Port=5432;Username=faes;Password=faes;Database=faes_smoke") and the
// binary will bootstrap the schema, create a document, append events, and read them back.
// When the env var is unset, the program only touches the surface area enough to keep symbols
// alive for the AOT analyzer.

using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Postgres;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Processors;
using Microsoft.Extensions.DependencyInjection;

var connectionString = Environment.GetEnvironmentVariable("FAES_PG_CONNSTR");

var services = new ServiceCollection();

// 1. Core FAES wiring: composite factories + keyed-dictionary plumbing.
services.AddFaes(faes => faes.UseDefaultStorage("postgres"));

// Smoke-test stub for IAggregateFactory. Real consumers register concrete aggregate factories
// via the FAES builder; the AOT analyzer just needs the constructor signature exercised.
services.AddSingleton<IAggregateFactory, NoopAggregateFactory>();

// 2. Postgres-specific registrations (NpgsqlDataSource, keyed providers, etc.).
services.ConfigureNpgsqlEventStore(new EventStreamPostgresSettings
{
    ConnectionString = connectionString ?? "Host=127.0.0.1;Username=faes;Password=faes;Database=faes_smoke",
    AutoCreate = SchemaBootstrapMode.CreateOrUpdate,
});

await using var provider = services.BuildServiceProvider();

// Symbol-keep-alive: resolve everything the AOT analyzer needs to see exercised.
var bootstrapper          = provider.GetRequiredService<PostgresSchemaBootstrapper>();
var dataStore             = provider.GetRequiredService<PostgresDataStore>();
var snapshotStore         = provider.GetRequiredService<ISnapShotStore>();
var documentFactory       = provider.GetRequiredKeyedService<IObjectDocumentFactory>(ServiceCollectionExtensions.PostgresServiceKey);
var streamFactory         = provider.GetRequiredKeyedService<IEventStreamFactory>(ServiceCollectionExtensions.PostgresServiceKey);
var tagFactory            = provider.GetRequiredKeyedService<IDocumentTagDocumentFactory>(ServiceCollectionExtensions.PostgresServiceKey);
var objectIdProvider      = provider.GetRequiredKeyedService<IObjectIdProvider>(ServiceCollectionExtensions.PostgresServiceKey);
var healthCheck           = provider.GetRequiredService<ErikLieben.FA.ES.Postgres.HealthChecks.PostgresHealthCheck>();

Console.WriteLine("DI graph resolved.");
Console.WriteLine($"  bootstrapper:          {bootstrapper.GetType().Name}");
Console.WriteLine($"  dataStore:             {dataStore.GetType().Name}");
Console.WriteLine($"  snapshotStore:         {snapshotStore.GetType().Name}");
Console.WriteLine($"  objectDocumentFactory: {documentFactory.GetType().Name}");
Console.WriteLine($"  eventStreamFactory:    {streamFactory.GetType().Name}");
Console.WriteLine($"  documentTagFactory:    {tagFactory.GetType().Name}");
Console.WriteLine($"  objectIdProvider:      {objectIdProvider.GetType().Name}");
Console.WriteLine($"  healthCheck:           {healthCheck.GetType().Name}");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("FAES_PG_CONNSTR not set; skipping runtime exercise.");
    return 0;
}

await bootstrapper.EnsureSchemaAsync();
var doc = await documentFactory.GetOrCreateAsync("AotSmoke", "smoke-1");
await dataStore.AppendAsync(doc, CancellationToken.None,
    new JsonEvent { EventType = "Hello", EventVersion = 0, Payload = """{"msg":"hi"}""" });

var events = await dataStore.ReadAsync(doc);
var count = events?.Count() ?? 0;
Console.WriteLine($"Round-tripped {count} event(s) under NativeAOT.");
return 0;

internal sealed class NoopAggregateFactory : IAggregateFactory
{
    public IAggregateFactory<T>? GetFactory<T>() where T : IBase => null;
    public IAggregateCovarianceFactory<IBase>? GetFactory(Type type) => null;
}
