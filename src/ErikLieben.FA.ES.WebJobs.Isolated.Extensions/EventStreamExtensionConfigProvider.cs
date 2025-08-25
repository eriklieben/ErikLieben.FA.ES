using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

public class EventStreamExtensionConfigProvider : IExtensionConfigProvider
{

    public EventStreamExtensionConfigProvider()
    {
    }

    public void Initialize(ExtensionConfigContext context)
    {
        var rule = context.AddBindingRule<EventStreamAttribute>();
        rule.BindToInput((attr) => ConvertToParameterBindingData(attr));
    }

    private static ParameterBindingData ConvertToParameterBindingData(EventStreamAttribute attribute)
    {
        var blobDetails = new EventStreamAttributeData()
        {
            ObjectId = attribute.ObjectId,
            ObjectType = attribute.ObjectType,
            Connection = attribute.Connection,
            DocumentType = attribute.DocumentType,
            DefaultStreamType = attribute.DefaultStreamType,
            DefaultStreamConnection = attribute.DefaultStreamConnection,
            CreateEmptyObjectWhenNonExistent = attribute.CreateEmptyObjectWhenNonExistent
        };

        var blobDetailsBinaryData = new BinaryData(blobDetails);
        var bindingData = new ParameterBindingData("1.0", "ErikLieben.FA.ES", blobDetailsBinaryData, "application/json");
        return bindingData;
    }
}
