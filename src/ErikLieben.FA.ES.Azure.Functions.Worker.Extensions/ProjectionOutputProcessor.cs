using System.Diagnostics;
using System.Reflection;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Processes projection output attributes by loading, updating, and saving projections.
/// This class contains the core logic extracted from <see cref="ProjectionOutputMiddleware"/>
/// to enable unit testing without Azure Functions infrastructure dependencies.
/// </summary>
public class ProjectionOutputProcessor
{
    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");

    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectDocumentFactory _objectDocumentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionOutputProcessor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving projection factories.</param>
    /// <param name="objectDocumentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    /// <param name="logger">The logger.</param>
    public ProjectionOutputProcessor(
        IServiceProvider serviceProvider,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory,
        ILogger logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _objectDocumentFactory = objectDocumentFactory ?? throw new ArgumentNullException(nameof(objectDocumentFactory));
        _eventStreamFactory = eventStreamFactory ?? throw new ArgumentNullException(nameof(eventStreamFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the specified projection output attributes by loading, updating, and optionally saving each projection.
    /// </summary>
    /// <param name="outputAttributes">The projection output attributes to process.</param>
    /// <param name="functionName">The name of the function being processed (for logging).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AggregateException">Thrown when one or more projections fail to update or save.</exception>
    public async Task ProcessProjectionOutputsAsync(
        IReadOnlyList<ProjectionOutputAttribute> outputAttributes,
        string functionName,
        CancellationToken cancellationToken = default)
    {
        if (outputAttributes == null || outputAttributes.Count == 0)
        {
            return;
        }

        using var activity = ActivitySource.StartActivity($"ProjectionOutputProcessor.{nameof(ProcessProjectionOutputsAsync)}");

        _logger.LogDebug("Processing {Count} projection output(s) for function {FunctionName}",
            outputAttributes.Count, functionName);

        var exceptions = new List<Exception>();
        var updatedProjections = new List<(Projection projection, ProjectionOutputAttribute attribute)>();

        // Load and update all projections
        foreach (var attribute in outputAttributes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var projection = await LoadAndUpdateProjectionAsync(attribute);
                if (projection != null)
                {
                    updatedProjections.Add((projection, attribute));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update projection {ProjectionType}", attribute.ProjectionType.Name);
                exceptions.Add(new InvalidOperationException(
                    $"Failed to update projection '{attribute.ProjectionType.Name}': {ex.Message}", ex));
            }
        }

        // If any updates failed, throw before saving
        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to update {exceptions.Count} projection(s). No projections were saved.",
                exceptions);
        }

        // Save all projections that have SaveAfterUpdate enabled
        foreach (var (projection, attribute) in updatedProjections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attribute.SaveAfterUpdate)
            {
                try
                {
                    await SaveProjectionAsync(projection, attribute);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save projection {ProjectionType}", attribute.ProjectionType.Name);
                    exceptions.Add(new InvalidOperationException(
                        $"Failed to save projection '{attribute.ProjectionType.Name}': {ex.Message}", ex));
                }
            }
        }

        // If any saves failed, throw
        if (exceptions.Count > 0)
        {
            throw new AggregateException(
                $"Failed to save {exceptions.Count} projection(s) after update.",
                exceptions);
        }

        _logger.LogDebug("Successfully updated and saved {Count} projection(s)", updatedProjections.Count);
    }

    /// <summary>
    /// Loads and updates a projection to its latest version.
    /// </summary>
    /// <param name="attribute">The projection output attribute.</param>
    /// <returns>The updated projection, or null if no factory was found.</returns>
    public async Task<Projection?> LoadAndUpdateProjectionAsync(ProjectionOutputAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        using var activity = ActivitySource.StartActivity($"ProjectionOutputProcessor.LoadAndUpdate.{attribute.ProjectionType.Name}");

        var projection = await GetProjectionAsync(attribute);

        if (projection == null)
        {
            throw new InvalidOperationException(
                $"No projection factory registered for type '{attribute.ProjectionType.Name}'. " +
                $"Register IProjectionFactory<{attribute.ProjectionType.Name}> or IProjectionFactory in the service collection.");
        }

        _logger.LogDebug("Updating projection {ProjectionType} to latest version", attribute.ProjectionType.Name);

        await projection.UpdateToLatestVersion();

        return projection;
    }

