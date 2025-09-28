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
    /// <summary>
    /// Specifies that a parameter or return value binds to an Event Sourcing aggregate loaded from an event stream.
    /// </summary>
    /// <remarks>
    /// The attribute uses the configured <see cref="EventStreamConverter"/> to load or create the target object
    /// based on binding data supplied by the trigger or other inputs.
    /// </remarks>
    public class EventStreamInputAttribute : InputBindingAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventStreamInputAttribute"/> class.
        /// </summary>
        /// <param name="objectId">The identifier of the object to bind to.</param>
        public EventStreamInputAttribute(string objectId)
        {
            ObjectId = objectId;
        }

        /// <summary>
        /// Gets or sets the object identifier to bind the event stream to.
        /// </summary>
        public string? ObjectId { get; set; }

        /// <summary>
        /// Gets or sets the optional object type name used when resolving the document and stream.
        /// </summary>
        public string? ObjectType { get; set; }

        /// <summary>
        /// Gets or sets the name of the connection configuration used to access the event store backend.
        /// </summary>
        public string? Connection { get; set; }

        /// <summary>
        /// Gets or sets the document type that determines which document factory to use.
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Gets or sets the default stream type used when the binding data does not specify a stream type.
        /// </summary>
        public string? DefaultStreamType { get; set; }

        /// <summary>
        /// Gets or sets the default stream connection name used when the binding data does not specify a connection.
        /// </summary>
        public string? DefaultStreamConnection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a new object is created when the specified object does not exist.
        /// </summary>
        public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
    }
}
