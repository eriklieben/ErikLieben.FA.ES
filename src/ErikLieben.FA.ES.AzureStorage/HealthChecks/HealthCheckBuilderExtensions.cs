using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.AzureStorage.HealthChecks;

/// <summary>
/// Extension methods for adding Azure Storage health checks.
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a health check for Azure Blob Storage.
    /// </summary>
    /// <param name="builder">The health check builder.</param>
    /// <param name="name">The name of the health check. Defaults to "azure-blob-storage".</param>
    /// <param name="clientName">The named client name registered with Azure client factory. Defaults to "Store".</param>
    /// <param name="failureStatus">The failure status. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="timeout">Optional timeout for the health check.</param>
    /// <returns>The health check builder.</returns>
    public static IHealthChecksBuilder AddBlobStorageHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-blob-storage",
        string clientName = "Store",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                var clientFactory = sp.GetRequiredService<IAzureClientFactory<BlobServiceClient>>();
                var settings = sp.GetRequiredService<EventStreamBlobSettings>();
                return new BlobStorageHealthCheck(clientFactory, settings, clientName);
            },
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Adds a health check for Azure Table Storage.
    /// </summary>
    /// <param name="builder">The health check builder.</param>
    /// <param name="name">The name of the health check. Defaults to "azure-table-storage".</param>
    /// <param name="clientName">The named client name registered with Azure client factory. Defaults to "Store".</param>
    /// <param name="failureStatus">The failure status. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="timeout">Optional timeout for the health check.</param>
    /// <returns>The health check builder.</returns>
    public static IHealthChecksBuilder AddTableStorageHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "azure-table-storage",
        string clientName = "Store",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                var clientFactory = sp.GetRequiredService<IAzureClientFactory<TableServiceClient>>();
                var settings = sp.GetRequiredService<EventStreamTableSettings>();
                return new TableStorageHealthCheck(clientFactory, settings, clientName);
            },
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Adds health checks for all configured Azure Storage providers (Blob and Table).
    /// </summary>
    /// <param name="builder">The health check builder.</param>
    /// <param name="clientName">The named client name registered with Azure client factory. Defaults to "Store".</param>
    /// <param name="tags">Optional tags for the health checks.</param>
    /// <returns>The health check builder.</returns>
    /// <remarks>
    /// This method attempts to add health checks for both Blob and Table storage.
    /// If a required service is not registered (e.g., settings or client factory),
    /// that health check is skipped.
    /// </remarks>
    public static IHealthChecksBuilder AddAzureStorageHealthChecks(
        this IHealthChecksBuilder builder,
        string clientName = "Store",
        IEnumerable<string>? tags = null)
    {
        var tagsList = tags?.ToList() ?? ["storage"];

        builder.Add(new HealthCheckRegistration(
            "azure-blob-storage",
            sp =>
            {
                var clientFactory = sp.GetService<IAzureClientFactory<BlobServiceClient>>();
                var settings = sp.GetService<EventStreamBlobSettings>();

                if (clientFactory != null && settings != null)
                {
                    return new BlobStorageHealthCheck(clientFactory, settings, clientName);
                }

                // Return a no-op health check if services are not configured
                return new NoOpHealthCheck();
            },
            null,
            tagsList));

        builder.Add(new HealthCheckRegistration(
            "azure-table-storage",
            sp =>
            {
                var clientFactory = sp.GetService<IAzureClientFactory<TableServiceClient>>();
                var settings = sp.GetService<EventStreamTableSettings>();

                if (clientFactory != null && settings != null)
                {
                    return new TableStorageHealthCheck(clientFactory, settings, clientName);
                }

                // Return a no-op health check if services are not configured
                return new NoOpHealthCheck();
            },
            null,
            tagsList));

        return builder;
    }
}

/// <summary>
/// A no-op health check that always returns healthy.
/// Used when storage services are not configured.
/// </summary>
internal class NoOpHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Storage provider not configured"));
    }
}
