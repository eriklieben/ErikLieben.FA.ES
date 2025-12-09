using ErikLieben.FA.ES.CLI.Model;
using System.Text;

namespace ErikLieben.FA.ES.CLI.CodeGeneration;

/// <summary>
/// Shared helper class for generating settings application code for aggregates.
/// </summary>
internal static class AggregateSettingsCodeGenerator
{
    /// <summary>
    /// Extracts EventStreamType attribute settings and adds them to the assignments list.
    /// </summary>
    public static void ExtractEventStreamTypeSettings(EventStreamTypeAttributeData? attribute, List<string> assignments)
    {
        if (attribute == null)
            return;

        if (attribute.StreamType != null)
            assignments.Add($"document.Active.StreamType = \"{attribute.StreamType}\";");
        if (attribute.DocumentType != null)
            assignments.Add($"document.Active.DocumentType = \"{attribute.DocumentType}\";");
        if (attribute.DocumentTagType != null)
            assignments.Add($"document.Active.DocumentTagType = \"{attribute.DocumentTagType}\";");
        if (attribute.EventStreamTagType != null)
            assignments.Add($"document.Active.EventStreamTagType = \"{attribute.EventStreamTagType}\";");
        if (attribute.DocumentRefType != null)
            assignments.Add($"document.Active.DocumentRefType = \"{attribute.DocumentRefType}\";");
    }

    /// <summary>
    /// Extracts EventStreamBlobSettings attribute settings and adds them to the assignments list.
    /// </summary>
    public static void ExtractEventStreamBlobSettings(EventStreamBlobSettingsAttributeData? attribute, List<string> assignments)
    {
        if (attribute == null)
            return;

        if (attribute.DataStore != null)
        {
            assignments.Add($"document.Active.DataStore = \"{attribute.DataStore}\";");
            assignments.Add($"document.Active.StreamConnectionName = \"{attribute.DataStore}\";");
        }
        if (attribute.DocumentStore != null)
        {
            assignments.Add($"document.Active.DocumentStore = \"{attribute.DocumentStore}\";");
            assignments.Add($"document.Active.DocumentConnectionName = \"{attribute.DocumentStore}\";");
        }
        if (attribute.DocumentTagStore != null)
        {
            assignments.Add($"document.Active.DocumentTagStore = \"{attribute.DocumentTagStore}\";");
            assignments.Add($"document.Active.DocumentTagConnectionName = \"{attribute.DocumentTagStore}\";");
        }
        if (attribute.StreamTagStore != null)
        {
            assignments.Add($"document.Active.StreamTagStore = \"{attribute.StreamTagStore}\";");
            assignments.Add($"document.Active.StreamTagConnectionName = \"{attribute.StreamTagStore}\";");
        }
        if (attribute.SnapShotStore != null)
        {
            assignments.Add($"document.Active.SnapShotStore = \"{attribute.SnapShotStore}\";");
            assignments.Add($"document.Active.SnapShotConnectionName = \"{attribute.SnapShotStore}\";");
        }
    }

    /// <summary>
    /// Builds the code block with proper indentation for applying settings.
    /// </summary>
    public static string BuildSettingsCodeBlock(List<string> assignments)
    {
        var code = new StringBuilder();
        code.AppendLine();
        code.AppendLine("                                 // Apply aggregate-specific settings for new documents");
        code.AppendLine("                                 if (document.Active.CurrentStreamVersion == -1)");
        code.AppendLine("                                 {");

        foreach (var assignment in assignments)
        {
            code.AppendLine($"                                     {assignment}");
        }

        code.AppendLine("                                     await this.objectDocumentFactory.SetAsync(document);");
        code.Append("                                 }");

        return code.ToString();
    }
}
