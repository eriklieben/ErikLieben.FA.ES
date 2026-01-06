using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// A demo note was added to the project.
/// This event can be added regardless of project state (active or completed)
/// and is used for demonstrating live migration scenarios.
/// </summary>
[EventName("Project.DemoNoteAdded")]
public record DemoNoteAdded(
    string Note,
    string AddedBy,
    DateTime AddedAt);
