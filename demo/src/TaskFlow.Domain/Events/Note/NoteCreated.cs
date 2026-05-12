using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Note;

[EventName("Note.Created")]
public record NoteCreated(
    string Title,
    string Body,
    DateTime CreatedAt);
