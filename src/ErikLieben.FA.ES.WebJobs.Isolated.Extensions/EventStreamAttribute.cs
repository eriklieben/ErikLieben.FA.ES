using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs;
using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

[Binding]
[ConnectionProvider(typeof(StorageAccountAttribute))]
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
/// <summary>
/// Specifies that a parameter or return value binds to an Event Sourcing aggregate loaded from an event stream in WebJobs isolated worker.
/// </summary>
public class EventStreamAttribute : Attribute, IConnectionProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventStreamAttribute"/> class.
    /// </summary>
    /// <param name="objectId">The identifier of the object to bind to.</param>
    public EventStreamAttribute(string objectId)
    {
        ObjectId = objectId;
    }

    /// <summary>
    /// Gets or sets the object identifier to bind the event stream to.
    /// </summary>
    [AutoResolve]
    public string ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the optional object type name used when resolving the document and stream.
    /// </summary>
    [AutoResolve]
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{2,62}$")]
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
