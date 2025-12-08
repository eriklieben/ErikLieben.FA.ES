using System.Text.RegularExpressions;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Resolves blob path templates with partition keys.
/// Supports templates like "questions/{language}.json" or "kanban/{projectId}.json".
/// </summary>
public static class BlobPathTemplateResolver
{
    /// <summary>
    /// Resolves a template with partition values.
    /// Example: "questions/{language}.json" + { "language": "en-GB" } → "questions/en-GB.json"
    /// </summary>
    public static string Resolve(string template, Dictionary<string, string> values)
    {
        var result = template;

        foreach (var kvp in values)
        {
            var placeholder = $"{{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value);
        }

        // Ensure .json extension if not present
        if (!result.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            result += ".json";
        }

        return result;
    }

    /// <summary>
    /// Resolves template with simple partition key.
    /// Uses "partitionKey" as default placeholder name.
    /// </summary>
    public static string Resolve(string template, string partitionKey)
    {
        return Resolve(template, new Dictionary<string, string>
        {
            ["partitionKey"] = partitionKey
        });
    }

    /// <summary>
    /// Extracts placeholder names from template.
    /// Example: "questions/{entityType}/{language}.json" → ["entityType", "language"]
    /// </summary>
    public static IEnumerable<string> GetPlaceholders(string template)
    {
        var regex = new Regex(@"\{(\w+)\}");
        var matches = regex.Matches(template);

        foreach (Match match in matches)
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>
    /// Extracts partition values from resolved path.
    /// Example: template="questions/{language}.json", path="questions/en-GB.json"
    ///          → { "language": "en-GB" }
    /// </summary>
    public static Dictionary<string, string> ExtractValues(string template, string resolvedPath)
    {
        var values = new Dictionary<string, string>();

        // Remove .json extension for comparison
        var templateWithoutExt = template.Replace(".json", "");
        var pathWithoutExt = resolvedPath.Replace(".json", "");

        // Build regex from template by replacing {placeholder} with named capture groups
        // Note: We don't use Regex.Escape because it would escape the braces,
        // and blob paths typically don't contain regex special characters
        var regexPattern = Regex.Replace(templateWithoutExt, @"\{(\w+)\}", @"(?<$1>[^/]+)");

        var regex = new Regex($"^{regexPattern}$");
        var match = regex.Match(pathWithoutExt);

        if (match.Success)
        {
            foreach (Group group in match.Groups)
            {
                if (!int.TryParse(group.Name, out _)) // Skip numeric groups
                {
                    values[group.Name] = group.Value;
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Gets the container name from template.
    /// Example: "projections/questions/{language}.json" → "projections"
    /// </summary>
    public static string GetContainerName(string template)
    {
        var parts = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "projections";
    }
}
