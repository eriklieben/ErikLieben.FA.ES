using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

/// <summary>
/// Extension configuration provider that registers the EventStream and Projection bindings with Azure Functions.
/// </summary>
public class EventStreamExtensionConfigProvider : IExtensionConfigProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamExtensionConfigProvider"/> class.
    /// </summary>
    public EventStreamExtensionConfigProvider()
    {
    }

    /// <inheritdoc/>
    public void Initialize(ExtensionConfigContext context)
    {
        // Register EventStream binding
        var eventStreamRule = context.AddBindingRule<EventStreamAttribute>();
        eventStreamRule.BindToInput((attr) => ConvertEventStreamToParameterBindingData(attr));

        // Register Projection binding
        var projectionRule = context.AddBindingRule<ProjectionAttribute>();
        projectionRule.BindToInput((attr) => ConvertProjectionToParameterBindingData(attr));
    }

    private static ParameterBindingData ConvertEventStreamToParameterBindingData(EventStreamAttribute attribute)
    {
        var data = new EventStreamAttributeData()
        {
            ObjectId = attribute.ObjectId,
            ObjectType = attribute.ObjectType,
            Connection = attribute.Connection,
            DocumentType = attribute.DocumentType,
            DefaultStreamType = attribute.DefaultStreamType,
            DefaultStreamConnection = attribute.DefaultStreamConnection,
            CreateEmptyObjectWhenNonExistent = attribute.CreateEmptyObjectWhenNonExistent
        };

        var binaryData = new BinaryData(data);
        return new ParameterBindingData("1.0", "ErikLieben.FA.ES", binaryData, "application/json");
    }

    private static ParameterBindingData ConvertProjectionToParameterBindingData(ProjectionAttribute attribute)
    {
        var data = new ProjectionAttributeData()
        {
            BlobName = attribute.BlobName,
            CreateIfNotExists = attribute.CreateIfNotExists,
            Connection = attribute.Connection
        };

        var binaryData = new BinaryData(data);
        return new ParameterBindingData("1.0", "ErikLieben.FA.ES.Projection", binaryData, "application/json");
    }
}
