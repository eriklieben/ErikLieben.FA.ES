using ErikLieben.FA.ES.S3.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErikLieben.FA.ES.S3.HealthChecks;

/// <summary>
/// Extension methods for adding S3 storage health checks.
/// </summary>
public static class S3HealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a health check for S3-compatible storage.
    /// </summary>
    /// <param name="builder">The health check builder.</param>
    /// <param name="name">The name of the health check. Defaults to "s3-storage".</param>
    /// <param name="failureStatus">The failure status. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <param name="timeout">Optional timeout for the health check.</param>
    /// <returns>The health check builder.</returns>
    public static IHealthChecksBuilder AddS3StorageHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "s3-storage",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                var clientFactory = sp.GetRequiredService<IS3ClientFactory>();
                var settings = sp.GetRequiredService<EventStreamS3Settings>();
                return new S3StorageHealthCheck(clientFactory, settings);
            },
            failureStatus,
            tags,
            timeout));
    }
}
