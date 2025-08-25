namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

public class EventStreamAttributeData
{
    public string? ObjectId { get; set; }
    public string? ObjectType { get; set; }
    public string? Connection { get; set; }
    public string? DocumentType { get; set; }
    public string? DefaultStreamType { get; set; }
    public string? DefaultStreamConnection { get; set; }
    public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
}