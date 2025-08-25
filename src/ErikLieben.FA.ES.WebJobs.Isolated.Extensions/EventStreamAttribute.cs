using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs;
using System.ComponentModel.DataAnnotations;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

[Binding]
[ConnectionProvider(typeof(StorageAccountAttribute))]
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
public class EventStreamAttribute : Attribute, IConnectionProvider
{
    public EventStreamAttribute(string objectId)
    {
        ObjectId = objectId;
    }

    [AutoResolve]
    public string ObjectId { get; set; }

    [AutoResolve]
    [RegularExpression("^[A-Za-z][A-Za-z0-9]{2,62}$")]
    public string? ObjectType { get; set; }

    public string? Connection { get; set; }

    public string? DocumentType { get; set; }

    public string? DefaultStreamType { get; set; }

    public string? DefaultStreamConnection { get; set; }

    public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
}