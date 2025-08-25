using System.Runtime.CompilerServices;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

[assembly: ExtensionInformation("ErikLieben.FA.ES.WebJobs.Isolated.Extensions.Tests", "1.0.0")]
[assembly: InternalsVisibleTo("ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests")]

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions
{
    [InputConverter(typeof(EventStreamConverter))]
    [ConverterFallbackBehavior(ConverterFallbackBehavior.Default)]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public class EventStreamInputAttribute : InputBindingAttribute
    {
        public EventStreamInputAttribute(string objectId)
        {
            ObjectId = objectId;
        }
        public string? ObjectId { get; set; }

        public string? ObjectType { get; set; }

        public string? Connection { get; set; }

        public string? DocumentType { get; set; }

        public string? DefaultStreamType { get; set; }
        public string? DefaultStreamConnection { get; set; }

        public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
    }
}
