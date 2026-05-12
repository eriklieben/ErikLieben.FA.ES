using ErikLieben.FA.ES;
using ErikLieben.FA.Results;
using TaskFlow.Domain.Events.Note;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Aggregates;

public partial interface INoteFactory
{
    Task<(Result result, Note? note)> CreateNoteAsync(
        string title,
        string body,
        DateTime? createdAt = null);
}

public partial class NoteFactory
{
    public async Task<(Result result, Note? note)> CreateNoteAsync(
        string title,
        string body,
        DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (Result.Failure(new ValidationError("Title is required.", nameof(title))), null);
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            return (Result.Failure(new ValidationError("Body is required.", nameof(body))), null);
        }

        var noteId = NoteId.New();
        var when = createdAt ?? DateTime.UtcNow;
        var note = await CreateAsync(noteId, new NoteCreated(title, body, when)).ConfigureAwait(false);
        return (Result.Success(), note);
    }
}
