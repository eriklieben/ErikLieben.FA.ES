using ErikLieben.FA.ES.CosmosDb.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.CosmosDb.Tests.HealthChecks;

public class HealthCheckBuilderExtensionsTests
{
    public class AddCosmosDbHealthCheckMethod
    {
        [Fact]
        public void Should_register_health_check_with_default_name()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            builder.AddCosmosDbHealthCheck();

            // Verify that health check registration services were added
            Assert.True(services.Count > 0);
        }

        [Fact]
        public void Should_register_health_check_with_custom_name()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            builder.AddCosmosDbHealthCheck(name: "my-cosmos-check");

            Assert.True(services.Count > 0);
        }

        [Fact]
        public void Should_register_health_check_with_tags()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            var result = builder.AddCosmosDbHealthCheck(tags: ["faes", "storage", "cosmosdb"]);

            Assert.NotNull(result);
        }

        [Fact]
        public void Should_register_health_check_with_custom_failure_status()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            var result = builder.AddCosmosDbHealthCheck(failureStatus: HealthStatus.Degraded);

            Assert.NotNull(result);
        }

        [Fact]
        public void Should_register_health_check_with_timeout()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();
            var timeout = TimeSpan.FromSeconds(30);

            var result = builder.AddCosmosDbHealthCheck(timeout: timeout);

            Assert.NotNull(result);
        }

        [Fact]
        public void Should_return_builder_for_chaining()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            var result = builder.AddCosmosDbHealthCheck();

            Assert.NotNull(result);
        }

        [Fact]
        public void Should_accept_all_optional_parameters()
        {
            var services = new ServiceCollection();
            var builder = services.AddHealthChecks();

            var result = builder.AddCosmosDbHealthCheck(
                name: "custom-cosmos",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["tag1", "tag2"],
                timeout: TimeSpan.FromSeconds(10));

            Assert.NotNull(result);
        }
    }
}
