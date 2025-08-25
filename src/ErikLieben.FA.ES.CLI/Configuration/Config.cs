using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.CLI.Configuration;

public class Config
{
    public List<string> AdditionalJsonSerializables { get; init; } = [];

    [JsonPropertyName("ES")]
    public EsConfig Es { get; init; } = new();
}


public class EsConfig
{
    public bool EnableDiagnostics { get; init; } = false;
}
