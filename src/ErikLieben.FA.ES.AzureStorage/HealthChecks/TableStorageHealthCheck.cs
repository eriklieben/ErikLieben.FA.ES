using Azure.Data.Tables;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.AzureStorage.HealthChecks;

/// <summary>
/// Health check for Azure Table Storage connectivity.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the configured Azure Table Storage account is accessible
/// and responsive. It performs a lightweight operation (get service properties) to verify connectivity.
/// </para>
/// <para>
/// The check uses OpenTelemetry tracing for observability.
/// </para>
/// </remarks>
public class TableStorageHealthCheck : IHealthCheck
{
    private readonly IAzureClientFactory<TableServiceClient> _clientFactory;
    private readonly EventStreamTableSettings _settings;
    private readonly string _clientName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStorageHealthCheck"/> class.
    /// </summary>
    /// <param name="clientFactory">The Azure client factory for creating table clients.</param>
    /// <param name="settings">The table storage settings.</param>
    /// <param name="clientName">Optional client name for named clients. Defaults to "Store".</param>
    public TableStorageHealthCheck(
        IAzureClientFactory<TableServiceClient> clientFactory,
        EventStreamTableSettings settings,
        string clientName = "Store")
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        _clientFactory = clientFactory;
        _settings = settings;
        _clientName = clientName;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("TableStorageHealthCheck");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, FaesSemanticConventions.DbSystemAzureTable);
            activity.SetTag(FaesSemanticConventions.StorageProvider, FaesSemanticConventions.StorageProviderTable);
        }

        try
        {
            var client = _clientFactory.CreateClient(_clientName);

            // Perform a lightweight operation to verify connectivity
            var response = await client.GetPropertiesAsync(cancellationToken);

            var data = new Dictionary<string, object>();

            if (response.Value.Logging != null)
            {
                data["LoggingEnabled"] = response.Value.Logging.Read || response.Value.Logging.Write || response.Value.Logging.Delete;
            }

            activity?.SetTag(FaesSemanticConventions.Success, true);

            return HealthCheckResult.Healthy(
                description: "Azure Table Storage is accessible",
                data: data);
        }
        catch (Exception ex)
        {
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);

            return HealthCheckResult.Unhealthy(
                description: $"Azure Table Storage is not accessible: {ex.Message}",
                exception: ex);
        }
    }
}
