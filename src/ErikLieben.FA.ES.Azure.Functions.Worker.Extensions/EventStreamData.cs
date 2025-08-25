using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

public class EventStreamData
{
    public EventStreamData(
        string objectId, 
        string objectType,
        string connection,
        string documentType,
        string defaultStreamType,
        string defaultStreamConnection,
        bool createEmtpyObjectWhenNonExisting)
    {
        ObjectId = objectId;
        ObjectType = objectType;
        Connection = connection;
        DocumentType = documentType;
        DefaultStreamType = defaultStreamType;
        DefaultStreamConnection = defaultStreamConnection;
        CreateEmptyObjectWhenNonExistent = createEmtpyObjectWhenNonExisting;
    }

    [JsonConstructor]
    internal EventStreamData() { }


    public string? ObjectId { get; set; }
    public string? ObjectType { get; set; }
    public string? Connection { get; set; }
    public string? DocumentType { get; set; }
    public string? DefaultStreamType { get; set; }
    public string? DefaultStreamConnection { get; set; }
    public bool CreateEmptyObjectWhenNonExistent { get; set; } = false;
}
