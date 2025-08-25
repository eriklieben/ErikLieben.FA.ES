namespace ErikLieben.FA.ES.Documents;

public record TerminatedStream
{
    public string? StreamIdentifier { get; set; }

    public string? StreamType { get; set; }

    public string? StreamConnectionName { get; set; }


    public string? Reason { get; set; }

    public string? ContinuationStreamId { get; set; }

    public DateTimeOffset TerminationDate { get; set; }

    public int? StreamVersion { get; set; }

    public bool Deleted { get; set; } = false;

    public DateTimeOffset DeletionDate { get; set; }
}