    /// <summary>
    /// Gets or creates a projection using the registered factory.
    /// </summary>
    /// <param name="attribute">The projection output attribute.</param>
    /// <returns>The projection, or null if no factory was found.</returns>
    internal async Task<Projection?> GetProjectionAsync(ProjectionOutputAttribute attribute)
    {
        // Try to get the generic factory first
        var factoryType = typeof(IProjectionFactory<>).MakeGenericType(attribute.ProjectionType);
        var factory = _serviceProvider.GetService(factoryType);

        if (factory is IProjectionFactory projectionFactory)
        {
            return await projectionFactory.GetOrCreateProjectionAsync(
                _objectDocumentFactory,
                _eventStreamFactory,
                attribute.BlobName);
        }

        // Fall back to looking for IProjectionFactory implementations
        var factories = _serviceProvider.GetServices<IProjectionFactory>();
        var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == attribute.ProjectionType);

        if (matchingFactory != null)
        {
            return await matchingFactory.GetOrCreateProjectionAsync(
                _objectDocumentFactory,
                _eventStreamFactory,
                attribute.BlobName);
        }

        return null;
    }

    /// <summary>
    /// Saves a projection using the registered factory.
    /// </summary>
    /// <param name="projection">The projection to save.</param>
    /// <param name="attribute">The projection output attribute.</param>
    public async Task SaveProjectionAsync(Projection projection, ProjectionOutputAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(attribute);

        using var activity = ActivitySource.StartActivity($"ProjectionOutputProcessor.Save.{attribute.ProjectionType.Name}");

        var factory = GetProjectionFactory(attribute.ProjectionType);

        if (factory == null)
        {
            throw new InvalidOperationException(
                $"No projection factory found to save projection '{attribute.ProjectionType.Name}'.");
        }

        // Use reflection to call SaveAsync on the generic factory
        var saveMethod = factory.GetType().GetMethod("SaveAsync");
        if (saveMethod != null)
        {
            var task = saveMethod.Invoke(factory, [projection, attribute.BlobName, CancellationToken.None]) as Task;
            if (task != null)
            {
                await task;
            }
        }

        _logger.LogDebug("Saved projection {ProjectionType}", attribute.ProjectionType.Name);
    }

    /// <summary>
    /// Gets the projection factory for the specified type.
    /// </summary>
    /// <param name="projectionType">The projection type.</param>
    /// <returns>The factory, or null if not found.</returns>
    internal object? GetProjectionFactory(Type projectionType)
    {
        var factoryType = typeof(IProjectionFactory<>).MakeGenericType(projectionType);
        var factory = _serviceProvider.GetService(factoryType);

        if (factory != null)
        {
            return factory;
        }

        // Fall back to looking for IProjectionFactory implementations
        var factories = _serviceProvider.GetServices<IProjectionFactory>();
        return factories.FirstOrDefault(f => f.ProjectionType == projectionType);
    }

    /// <summary>
    /// Resolves the target method from an entry point string.
    /// </summary>
    /// <param name="entryPoint">The entry point in format "Namespace.ClassName.MethodName".</param>
    /// <returns>The method info, or null if not found.</returns>
    public static MethodInfo? ResolveTargetMethod(string? entryPoint)
    {
        if (string.IsNullOrEmpty(entryPoint))
        {
            return null;
        }

        var lastDotIndex = entryPoint.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            return null;
        }

        var typeName = entryPoint.Substring(0, lastDotIndex);
        var methodName = entryPoint.Substring(lastDotIndex + 1);

        // Try to find the type in all loaded assemblies
        Type? targetType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            targetType = assembly.GetType(typeName);
            if (targetType != null)
            {
                break;
            }
        }

        if (targetType == null)
        {
            return null;
        }

        return targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    }

    /// <summary>
    /// Gets projection output attributes from a method.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The list of projection output attributes.</returns>
    public static IReadOnlyList<ProjectionOutputAttribute> GetProjectionOutputAttributes(MethodInfo? method)
    {
        if (method == null)
        {
            return Array.Empty<ProjectionOutputAttribute>();
        }

        return method.GetCustomAttributes<ProjectionOutputAttribute>(inherit: true).ToList();
    }
}
