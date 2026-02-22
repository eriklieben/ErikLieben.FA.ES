using Amazon.S3;
using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.EventStream;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.S3.Builder;

/// <summary>
/// Extension methods for configuring S3-compatible storage providers with the FAES builder.
/// </summary>
public static class FaesBuilderExtensions
{
    private const string S3ServiceKey = "s3";
    private static bool _s3ExceptionExtractorRegistered;

    /// <summary>
    /// Configures S3-compatible storage as an event store provider.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="settings">The S3 storage settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseS3Storage(this IFaesBuilder builder, EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IS3ClientFactory, S3ClientFactory>();
        builder.Services.AddKeyedSingleton<IDocumentTagDocumentFactory, S3TagFactory>(S3ServiceKey);
        builder.Services.AddKeyedSingleton<IObjectDocumentFactory, S3ObjectDocumentFactory>(S3ServiceKey);
        builder.Services.AddKeyedSingleton<IEventStreamFactory, S3EventStreamFactory>(S3ServiceKey);
        builder.Services.AddKeyedSingleton<IObjectIdProvider, S3ObjectIdProvider>(S3ServiceKey);

        RegisterS3ExceptionExtractor();

        return builder;
    }

    /// <summary>
    /// Configures S3-compatible storage with a configuration action.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="configure">Action to configure S3 settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseS3Storage(this IFaesBuilder builder, Action<EventStreamS3Settings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new EventStreamS3Settings("s3");
        configure(settings);
        return builder.UseS3Storage(settings);
    }

    /// <summary>
    /// Adds a health check for S3-compatible storage.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithS3HealthCheck(this IFaesBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddS3StorageHealthCheck(tags: ["faes", "storage", "s3"]);

        return builder;
    }

    /// <summary>
    /// Registers the S3-based implementations for document tags, documents, and event streams using the provided settings.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="settings">The S3 settings controlling buckets, chunking, and defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance to allow chaining.</returns>
    public static IServiceCollection ConfigureS3EventStore(this IServiceCollection services, EventStreamS3Settings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddSingleton(settings);
        services.AddSingleton<IS3ClientFactory, S3ClientFactory>();
        services.AddKeyedSingleton<IDocumentTagDocumentFactory, S3TagFactory>(S3ServiceKey);
        services.AddKeyedSingleton<IObjectDocumentFactory, S3ObjectDocumentFactory>(S3ServiceKey);
        services.AddKeyedSingleton<IEventStreamFactory, S3EventStreamFactory>(S3ServiceKey);
        services.AddKeyedSingleton<IObjectIdProvider, S3ObjectIdProvider>(S3ServiceKey);

        RegisterS3ExceptionExtractor();

        return services;
    }

    /// <summary>
    /// Registers the S3-backed projection status coordinator.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="prefix">The key prefix for projection status documents. Default: "projection-status".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithS3ProjectionStatusCoordinator(
        this IFaesBuilder builder,
        string prefix = "projection-status")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IProjectionStatusCoordinator>(sp =>
        {
            var clientFactory = sp.GetRequiredService<IS3ClientFactory>();
            var settings = sp.GetRequiredService<EventStreamS3Settings>();
            var logger = sp.GetService<ILogger<S3ProjectionStatusCoordinator>>();
            return new S3ProjectionStatusCoordinator(clientFactory, settings, prefix, logger);
        });

        return builder;
    }

    /// <summary>
    /// Registers the S3 <see cref="AmazonS3Exception"/> status code extractor with <see cref="ResilientDataStore"/>.
    /// This enables proper retry handling for S3-specific transient errors (throttling, etc.).
    /// </summary>
    /// <remarks>
    /// This method is automatically called by <see cref="UseS3Storage(IFaesBuilder, EventStreamS3Settings)"/> and
    /// <see cref="ConfigureS3EventStore"/> but can be called explicitly if you're
    /// configuring S3 storage services manually without those extension methods.
    /// </remarks>
    private static void RegisterS3ExceptionExtractor()
    {
        if (_s3ExceptionExtractorRegistered)
        {
            return;
        }

        ResilientDataStore.RegisterStatusCodeExtractor(exception =>
        {
            if (exception is AmazonS3Exception s3Ex)
            {
                return (int)s3Ex.StatusCode;
            }
            return null;
        });

        _s3ExceptionExtractorRegistered = true;
    }
}
