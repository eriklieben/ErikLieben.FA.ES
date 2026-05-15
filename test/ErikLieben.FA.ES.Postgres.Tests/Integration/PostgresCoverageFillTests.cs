using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Postgres.Builder;
using ErikLieben.FA.ES.Postgres.Configuration;
using ErikLieben.FA.ES.Postgres.Exceptions;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ErikLieben.FA.ES.Postgres.Tests.Integration;

/// <summary>
/// Tests that fill remaining coverage gaps: tag-fan-out wrappers on the document store/factory,
/// event-stream factory construction path, snapshot bulk/non-generic paths, and the
/// PostgresSchemaException constructor surface.
/// </summary>
[Collection("Postgres")]
public class PostgresCoverageFillTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private PostgresDocumentStore documentStore = null!;
    private PostgresObjectDocumentFactory factory = null!;
    private PostgresDocumentTagStore documentTags = null!;
    private PostgresSnapShotStore snapshots = null!;
    private PostgresTagFactory tagFactory = null!;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetAsync();
        var typeSettings = new EventStreamDefaultTypeSettings("postgres");
        tagFactory = new PostgresTagFactory(fixture.DataSource);
        documentStore = new PostgresDocumentStore(
            fixture.DataSource, fixture.Settings, typeSettings, tagFactory, fixture.Bootstrapper);
        factory = new PostgresObjectDocumentFactory(documentStore);
        documentTags = new PostgresDocumentTagStore(fixture.DataSource);
        snapshots = new PostgresSnapShotStore(fixture.DataSource);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DocumentStore_GetByDocumentByTagAsync_returns_all_tagged_documents()
    {
        var a = await documentStore.CreateAsync("Order", "tag-doc-a");
        var b = await documentStore.CreateAsync("Order", "tag-doc-b");
        await documentTags.SetAsync(a, "shared");
        await documentTags.SetAsync(b, "shared");

        var docs = (await documentStore.GetByDocumentByTagAsync("Order", "shared")).ToList();
        Assert.Equal(2, docs.Count);
        Assert.Contains(docs, d => d.ObjectId == "tag-doc-a");
        Assert.Contains(docs, d => d.ObjectId == "tag-doc-b");
    }

    [Fact]
    public async Task DocumentStore_GetFirstByDocumentByTagAsync_returns_one_or_null()
    {
        var a = await documentStore.CreateAsync("Order", "tag-doc-c");
        await documentTags.SetAsync(a, "unique");

        var hit = await documentStore.GetFirstByDocumentByTagAsync("Order", "unique");
        Assert.NotNull(hit);
        Assert.Equal("tag-doc-c", hit!.ObjectId);

        var miss = await documentStore.GetFirstByDocumentByTagAsync("Order", "nonexistent");
        Assert.Null(miss);
    }

    [Fact]
    public async Task ObjectDocumentFactory_tag_lookup_overloads_delegate_correctly()
    {
        var a = await documentStore.CreateAsync("Order", "fac-tag-a");
        await documentTags.SetAsync(a, "alpha");

        var first = await factory.GetFirstByObjectDocumentTag("Order", "alpha");
        Assert.NotNull(first);
        Assert.Equal("fac-tag-a", first!.ObjectId);

        var all = (await factory.GetByObjectDocumentTag("Order", "alpha")).ToList();
        Assert.Single(all);
    }

    [Fact]
    public async Task EventStreamFactory_Create_returns_PostgresEventStream()
    {
        var doc = await documentStore.CreateAsync("Order", "stream-1");

        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var aggregateFactory = Substitute.For<IAggregateFactory>();

        var streamFactory = new PostgresEventStreamFactory(
            fixture.Settings,
            dataStore,
            snapshots,
            tagFactory,
            factory,
            aggregateFactory);

        var stream = streamFactory.Create(doc);
        Assert.NotNull(stream);
        Assert.IsType<PostgresEventStream>(stream);
        Assert.Equal(doc.Active.StreamIdentifier, stream.StreamIdentifier);
    }

    [Fact]
    public async Task SnapShotStore_DeleteManyAsync_removes_named_versions()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-bulk");
        for (var v = 0; v < 5; v++)
        {
            await snapshots.SetAsync(
                new SnapAggregate { Name = $"v{v}" }, SnapJsonContext.Default.SnapAggregate, doc, v);
        }

        var removed = await snapshots.DeleteManyAsync(doc, [0, 2, 4]);
        Assert.Equal(3, removed);

        var remaining = await snapshots.ListSnapshotsAsync(doc);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, m => m.Version == 1);
        Assert.Contains(remaining, m => m.Version == 3);
    }

    [Fact]
    public async Task SnapShotStore_DeleteManyAsync_with_empty_list_is_zero()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-empty");
        var removed = await snapshots.DeleteManyAsync(doc, []);
        Assert.Equal(0, removed);
    }

    [Fact]
    public async Task SnapShotStore_non_generic_GetAsync_returns_boxed_object()
    {
        var doc = await documentStore.CreateAsync("Order", "snap-boxed");
        await snapshots.SetAsync(
            new SnapAggregate { Name = "boxed", Count = 9 },
            SnapJsonContext.Default.SnapAggregate, doc, 0);

        JsonTypeInfo info = SnapJsonContext.Default.SnapAggregate;
        var loaded = await snapshots.GetAsync(info, doc, 0);

        Assert.NotNull(loaded);
        var snap = Assert.IsType<SnapAggregate>(loaded);
        Assert.Equal("boxed", snap.Name);
        Assert.Equal(9, snap.Count);
    }

    [Fact]
    public async Task ObjectDocumentFactory_GetOrCreate_GetAsync_SetAsync_round_trip()
    {
        var doc = await factory.GetOrCreateAsync("Order", "fac-rt");
        Assert.NotNull(doc);
        Assert.Equal("fac-rt", doc.ObjectId);

        var reloaded = await factory.GetAsync("Order", "fac-rt");
        Assert.Equal(doc.ObjectId, reloaded.ObjectId);

        reloaded.Active.CurrentStreamVersion = 7;
        await factory.SetAsync(reloaded);

        var again = await factory.GetAsync("Order", "fac-rt");
        Assert.Equal(7, again.Active.CurrentStreamVersion);
    }

    [Fact]
    public void Builder_WithPostgresProjectionStatusCoordinator_registers_coordinator()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IAggregateFactory, NoopAggregateFactory>();
        services.AddFaes(faes => faes
            .UseDefaultStorage("postgres")
            .UsePostgres(s => s.ConnectionString = fixture.ConnectionString)
            .WithPostgresProjectionStatusCoordinator());

        using var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<ErikLieben.FA.ES.Projections.IProjectionStatusCoordinator>();
        Assert.IsType<PostgresProjectionStatusCoordinator>(coordinator);
    }

    [Fact]
    public async Task DataStore_serializes_and_reads_all_optional_event_fields()
    {
        // Exercises the "schemaVersion > 1", "externalSequencer set", "metadata Count > 0",
        // "actionMetadata not null" branches on the serializer; and the non-null jsonb
        // read branches on the deserializer.
        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var doc = await documentStore.CreateAsync("Order", "branch-1");

        var richEvent = new JsonEvent
        {
            EventType = "Rich",
            EventVersion = 0,
            SchemaVersion = 3,
            ExternalSequencer = "seq-42",
            Payload = """{"x":1}""",
            ActionMetadata = new ActionMetadata { CorrelationId = "corr", CausationId = "cause" },
            Metadata = new Dictionary<string, string> { ["region"] = "eu", ["tier"] = "gold" },
        };

        await dataStore.AppendAsync(doc, CancellationToken.None, richEvent);

        var read = (await dataStore.ReadAsync(doc))!.Single();
        Assert.Equal("Rich", read.EventType);
        Assert.Equal(3, read.SchemaVersion);
        Assert.Equal("seq-42", read.ExternalSequencer);
        Assert.Equal("corr", read.ActionMetadata!.CorrelationId);
        Assert.Equal("eu", read.Metadata["region"]);
    }

    [Fact]
    public async Task DataStore_handles_empty_payload_and_reads_null_jsonb_columns()
    {
        // Empty payload → serializer writes "{}" (covers the IsNullOrEmpty(Payload) true branch).
        // Then we null the optional columns directly so the reader's IsDBNull branches fire.
        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var doc = await documentStore.CreateAsync("Order", "branch-2");

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "Empty", EventVersion = 0, Payload = "" });

        await using (var connection = await fixture.DataSource.OpenConnectionAsync())
        {
            await using var cmd = new Npgsql.NpgsqlCommand(
                "UPDATE faes_events SET correlation_id = NULL, causation_id = NULL, idempotent_key = NULL, " +
                "originated_from_user = NULL, event_occured_at = NULL, metadata = NULL, " +
                "schema_version = NULL, external_sequencer = NULL " +
                "WHERE object_name = 'Order' AND stream_id = $1 AND version = 0",
                connection);
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter<string> { TypedValue = doc.Active.StreamIdentifier });
            await cmd.ExecuteNonQueryAsync();
        }

        var read = (await dataStore.ReadAsync(doc))!.Single();
        Assert.Equal("Empty", read.EventType);
        Assert.Equal(1, read.SchemaVersion); // null schema_version maps to 1 (default)
        Assert.Null(read.ExternalSequencer);
        Assert.Empty(read.Metadata);
        Assert.NotNull(read.ActionMetadata);
    }

    [Fact]
    public async Task DataStore_round_trips_originated_from_user_and_event_occured_at()
    {
        // Covers ParseVersionToken on the read side and the NULLIF::timestamptz cast
        // in faes_append on the write side — both new in 2.0.0-preview.16.
        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var doc = await documentStore.CreateAsync("Order", "vt-doc");

        var occurred = new DateTimeOffset(2026, 5, 15, 12, 34, 56, TimeSpan.Zero);
        var token = new VersionToken("Order", "originator-1", "originator-1-0000000000", 7);
        var evt = new JsonEvent
        {
            EventType = "Rich",
            EventVersion = 0,
            Payload = """{"x":1}""",
            ActionMetadata = new ActionMetadata(
                CorrelationId: "corr",
                CausationId: "cause",
                IdempotentKey: "idem-1",
                OriginatedFromUser: token,
                EventOccuredAt: occurred),
        };

        await dataStore.AppendAsync(doc, CancellationToken.None, evt);

        var read = (await dataStore.ReadAsync(doc))!.Single();
        Assert.Equal("idem-1", read.ActionMetadata!.IdempotentKey);
        Assert.NotNull(read.ActionMetadata.OriginatedFromUser);
        Assert.Equal(token.Value, read.ActionMetadata.OriginatedFromUser!.Value);
        Assert.Equal(token.SchemaVersion, read.ActionMetadata.OriginatedFromUser.SchemaVersion);
        Assert.Equal(occurred, read.ActionMetadata.EventOccuredAt);
    }

    [Fact]
    public async Task DataStore_idempotent_key_unique_constraint_blocks_duplicate_on_same_aggregate()
    {
        // Covers ux_faes_events_idempotent_key. Two events with the same IdempotentKey
        // on the same (object_name, object_id) must violate the partial unique index.
        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var doc = await documentStore.CreateAsync("Order", "idem-doc");

        var first = new JsonEvent
        {
            EventType = "Created",
            EventVersion = 0,
            Payload = "{}",
            ActionMetadata = new ActionMetadata(IdempotentKey: "same-key"),
        };
        await dataStore.AppendAsync(doc, CancellationToken.None, first);

        var second = new JsonEvent
        {
            EventType = "Updated",
            EventVersion = 1,
            Payload = "{}",
            ActionMetadata = new ActionMetadata(IdempotentKey: "same-key"),
        };

        var ex = await Assert.ThrowsAsync<PostgresProcessingException>(
            () => dataStore.AppendAsync(doc, CancellationToken.None, second));
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DataStore_null_idempotent_keys_do_not_collide()
    {
        // Partial unique index has WHERE idempotent_key IS NOT NULL — multiple events
        // with no idempotent key on the same aggregate must coexist.
        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var doc = await documentStore.CreateAsync("Order", "null-idem-doc");

        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "A", EventVersion = 0, Payload = "{}" });
        await dataStore.AppendAsync(doc, CancellationToken.None,
            new JsonEvent { EventType = "B", EventVersion = 1, Payload = "{}" });

        var events = (await dataStore.ReadAsync(doc))!.ToList();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task DocumentStore_CreateAsync_sets_schema_version_to_2_0_0()
    {
        var doc = await documentStore.CreateAsync("Order", "schema-version-doc");
        Assert.Equal("2.0.0", doc.SchemaVersion);
    }

    [Fact]
    public async Task Bootstrapper_AutoCreate_None_is_a_noop()
    {
        var noBootstrap = new PostgresSchemaBootstrapper(
            fixture.DataSource,
            fixture.Settings with { AutoCreate = SchemaBootstrapMode.None });
        await noBootstrap.EnsureSchemaAsync();
        // Schema already exists from fixture; just exercising the early-return branch.
    }

    [Fact]
    public async Task Bootstrapper_partition_creation_is_idempotent_and_optional()
    {
        // Optional path: AutoCreatePartitionsPerObjectName = false → no-op.
        var noPart = new PostgresSchemaBootstrapper(
            fixture.DataSource,
            fixture.Settings with { AutoCreatePartitionsPerObjectName = false });
        await noPart.EnsurePartitionForObjectNameAsync("AnyName");

        // Enabled path: first call creates; second call hits the duplicate_table ignore branch.
        var withPart = fixture.Bootstrapper;
        await withPart.EnsurePartitionForObjectNameAsync("BranchAggregate");
        await withPart.EnsurePartitionForObjectNameAsync("BranchAggregate");
    }

    [Fact]
    public async Task ProjectionCoordinator_CancelRebuild_without_row_is_silent()
    {
        var coord = new PostgresProjectionStatusCoordinator(fixture.DataSource);
        // Token for a projection that was never started — Cancel should be a no-op,
        // exercising the "row is null" early-return branch.
        var orphan = RebuildToken.Create("Ghost", "nope", RebuildStrategy.BlockingWithCatchUp, TimeSpan.FromMinutes(1));
        await coord.CancelRebuildAsync(orphan, error: "n/a");
        var status = await coord.GetStatusAsync("Ghost", "nope");
        Assert.Null(status);
    }

    [Fact]
    public async Task ProjectionCoordinator_Enable_on_missing_row_is_noop()
    {
        var coord = new PostgresProjectionStatusCoordinator(fixture.DataSource);
        await coord.EnableAsync("Ghost", "missing");
        Assert.Null(await coord.GetStatusAsync("Ghost", "missing"));
    }

    [Fact]
    public async Task ProjectionCoordinator_CompleteRebuild_without_error_uses_completion_path()
    {
        var coord = new PostgresProjectionStatusCoordinator(fixture.DataSource);
        var token = await coord.StartRebuildAsync(
            "P-branch", "x",
            RebuildStrategy.BlueGreen,
            TimeSpan.FromMinutes(5));

        // Cancel without error → success branch (status → Active, rebuildInfo.WithCompletion).
        await coord.CancelRebuildAsync(token, error: null);
        var status = await coord.GetStatusAsync("P-branch", "x");
        Assert.Equal(ProjectionStatus.Active, status!.Status);
        Assert.NotNull(status.RebuildInfo!.CompletedAt);
        Assert.Null(status.RebuildInfo.Error);
    }

    [Fact]
    public async Task EventStreamFactory_rewrites_default_StreamType_to_settings_default()
    {
        var doc = await documentStore.CreateAsync("Order", "stream-default");
        doc.Active.StreamType = "default"; // trigger the rewrite branch

        var dataStore = new PostgresDataStore(fixture.DataSource, fixture.Settings);
        var sf = new PostgresEventStreamFactory(
            fixture.Settings, dataStore, snapshots, tagFactory, factory,
            Substitute.For<IAggregateFactory>());

        var stream = sf.Create(doc);
        Assert.NotNull(stream);
        Assert.Equal(fixture.Settings.DefaultDataStore, doc.Active.StreamType);
    }

    [Fact]
    public void PostgresSchemaException_constructors_cover_both_overloads()
    {
        var inner = new InvalidOperationException("inner");
        var withMessage = new PostgresSchemaException("schema went bang");
        Assert.Equal("schema went bang", withMessage.Message);
        Assert.Null(withMessage.InnerException);

        var withInner = new PostgresSchemaException("wrapped", inner);
        Assert.Equal("wrapped", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
    }
}
