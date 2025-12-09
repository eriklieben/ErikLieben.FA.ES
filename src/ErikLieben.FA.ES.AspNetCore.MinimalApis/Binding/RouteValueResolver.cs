using Microsoft.AspNetCore.Routing;

namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Binding;

/// <summary>
/// Provides utilities for resolving and substituting route parameter values.
/// </summary>
internal static class RouteValueResolver
{
    /// <summary>
    /// Gets a route parameter value from the route value dictionary.
    /// </summary>
    /// <param name="routeValues">The route values dictionary.</param>
    /// <param name="parameterName">The name of the route parameter.</param>
    /// <returns>The route parameter value as a string, or <c>null</c> if not found.</returns>
    public static string? GetRouteValue(RouteValueDictionary routeValues, string parameterName)
    {
        if (routeValues.TryGetValue(parameterName, out var value))
        {
            return value?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Substitutes route parameter placeholders in a pattern with actual values.
    /// </summary>
    /// <param name="pattern">
    /// The pattern containing placeholders in the format "{parameterName}".
    /// </param>
    /// <param name="routeValues">The route values dictionary.</param>
    /// <returns>
    /// The pattern with all placeholders replaced by their corresponding values,
    /// or the original pattern if it's <c>null</c> or empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Example: Pattern "{orderId}/items/{itemId}" with route values
    /// { orderId: "123", itemId: "456" } becomes "123/items/456".
    /// </para>
    /// <para>
    /// Placeholders that don't have corresponding route values are left unchanged.
    /// </para>
    /// </remarks>
    public static string? SubstituteRouteValues(string? pattern, RouteValueDictionary routeValues)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return pattern;
        }

        var result = pattern;
        foreach (var kvp in routeValues)
        {
            var placeholder = $"{{{kvp.Key}}}";
            var value = kvp.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
