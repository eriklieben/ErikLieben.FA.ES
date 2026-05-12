using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Note;

[EventName("Note.Edited")]
public record NoteEdited(
    string Body,
    DateTime EditedAt);
