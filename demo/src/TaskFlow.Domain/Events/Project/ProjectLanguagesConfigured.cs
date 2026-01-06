using ErikLieben.FA.ES.Attributes;

namespace TaskFlow.Domain.Events.Project;

/// <summary>
/// The required languages for a project have been configured.
/// Work items in this project should have translations for all specified languages.
/// </summary>
[EventName("Project.LanguagesConfigured")]
public record ProjectLanguagesConfigured(
    string[] RequiredLanguages,
    string ConfiguredBy,
    DateTime ConfiguredAt);
