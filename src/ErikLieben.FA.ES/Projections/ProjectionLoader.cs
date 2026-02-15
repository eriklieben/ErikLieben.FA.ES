using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Default implementation of <see cref="IProjectionLoader"/> that provides
/// version-aware projection loading with schema mismatch handling.
/// </summary>
public class ProjectionLoader : IProjectionLoader
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectionStatusCoordinator _statusCoordinator;
    private readonly ProjectionOptions _options;
    private readonly ILogger<ProjectionLoader>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionLoader"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving projection factories.</param>
    /// <param name="statusCoordinator">The projection status coordinator.</param>
    /// <param name="options">The projection options.</param>
    /// <param name="logger">Optional logger.</param>
    public ProjectionLoader(
        IServiceProvider serviceProvider,
        IProjectionStatusCoordinator statusCoordinator,
        IOptions<ProjectionOptions>? options = null,
        ILogger<ProjectionLoader>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _statusCoordinator = statusCoordinator ?? throw new ArgumentNullException(nameof(statusCoordinator));
        _options = options?.Value ?? new ProjectionOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string objectId, CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("ProjectionLoader.Get");

        var factory = _serviceProvider.GetRequiredService<IProjectionFactory<T>>();
        var documentFactory = _serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = _serviceProvider.GetRequiredService<IEventStreamFactory>();

        var exists = await factory.ExistsAsync(cancellationToken: cancellationToken);
        if (!exists)
        {
            return null;
        }

        return await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetVersionAsync<T>(string objectId, int version, CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("ProjectionLoader.GetVersion");

        var factory = _serviceProvider.GetRequiredService<IProjectionFactory<T>>();
        var documentFactory = _serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = _serviceProvider.GetRequiredService<IEventStreamFactory>();

        var blobName = GetVersionedBlobName<T>(version);

        var exists = await factory.ExistsAsync(blobName, cancellationToken);
        if (!exists)
        {
            return null;
        }

        return await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, blobName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProjectionVersionMetadata> GetVersionMetadataAsync<T>(
        string objectId,
        CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("ProjectionLoader.GetVersionMetadata");

        var projectionName = typeof(T).Name;
        var status = await _statusCoordinator.GetStatusAsync(projectionName, objectId, cancellationToken);

        var activeVersion = status?.SchemaVersion ?? 1;
        int? rebuildingVersion = null;
        var versions = new List<VersionInfo>();

        if (status != null)
        {
            versions.Add(new VersionInfo(
                status.SchemaVersion,
                status.Status,
                null,
                status.StatusChangedAt ?? DateTimeOffset.MinValue));

            if (status.Status.IsRebuilding() && status.RebuildInfo != null)
            {
                rebuildingVersion = status.SchemaVersion + 1;
                versions.Add(new VersionInfo(
                    rebuildingVersion.Value,
                    ProjectionStatus.Rebuilding,
                    null,
                    status.RebuildInfo.StartedAt));
            }
        }
        else
        {
            versions.Add(new VersionInfo(1, ProjectionStatus.Active, null, DateTimeOffset.MinValue));
        }

        return new ProjectionVersionMetadata(activeVersion, rebuildingVersion, versions);
    }

    /// <inheritdoc />
    public async Task<ProjectionLoadResult<T>> GetWithVersionCheckAsync<T>(
        string objectId,
        CancellationToken cancellationToken = default)
        where T : Projection
    {
        using var activity = FaesInstrumentation.Projections.StartActivity("ProjectionLoader.GetWithVersionCheck");

        var factory = _serviceProvider.GetRequiredService<IProjectionFactory<T>>();
        var documentFactory = _serviceProvider.GetRequiredService<IObjectDocumentFactory>();
        var eventStreamFactory = _serviceProvider.GetRequiredService<IEventStreamFactory>();

        var exists = await factory.ExistsAsync(cancellationToken: cancellationToken);
        if (!exists)
        {
            // Determine code schema version from a new instance
            var tempInstance = Activator.CreateInstance<T>();
            return ProjectionLoadResult<T>.NotFound(tempInstance.CodeSchemaVersion);
        }

        var projection = await factory.GetOrCreateAsync(documentFactory, eventStreamFactory, cancellationToken: cancellationToken);

        if (projection.NeedsSchemaUpgrade)
        {
            _logger?.LogWarning(
                "Schema mismatch for {ProjectionType}: stored v{StoredVersion}, code v{CodeVersion}",
                typeof(T).Name, projection.SchemaVersion, projection.CodeSchemaVersion);

            switch (_options.SchemaMismatchBehavior)
            {
                case SchemaMismatchBehavior.Throw:
                    throw new InvalidOperationException(
                        $"Schema version mismatch for {typeof(T).Name}: stored version {projection.SchemaVersion}, " +
                        $"code version {projection.CodeSchemaVersion}. A rebuild is required.");

                case SchemaMismatchBehavior.AutoRebuild:
                    _logger?.LogInformation(
                        "Auto-rebuild triggered for {ProjectionType} due to schema mismatch",
                        typeof(T).Name);
                    var projectionName = typeof(T).Name;
                    await _statusCoordinator.StartRebuildAsync(
                        projectionName,
                        objectId,
                        _options.DefaultRebuildStrategy,
                        _options.RebuildTimeout,
                        cancellationToken);
                    break;

                case SchemaMismatchBehavior.Warn:
                default:
                    break;
            }
        }

        return ProjectionLoadResult<T>.Success(projection);
    }

    private static string GetVersionedBlobName<T>(int version) where T : Projection
    {
        var name = typeof(T).Name;
        return $"{name}_v{version}";
    }
}
