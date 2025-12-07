using ErikLieben.FA.ES.CosmosDb.Configuration;
using Xunit;

namespace ErikLieben.FA.ES.CosmosDb.Tests.Configuration;

public class EventStreamCosmosDbSettingsTests
{
    public class DefaultValues
    {
        [Fact]
        public void ProjectionsContainerName_should_have_default_value()
        {
            // Arrange & Act
            var sut = new EventStreamCosmosDbSettings();

            // Assert
            Assert.Equal("projections", sut.ProjectionsContainerName);
        }

        [Fact]
        public void ProjectionsThroughput_should_be_null_by_default()
        {
            // Arrange & Act
            var sut = new EventStreamCosmosDbSettings();

            // Assert
            Assert.Null(sut.ProjectionsThroughput);
        }
    }

    public class CustomValues
    {
        [Fact]
        public void Should_allow_custom_projections_container_name()
        {
            // Arrange & Act
            var sut = new EventStreamCosmosDbSettings
            {
                ProjectionsContainerName = "custom-projections"
            };

            // Assert
            Assert.Equal("custom-projections", sut.ProjectionsContainerName);
        }

        [Fact]
        public void Should_allow_custom_projections_throughput()
        {
            // Arrange
            var throughput = new ThroughputSettings
            {
                ManualThroughput = 400
            };

            // Act
            var sut = new EventStreamCosmosDbSettings
            {
                ProjectionsThroughput = throughput
            };

            // Assert
            Assert.NotNull(sut.ProjectionsThroughput);
            Assert.Equal(400, sut.ProjectionsThroughput.ManualThroughput);
        }

        [Fact]
        public void Should_allow_autoscale_projections_throughput()
        {
            // Arrange
            var throughput = new ThroughputSettings
            {
                AutoscaleMaxThroughput = 4000
            };

            // Act
            var sut = new EventStreamCosmosDbSettings
            {
                ProjectionsThroughput = throughput
            };

            // Assert
            Assert.NotNull(sut.ProjectionsThroughput);
            Assert.Equal(4000, sut.ProjectionsThroughput.AutoscaleMaxThroughput);
        }
    }
}
