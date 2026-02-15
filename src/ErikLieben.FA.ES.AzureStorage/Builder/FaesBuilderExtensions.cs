using Azure;
using ErikLieben.FA.ES.AzureStorage.Blob;
using ErikLieben.FA.ES.AzureStorage.Configuration;
using ErikLieben.FA.ES.AzureStorage.HealthChecks;
using ErikLieben.FA.ES.AzureStorage.Table;
using ErikLieben.FA.ES.Builder;
using ErikLieben.FA.ES.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.AzureStorage.Builder;

/// <summary>
/// Extension methods for configuring Azure Storage providers with the FAES builder.
/// </summary>
public static class FaesBuilderExtensions
{
    private const string BlobServiceKey = "blob";
    private const string TableServiceKey = "table";
    private const string DefaultClientName = "Store";
    private static bool _azureExceptionExtractorRegistered;

    /// <summary>
    /// Configures Azure Blob Storage as an event store provider.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="settings">The Blob storage settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseBlobStorage(this IFaesBuilder builder, EventStreamBlobSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        builder.Services.AddSingleton(settings);
        builder.Services.AddKeyedSingleton<IDocumentTagDocumentFactory, BlobTagFactory>(BlobServiceKey);
        builder.Services.AddKeyedSingleton<IObjectDocumentFactory, BlobObjectDocumentFactory>(BlobServiceKey);
        builder.Services.AddKeyedSingleton<IEventStreamFactory, BlobEventStreamFactory>(BlobServiceKey);
        builder.Services.AddKeyedSingleton<IObjectIdProvider, BlobObjectIdProvider>(BlobServiceKey);

        RegisterAzureExceptionExtractor();

        return builder;
    }

    /// <summary>
    /// Configures Azure Blob Storage with a configuration action.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="configure">Action to configure blob settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseBlobStorage(this IFaesBuilder builder, Action<EventStreamBlobSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new EventStreamBlobSettings(DefaultClientName);
        configure(settings);
        return builder.UseBlobStorage(settings);
    }

    /// <summary>
    /// Configures Azure Table Storage as an event store provider.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="settings">The Table storage settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseTableStorage(this IFaesBuilder builder, EventStreamTableSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        builder.Services.AddSingleton(settings);
        builder.Services.AddKeyedSingleton<IDocumentTagDocumentFactory, TableTagFactory>(TableServiceKey);
        builder.Services.AddKeyedSingleton<IObjectDocumentFactory, TableObjectDocumentFactory>(TableServiceKey);
        builder.Services.AddKeyedSingleton<IEventStreamFactory, TableEventStreamFactory>(TableServiceKey);
        builder.Services.AddKeyedSingleton<IObjectIdProvider, TableObjectIdProvider>(TableServiceKey);

        RegisterAzureExceptionExtractor();

        return builder;
    }

    /// <summary>
    /// Configures Azure Table Storage with a configuration action.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="configure">Action to configure table settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder UseTableStorage(this IFaesBuilder builder, Action<EventStreamTableSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new EventStreamTableSettings(DefaultClientName);
        configure(settings);
        return builder.UseTableStorage(settings);
    }

    /// <summary>
    /// Adds health checks for all configured Azure Storage providers.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="clientName">The named client name for Azure client factory. Defaults to "Store".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithAzureStorageHealthChecks(this IFaesBuilder builder, string clientName = DefaultClientName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddAzureStorageHealthChecks(clientName, ["faes", "storage"]);

        return builder;
    }

    /// <summary>
    /// Adds a health check for Azure Blob Storage.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="clientName">The named client name for Azure client factory. Defaults to "Store".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithBlobStorageHealthCheck(this IFaesBuilder builder, string clientName = DefaultClientName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddBlobStorageHealthCheck(clientName: clientName, tags: ["faes", "storage", "blob"]);

        return builder;
    }

    /// <summary>
    /// Adds a health check for Azure Table Storage.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="clientName">The named client name for Azure client factory. Defaults to "Store".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithTableStorageHealthCheck(this IFaesBuilder builder, string clientName = DefaultClientName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHealthChecks()
            .AddTableStorageHealthCheck(clientName: clientName, tags: ["faes", "storage", "table"]);

        return builder;
    }

    /// <summary>
    /// Registers the Blob Storage-backed projection status coordinator.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="containerName">The container name for projection status documents. Default: "projection-status".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithBlobProjectionStatusCoordinator(
        this IFaesBuilder builder,
        string containerName = "projection-status")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<Projections.IProjectionStatusCoordinator>(sp =>
        {
            var blobServiceClient = sp.GetRequiredService<Azure.Storage.Blobs.BlobServiceClient>();
            var logger = sp.GetService<ILogger<BlobProjectionStatusCoordinator>>();
            return new BlobProjectionStatusCoordinator(blobServiceClient, containerName, logger);
        });

        return builder;
    }

    /// <summary>
    /// Registers the Table Storage-backed projection status coordinator.
    /// </summary>
    /// <param name="builder">The FAES builder.</param>
    /// <param name="tableName">The table name for projection status entities. Default: "ProjectionStatus".</param>
    /// <returns>The builder for chaining.</returns>
    public static IFaesBuilder WithTableProjectionStatusCoordinator(
        this IFaesBuilder builder,
        string tableName = "ProjectionStatus")
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<Projections.IProjectionStatusCoordinator>(sp =>
        {
            var tableServiceClient = sp.GetRequiredService<Azure.Data.Tables.TableServiceClient>();
            var logger = sp.GetService<ILogger<TableProjectionStatusCoordinator>>();
            return new TableProjectionStatusCoordinator(tableServiceClient, tableName, logger);
        });

        return builder;
    }

    private static void RegisterAzureExceptionExtractor()
    {
        if (_azureExceptionExtractorRegistered)
        {
            return;
        }

        ResilientDataStore.RegisterStatusCodeExtractor(exception =>
        {
            if (exception is RequestFailedException requestFailedEx)
            {
                return requestFailedEx.Status;
            }
            return null;
        });

        _azureExceptionExtractorRegistered = true;
    }
}
