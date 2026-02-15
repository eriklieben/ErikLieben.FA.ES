using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.HealthChecks;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ErikLieben.FA.ES.AzureStorage.Tests.HealthChecks;

public class HealthCheckBuilderExtensionsTests
{
    public class AddBlobStorageHealthCheck
    {
        [Fact]
        public void Should_register_blob_storage_health_check_with_default_name()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddBlobStorageHealthCheck();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            Assert.Contains(options.Value.Registrations, r => r.Name == "azure-blob-storage");
        }

        [Fact]
        public void Should_register_blob_storage_health_check_with_custom_name()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddBlobStorageHealthCheck(name: "my-blob-check");
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            Assert.Contains(options.Value.Registrations, r => r.Name == "my-blob-check");
        }

        [Fact]
        public void Should_register_blob_storage_health_check_with_tags()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddBlobStorageHealthCheck(tags: ["storage", "azure"]);
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            var registration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            Assert.Contains("storage", registration.Tags);
            Assert.Contains("azure", registration.Tags);
        }

        [Fact]
        public void Should_register_blob_storage_health_check_with_failure_status()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddBlobStorageHealthCheck(failureStatus: HealthStatus.Degraded);
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            var registration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        }

        [Fact]
        public void Should_register_blob_storage_health_check_with_timeout()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();
            var timeout = TimeSpan.FromSeconds(10);

            // Act
            builder.AddBlobStorageHealthCheck(timeout: timeout);
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            var registration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            Assert.Equal(timeout, registration.Timeout);
        }

        [Fact]
        public void Should_create_health_check_instance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            var builder = services.AddHealthChecks();
            builder.AddBlobStorageHealthCheck();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Act
            var registration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            var healthCheck = registration.Factory(provider);

            // Assert
            Assert.IsType<BlobStorageHealthCheck>(healthCheck);
        }
    }

    public class AddTableStorageHealthCheck
    {
        [Fact]
        public void Should_register_table_storage_health_check_with_default_name()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddTableStorageHealthCheck();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            Assert.Contains(options.Value.Registrations, r => r.Name == "azure-table-storage");
        }

        [Fact]
        public void Should_register_table_storage_health_check_with_custom_name()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddTableStorageHealthCheck(name: "my-table-check");
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            Assert.Contains(options.Value.Registrations, r => r.Name == "my-table-check");
        }

        [Fact]
        public void Should_create_health_check_instance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();
            builder.AddTableStorageHealthCheck();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Act
            var registration = options.Value.Registrations.Single(r => r.Name == "azure-table-storage");
            var healthCheck = registration.Factory(provider);

            // Assert
            Assert.IsType<TableStorageHealthCheck>(healthCheck);
        }
    }

    public class AddAzureStorageHealthChecks
    {
        [Fact]
        public void Should_register_both_blob_and_table_health_checks()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddAzureStorageHealthChecks();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            Assert.Contains(options.Value.Registrations, r => r.Name == "azure-blob-storage");
            Assert.Contains(options.Value.Registrations, r => r.Name == "azure-table-storage");
        }

        [Fact]
        public void Should_apply_tags_to_both_health_checks()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddAzureStorageHealthChecks(tags: ["custom-tag"]);
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            var blobRegistration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            var tableRegistration = options.Value.Registrations.Single(r => r.Name == "azure-table-storage");
            Assert.Contains("custom-tag", blobRegistration.Tags);
            Assert.Contains("custom-tag", tableRegistration.Tags);
        }

        [Fact]
        public void Should_use_default_storage_tag_when_no_tags_provided()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();

            // Act
            builder.AddAzureStorageHealthChecks();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Assert
            var blobRegistration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            var tableRegistration = options.Value.Registrations.Single(r => r.Name == "azure-table-storage");
            Assert.Contains("storage", blobRegistration.Tags);
            Assert.Contains("storage", tableRegistration.Tags);
        }

        [Fact]
        public async Task Should_return_healthy_when_blob_services_not_configured()
        {
            // Arrange
            var services = new ServiceCollection();
            // Don't register blob services
            services.AddSingleton(Substitute.For<IAzureClientFactory<TableServiceClient>>());
            services.AddSingleton(new EventStreamTableSettings("Store"));
            var builder = services.AddHealthChecks();
            builder.AddAzureStorageHealthChecks();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Act
            var blobRegistration = options.Value.Registrations.Single(r => r.Name == "azure-blob-storage");
            var healthCheck = blobRegistration.Factory(provider);
            var context = new HealthCheckContext
            {
                Registration = blobRegistration
            };
            var result = await healthCheck.CheckHealthAsync(context);

            // Assert - NoOpHealthCheck returns Healthy with specific description
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("Storage provider not configured", result.Description);
        }

        [Fact]
        public async Task Should_return_healthy_when_table_services_not_configured()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IAzureClientFactory<BlobServiceClient>>());
            services.AddSingleton(new EventStreamBlobSettings("Store"));
            // Don't register table services
            var builder = services.AddHealthChecks();
            builder.AddAzureStorageHealthChecks();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

            // Act
            var tableRegistration = options.Value.Registrations.Single(r => r.Name == "azure-table-storage");
            var healthCheck = tableRegistration.Factory(provider);
            var context = new HealthCheckContext
            {
                Registration = tableRegistration
            };
            var result = await healthCheck.CheckHealthAsync(context);

            // Assert - NoOpHealthCheck returns Healthy with specific description
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("Storage provider not configured", result.Description);
        }
    }
}
