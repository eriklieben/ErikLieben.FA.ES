using Azure.Storage.Blobs;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.AzureStorage.HealthChecks;

/// <summary>
/// Health check for Azure Blob Storage connectivity.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the configured Azure Blob Storage account is accessible
/// and responsive. It performs a lightweight operation (get account info) to verify connectivity.
/// </para>
/// <para>
/// The check uses OpenTelemetry tracing for observability.
/// </para>
/// </remarks>
public class BlobStorageHealthCheck : IHealthCheck
{
    private readonly IAzureClientFactory<BlobServiceClient> _clientFactory;
    private readonly string _clientName;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageHealthCheck"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory for creating blob clients.</param>
    /// <param name="settings">The blob storage settings.</param>
    /// <param name="clientName">Optional client name for named clients. Defaults to "Store".</param>
    public BlobStorageHealthCheck(
        IAzureClientFactory<BlobServiceClient> clientFactory,
        EventStreamBlobSettings settings,
        string clientName = "Store")
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        _clientFactory = clientFactory;
        _clientName = clientName;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("BlobStorageHealthCheck");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureBlob);
            activity.SetTag(FaesSemanticConventions.StorageProvider, FaesSemanticConventions.StorageProviderBlob);
        }

        try
        {
            var client = _clientFactory.CreateClient(_clientName);

            // Perform a lightweight operation to verify connectivity
            var response = await client.GetAccountInfoAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "AccountKind", response.Value.AccountKind.ToString() },
                { "SkuName", response.Value.SkuName.ToString() }
            };

            activity?.SetTag(FaesSemanticConventions.Success, true);

            return HealthCheckResult.Healthy(
                description: "Azure Blob Storage is accessible",
                data: data);
        }
        catch (Exception ex)
        {
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);

            return HealthCheckResult.Unhealthy(
                description: $"Azure Blob Storage is not accessible: {ex.Message}",
                exception: ex);
        }
    }
}
