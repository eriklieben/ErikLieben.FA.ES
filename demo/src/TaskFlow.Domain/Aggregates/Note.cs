using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using TaskFlow.Domain.Events.Note;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

/// <summary>
/// Simple Note aggregate persisted in PostgreSQL — minimal aggregate used end-to-end
/// to exercise the Postgres event-store provider alongside the Postgres-backed projections.
/// </summary>
[Aggregate]
[EventStreamType("postgres", "postgres")]
public partial class Note : Aggregate
{
    public Note(IEventStream stream) : base(stream)
    {
    }

    public static void InitializeStream(IEventStream stream)
    {
        _ = new Note(stream);
    }

    public string? Title { get; private set; }
    public string? Body { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public ObjectMetadata<NoteId>? Metadata { get; private set; }

    public async Task<Result> Edit(string body, DateTime? editedAt = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure(new ValidationError("Body must not be empty.", nameof(body)));
        }
        if (Body == body)
        {
            return Result.Success();
        }
        var when = editedAt ?? DateTime.UtcNow;
        await Stream.Session(context => Fold(context.Append(new NoteEdited(body, when))));
        return Result.Success();
    }

    private void When(NoteCreated @event)
    {
        Title = @event.Title;
        Body = @event.Body;
        CreatedAt = @event.CreatedAt;
    }

    private void When(NoteEdited @event)
    {
        Body = @event.Body;
        EditedAt = @event.EditedAt;
    }

    private void PostWhen(IObjectDocument document, IEvent @event)
    {
        Metadata = ObjectMetadata<NoteId>.From(document, @event, NoteId.From(document.ObjectId));
    }
}
