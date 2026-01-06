using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Projections;

namespace TaskFlow.Domain.Projections;

/// <summary>
/// Execution context that carries language code information for language-specific projections.
/// </summary>
public class LanguageContext : IExecutionContext
{
    /// <summary>
    /// Gets the language code (e.g., "nl-NL", "de-DE").
    /// </summary>
    public string LanguageCode { get; }

    /// <summary>
    /// Gets the project ID this context applies to.
    /// </summary>
    public string ProjectId { get; }

    /// <summary>
    /// Gets a value indicating whether this is the root context (always true for LanguageContext).
    /// </summary>
    public bool IsRoot => ParentContext == null;

    /// <summary>
    /// Gets the document being processed (not used for language routing).
    /// </summary>
    public IObjectDocument Document { get; }

    /// <summary>
    /// Gets the parent execution context.
    /// </summary>
    public IExecutionContext? ParentContext { get; }

    /// <summary>
    /// Creates a new LanguageContext with the specified language code and project ID.
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "de-DE").</param>
    /// <param name="projectId">The project ID.</param>
    /// <param name="document">Optional document for context.</param>
    /// <param name="parentContext">Optional parent context.</param>
    public LanguageContext(
        string languageCode,
        string projectId,
        IObjectDocument? document = null,
        IExecutionContext? parentContext = null)
    {
        LanguageCode = languageCode;
        ProjectId = projectId;
        Document = document!;
        ParentContext = parentContext;
    }
}
