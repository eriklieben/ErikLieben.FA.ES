using System.Diagnostics;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using System.Text.Json;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;
using ErikLieben.FA.ES.Aggregates;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

[SupportsDeferredBinding]
internal class EventStreamConverter : IInputConverter
{
    private readonly IAggregateFactory aggregrateFactory;
    private readonly IObjectDocumentFactory objectDocumentFactory;
    private readonly IEventStreamFactory eventStreamFactory;

    private static readonly ActivitySource ActivitySource = new("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");

    public EventStreamConverter(
        IAggregateFactory aggregrateFactory,
        IObjectDocumentFactory objectDocumentFactory,
        IEventStreamFactory eventStreamFactory)
    {
        this.aggregrateFactory = aggregrateFactory;
        this.objectDocumentFactory = objectDocumentFactory;
        this.eventStreamFactory = eventStreamFactory;
    }

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
            if (modelBindingData.Source is not "ErikLieben.FA.ES")
            {
                throw new InvalidBindingSourceException(modelBindingData.Source, "ErikLieben.FA.ES.Azure.Functions.Worker.Extensions");
            }

            var eventStreamData = GetBindingDataContent(modelBindingData);
            var result = await ConvertModelBindingDataAsync(context.TargetType, eventStreamData);

            if (result is null)
            {
                return ConversionResult.Failed(new InvalidOperationException($"Unable to convert blob binding data to type '{context.TargetType.Name}'."));
            }

            return ConversionResult.Success(result);
        }
        catch (JsonException ex)
        {
            string msg =
                @"Binding parameters to complex objects uses JSON serialization.
                    1. Bind the parameter type as 'string' instead to get the raw values and avoid JSON deserialization, or
                    2. Change the blob to be valid json.";

            return ConversionResult.Failed(new InvalidOperationException(msg, ex));
        }
        catch (Exception ex)
        {
            return ConversionResult.Failed(ex);
        }
    }


    internal static EventStreamData GetBindingDataContent(ModelBindingData bindingData)
    {
        return bindingData is null
            ? throw new ArgumentNullException(nameof(bindingData))
            : bindingData.ContentType switch
        {
            "application/json" => bindingData.Content.ToObjectFromJson<EventStreamData>(),
            _ => throw new InvalidContentTypeException(bindingData.ContentType, "application/json")
        };
    }

    internal async Task<object?> ConvertModelBindingDataAsync(
        Type targetType,
        EventStreamData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.ObjectId);

        using var activity = ActivitySource.StartActivity($"EventStreamConverter.{nameof(ConvertModelBindingDataAsync)}");

        var factory = aggregrateFactory.GetFactory(targetType);
        if (factory == null)
        {
            throw new Exception("Configuration error, factory for target type cannot be setup");
        }
        var document = data.CreateEmptyObjectWhenNonExistent ?
            await objectDocumentFactory.GetOrCreateAsync(factory.GetObjectName(), data.ObjectId, data.ObjectType) :
            await objectDocumentFactory.GetAsync(factory.GetObjectName(), data.ObjectId, data.ObjectType);

        var eventStream = eventStreamFactory.Create(document);
        var obj = factory.Create(eventStream);
        await obj.Fold();

        return obj;
    }
}
