namespace ErikLieben.FA.ES.AzureStorage.Migration;

using ErikLieben.FA.ES.EventStreamManagement.Cutover;
using System.Text.Json.Serialization;

/// <summary>
/// JSON serializer context for AOT-compatible migration serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(MigrationRoutingEntry))]
[JsonSerializable(typeof(BackupData))]
[JsonSerializable(typeof(BackupRegistryData))]
[JsonSerializable(typeof(BackupRegistryEntry))]
internal partial class MigrationJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Internal data structure for backup serialization.
/// </summary>
internal class BackupData
{
    /// <summary>
    /// Gets or sets the backup identifier.
    /// </summary>
    public Guid BackupId { get; set; }

    /// <summary>
    /// Gets or sets when the backup was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the object identifier.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream version.
    /// </summary>
    public int StreamVersion { get; set; }

    /// <summary>
    /// Gets or sets the event count.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Gets or sets the serialized events as full JSON event strings.
    /// Each string is a complete JSON representation of a JsonEvent.
    /// </summary>
    public List<string>? SerializedEvents { get; set; }

    /// <summary>
    /// Gets or sets the serialized object document.
    /// </summary>
    public string? SerializedObjectDocument { get; set; }
}
