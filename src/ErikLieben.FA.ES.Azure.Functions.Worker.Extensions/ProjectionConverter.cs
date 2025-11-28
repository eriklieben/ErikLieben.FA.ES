using System.Diagnostics;
using System.Text.Json;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Input converter for Azure Functions that converts binding data into projection instances.
/// </summary>
[SupportsDeferredBinding]
internal class ProjectionConverter : IInputConverter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IObjectDocumentFactory _objectDocumentFactory;
    private readonly IEventStreamFactory _eventStreamFactory;

    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionConverter"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving projection factories.</param>
    /// <param name="objectDocumentFactory">The object document factory.</param>
    /// <param name="eventStreamFactory">The event stream factory.</param>
    public ProjectionConverter(
        IServiceProvider serviceProvider,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        _serviceProvider = serviceProvider;
        _objectDocumentFactory = objectDocumentFactory;
        _eventStreamFactory = eventStreamFactory;
    }

    /// <inheritdoc />
    public async ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        return context?.Source switch
        {
            ModelBindingData binding => await ConvertFromBindingDataAsync(context, binding),
            _ => ConversionResult.Unhandled(),
        };
    }

    private async Task<ConversionResult> ConvertFromBindingDataAsync(
        ConverterContext context,
        ModelBindingData modelBindingData)
    {
        try
        {
            if (modelBindingData.Source is not "ErikLieben.FA.ES.Projection")
            {
                return ConversionResult.Unhandled();
            }

            var projectionData = GetBindingDataContent(modelBindingData);
            var result = await ConvertModelBindingDataAsync(context.TargetType, projectionData);

            if (result is null)
            {
                return ConversionResult.Failed(new InvalidOperationException(
                    $"Unable to convert projection binding data to type '{context.TargetType.Name}'."));
            }

            return ConversionResult.Success(result);
        }
        catch (JsonException ex)
        {
            string msg =
                @"Binding parameters to complex objects uses JSON serialization.
                    1. Bind the parameter type as 'string' instead to get the raw values and avoid JSON deserialization, or
                    2. Change the projection data to be valid JSON.";

            return ConversionResult.Failed(new InvalidOperationException(msg, ex));
        }
        catch (Exception ex)
        {
            return ConversionResult.Failed(ex);
        }
    }

    internal static ProjectionData GetBindingDataContent(ModelBindingData bindingData)
    {
        return bindingData is null
            ? throw new ArgumentNullException(nameof(bindingData))
            : bindingData.ContentType switch
            {
                "application/json" => bindingData.Content.ToObjectFromJson<ProjectionData>()
                    ?? throw new InvalidOperationException("Binding data content is null or invalid JSON for ProjectionData."),
                _ => throw new Exceptions.InvalidContentTypeException(bindingData.ContentType, "application/json")
            };
    }

    internal async Task<object?> ConvertModelBindingDataAsync(
        Type targetType,
        ProjectionData? data)
    {
        using var activity = ActivitySource.StartActivity($"ProjectionConverter.{nameof(ConvertModelBindingDataAsync)}");

        // Try to get a factory registered for this specific projection type
        var factoryType = typeof(IProjectionFactory<>).MakeGenericType(targetType);
        var factory = _serviceProvider.GetService(factoryType);

        if (factory is IProjectionFactory projectionFactory)
        {
            return await projectionFactory.GetOrCreateProjectionAsync(
                _objectDocumentFactory,
                _eventStreamFactory,
                data?.BlobName);
        }

        // Fall back to looking for IProjectionFactory implementations
        var factories = _serviceProvider.GetServices<IProjectionFactory>();
        var matchingFactory = factories.FirstOrDefault(f => f.ProjectionType == targetType);

        if (matchingFactory != null)
        {
            return await matchingFactory.GetOrCreateProjectionAsync(
                _objectDocumentFactory,
                _eventStreamFactory,
                data?.BlobName);
        }

        throw new InvalidOperationException(
            $"No projection factory registered for type '{targetType.Name}'. " +
            $"Register IProjectionFactory<{targetType.Name}> or IProjectionFactory in the service collection.");
    }
}
