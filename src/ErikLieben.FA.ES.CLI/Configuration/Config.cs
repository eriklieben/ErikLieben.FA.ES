using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CLI.Configuration;

/// <summary>
/// Represents configuration for the FA.ES CLI tool.
/// </summary>
public class Config
{
    /// <summary>
    /// Gets a list of additional types (full names) to include for source-generated JSON serialization.
    /// </summary>
    public List<string> AdditionalJsonSerializables { get; init; } = [];

    /// <summary>
/// Gets configuration values for the Event Sourcing (ES) features of the CLI.
/// </summary>
[JsonPropertyName("ES")]
public EsConfig Es { get; init; } = new();

    /// <summary>
    /// Gets configuration for code generation output locations.
    /// </summary>
    public GenerationConfig Generation { get; init; } = new();
}


/// <summary>
/// Represents Event Sourcing specific settings for the CLI.
/// </summary>
public class EsConfig
{
    /// <summary>
    /// Gets a value indicating whether diagnostic output is enabled for CLI operations.
    /// </summary>
    public bool EnableDiagnostics { get; init; } = false;
}

/// <summary>
/// Configuration for code generation output locations.
/// </summary>
public class GenerationConfig
{
    public GeneratedTypeConfig Factory { get; init; } = new();
    public GeneratedTypeConfig Repository { get; init; } = new();
}

/// <summary>
/// Configuration for a specific generated type's output location.
/// </summary>
public class GeneratedTypeConfig
{
    /// <summary>
    /// Relative to project root, e.g. "Factories".
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Namespace override. Supports {ProjectNamespace} placeholder.
    /// </summary>
    public string? Namespace { get; init; }
}
