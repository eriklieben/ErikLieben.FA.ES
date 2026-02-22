namespace TaskFlow.Domain.ValueObjects.WorkItem;

/// <summary>
/// Represents a translated title for a work item in a specific language.
/// </summary>
/// <param name="LanguageCode">The language code (e.g., "nl-NL", "de-DE")</param>
/// <param name="Title">The translated title text</param>
public record LocalizedTitle(
    string LanguageCode,
    string Title);
