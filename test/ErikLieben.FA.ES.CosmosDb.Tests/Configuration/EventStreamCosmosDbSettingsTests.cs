using ErikLieben.FA.ES.CosmosDb.Configuration;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Configuration;

public class EventStreamCosmosDbSettingsTests
{
    public class DefaultValues
    {
        [Fact]
        public void Should_have_default_database_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("eventstore", sut.DatabaseName);
        }

        [Fact]
        public void Should_have_default_documents_container_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("documents", sut.DocumentsContainerName);
        }

        [Fact]
        public void Should_have_default_events_container_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("events", sut.EventsContainerName);
        }

        [Fact]
        public void Should_have_default_snapshots_container_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("snapshots", sut.SnapshotsContainerName);
        }

        [Fact]
        public void Should_have_default_tags_container_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("tags", sut.TagsContainerName);
        }

        [Fact]
        public void Should_have_default_projections_container_name()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal("projections", sut.ProjectionsContainerName);
        }

        [Fact]
        public void Should_have_auto_create_containers_enabled_by_default()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.True(sut.AutoCreateContainers);
        }

        [Fact]
        public void Should_have_default_max_batch_size()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal(100, sut.MaxBatchSize);
        }

        [Fact]
        public void Should_have_optimistic_concurrency_enabled_by_default()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.True(sut.UseOptimisticConcurrency);
        }

        [Fact]
        public void Should_have_infinite_ttl_by_default()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.Equal(-1, sut.DefaultTimeToLiveSeconds);
        }

        [Fact]
        public void Should_have_bulk_execution_disabled_by_default()
        {
            var sut = new EventStreamCosmosDbSettings();
            Assert.False(sut.EnableBulkExecution);
        }
    }

    public class CustomValues
    {
        [Fact]
        public void Should_allow_custom_database_name()
        {
            var sut = new EventStreamCosmosDbSettings { DatabaseName = "custom-db" };
            Assert.Equal("custom-db", sut.DatabaseName);
        }

        [Fact]
        public void Should_allow_custom_ttl()
        {
            var sut = new EventStreamCosmosDbSettings { DefaultTimeToLiveSeconds = 3600 };
            Assert.Equal(3600, sut.DefaultTimeToLiveSeconds);
        }

        [Fact]
        public void Should_allow_custom_throughput_settings()
        {
            var sut = new EventStreamCosmosDbSettings
            {
                EventsThroughput = new ThroughputSettings { ManualThroughput = 1000 }
            };
            Assert.Equal(1000, sut.EventsThroughput!.ManualThroughput);
        }
    }
}

public class ThroughputSettingsTests
{
    [Fact]
    public void Should_allow_autoscale_throughput()
    {
        var sut = new ThroughputSettings { AutoscaleMaxThroughput = 4000 };
        Assert.Equal(4000, sut.AutoscaleMaxThroughput);
    }

    [Fact]
    public void Should_allow_manual_throughput()
    {
        var sut = new ThroughputSettings { ManualThroughput = 400 };
        Assert.Equal(400, sut.ManualThroughput);
    }

    [Fact]
    public void Should_have_null_values_by_default()
    {
        var sut = new ThroughputSettings();
        Assert.Null(sut.AutoscaleMaxThroughput);
        Assert.Null(sut.ManualThroughput);
    }
}
