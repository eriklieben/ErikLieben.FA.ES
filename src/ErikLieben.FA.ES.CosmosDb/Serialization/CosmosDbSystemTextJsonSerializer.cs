using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.CosmosDb.Model;
using Microsoft.Azure.Cosmos;

namespace ErikLieben.FA.ES.CosmosDb.Serialization;

/// <summary>
/// A custom CosmosDB serializer that uses System.Text.Json with AOT support.
/// This serializer respects <see cref="JsonPropertyNameAttribute"/> attributes and
/// integrates with the <see cref="CosmosDbJsonContext"/> for AOT compatibility.
/// </summary>
/// <remarks>
/// <para>
/// This serializer extends <see cref="CosmosLinqSerializer"/> to support LINQ queries
/// with proper member name mapping. It uses the same JSON serialization options
/// as the AOT-generated context to ensure consistency between serialization and queries.
/// </para>
/// <para>
/// AOT Compatibility: All known entity types are registered in source-generated contexts
/// (<see cref="CosmosDbJsonContext"/> and <see cref="CosmosDbInternalTypesJsonContext"/>).
/// A fallback resolver is included for any SDK-internal types not known at compile time.
/// </para>
/// </remarks>
public class CosmosDbSystemTextJsonSerializer : CosmosLinqSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbSystemTextJsonSerializer"/> class
    /// with default options using <see cref="CosmosDbJsonContext"/>.
    /// </summary>
    public CosmosDbSystemTextJsonSerializer()
        : this(CreateDefaultOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbSystemTextJsonSerializer"/> class
    /// with custom options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use.</param>
    public CosmosDbSystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates the default JSON serializer options with AOT support.
    /// </summary>
    /// <remarks>
    /// Uses source-generated contexts for known entity types. The <see cref="DefaultJsonTypeInfoResolver"/>
    /// fallback is included for any SDK-internal types that may need serialization.
    /// For full AOT compatibility, ensure all custom types are registered in a <see cref="JsonSerializerContext"/>.
    /// </remarks>
    /// <returns>The configured JSON serializer options.</returns>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "DefaultJsonTypeInfoResolver is used as fallback for SDK-internal types. " +
                        "All known entity types are handled by source-generated contexts.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "DefaultJsonTypeInfoResolver is used as fallback for SDK-internal types. " +
                        "All known entity types are handled by source-generated contexts.")]
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                CosmosDbJsonContext.Default,
                CosmosDbInternalTypesJsonContext.Default,
                new DefaultJsonTypeInfoResolver())
        };
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Type resolution is handled by registered source-generated contexts. " +
                        "The generic method is required by CosmosLinqSerializer interface.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Type resolution is handled by registered source-generated contexts. " +
                        "The generic method is required by CosmosLinqSerializer interface.")]
    public override T FromStream<T>(Stream stream)
    {
        if (stream == null)
        {
            return default!;
        }

        // Check for empty stream - some streams don't support Length property
        try
        {
            if (stream.CanSeek)
            {
                if (stream.Length == 0)
                {
                    return default!;
                }
                // Ensure stream is at the beginning
                if (stream.Position != 0)
                {
                    stream.Position = 0;
                }
            }
        }
        catch (NotSupportedException)
        {
            // Stream doesn't support Length/Position - continue with deserialization
        }

        // Use synchronous read to avoid issues with CosmosDB SDK's stream handling
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        if (memoryStream.Length == 0)
        {
            return default!;
        }

        memoryStream.Position = 0;
        var result = JsonSerializer.Deserialize<T>(memoryStream, _options);
        return result ?? default!;
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Type resolution is handled by registered source-generated contexts. " +
                        "The generic method is required by CosmosLinqSerializer interface.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Type resolution is handled by registered source-generated contexts. " +
                        "The generic method is required by CosmosLinqSerializer interface.")]
    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Gets the serialized property name for a member, respecting <see cref="JsonPropertyNameAttribute"/>.
    /// This is critical for LINQ query translation to use correct property names.
    /// </summary>
    /// <param name="memberInfo">The member info to get the serialized name for.</param>
    /// <returns>The serialized property name.</returns>
    public override string SerializeMemberName(MemberInfo memberInfo)
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        // First, check for explicit JsonPropertyName attribute
        var jsonPropertyName = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyName != null)
        {
            return jsonPropertyName.Name;
        }

        // Fall back to the naming policy
        var memberName = memberInfo.Name;
        if (_options.PropertyNamingPolicy != null)
        {
            return _options.PropertyNamingPolicy.ConvertName(memberName);
        }

        return memberName;
    }
}

/// <summary>
/// AOT-compatible JSON serializer context for internal CosmosDB document types.
/// These types are used internally by the projection factory.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectionDocument))]
[JsonSerializable(typeof(CheckpointDocument))]
internal partial class CosmosDbInternalTypesJsonContext : JsonSerializerContext
{
}

/// <summary>
/// CosmosDB document wrapper for storing projection data.
/// </summary>
/// <remarks>
/// This type is public to enable AOT serialization registration.
/// It is used internally by <see cref="CosmosDbProjectionFactory{T}"/>.
/// </remarks>
public class ProjectionDocument
{
    /// <summary>
    /// The unique document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The projection name (partition key).
    /// </summary>
    [JsonPropertyName("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// The serialized projection JSON data.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// The last modified timestamp.
    /// </summary>
    [JsonPropertyName("lastModified")]
    public DateTimeOffset LastModified { get; set; }
}

/// <summary>
/// CosmosDB document wrapper for storing checkpoint data.
/// </summary>
/// <remarks>
/// This type is public to enable AOT serialization registration.
/// It is used internally by <see cref="CosmosDbProjectionFactory{T}"/>.
/// </remarks>
public class CheckpointDocument
{
    /// <summary>
    /// The unique document ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The projection name (partition key).
    /// </summary>
    [JsonPropertyName("projectionName")]
    public string ProjectionName { get; set; } = string.Empty;

    /// <summary>
    /// The checkpoint fingerprint.
    /// </summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// The serialized checkpoint JSON data.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// The creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
