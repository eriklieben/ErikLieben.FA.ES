using Amazon.S3;
using ErikLieben.FA.ES.Observability;
using ErikLieben.FA.ES.S3.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.S3.HealthChecks;

/// <summary>
/// Health check for S3-compatible storage connectivity.
/// </summary>
/// <remarks>
/// <para>
/// This health check verifies that the configured S3-compatible storage is accessible
/// and responsive. It performs a lightweight operation (list buckets) to verify connectivity.
/// </para>
/// <para>
/// The check uses OpenTelemetry tracing for observability.
/// </para>
/// </remarks>
public class S3StorageHealthCheck : IHealthCheck
{
    private readonly IS3ClientFactory _clientFactory;
    private readonly EventStreamS3Settings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3StorageHealthCheck"/> class.
    /// </summary>
    /// <param name="clientFactory">The S3 client factory for creating S3 clients.</param>
    /// <param name="settings">The S3 storage settings.</param>
    public S3StorageHealthCheck(
        IS3ClientFactory clientFactory,
        EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(settings);

        _clientFactory = clientFactory;
        _settings = settings;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = FaesInstrumentation.Storage.StartActivity("S3StorageHealthCheck");
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(FaesSemanticConventions.DbSystem, "s3");
            activity.SetTag(FaesSemanticConventions.StorageProvider, "s3");
        }

        try
        {
            var client = _clientFactory.CreateClient(_settings.DefaultDataStore);

            // Perform a lightweight operation to verify connectivity
            var response = await client.ListBucketsAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "BucketCount", response.Buckets.Count }
            };

            activity?.SetTag(FaesSemanticConventions.Success, true);

            return HealthCheckResult.Healthy(
                description: "S3 storage is accessible",
                data: data);
        }
        catch (Exception ex)
        {
            FaesInstrumentation.RecordException(activity, ex);
            activity?.SetTag(FaesSemanticConventions.Success, false);

            return HealthCheckResult.Unhealthy(
                description: $"S3 storage is not accessible: {ex.Message}",
                exception: ex);
        }
    }
}
