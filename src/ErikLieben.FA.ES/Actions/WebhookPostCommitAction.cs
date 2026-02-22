using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// A reference implementation of a webhook post-commit action.
/// </summary>
/// <remarks>
/// <para>
/// This class demonstrates how to implement an <see cref="IAsyncPostCommitAction"/>
/// that sends committed events to an external webhook endpoint.
/// </para>
/// <para>
/// Features demonstrated:
/// <list type="bullet">
/// <item>HMAC-SHA256 signature for webhook verification</item>
/// <item>Configurable webhook URL and secret</item>
/// <item>JSON payload serialization</item>
/// </list>
/// </para>
/// <para>
/// Consumers are encouraged to copy and adapt this implementation for their
/// specific needs (e.g., Azure Service Bus, Azure Event Grid, RabbitMQ, etc.).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Register webhook action in dependency injection
/// services.AddSingleton&lt;IAsyncPostCommitAction&gt;(sp =>
///     new WebhookPostCommitAction(
///         sp.GetRequiredService&lt;HttpClient&gt;(),
///         new WebhookPostCommitOptions
///         {
///             Url = "https://example.com/webhooks/events",
///             Secret = "your-webhook-secret"
///         }));
/// </code>
/// </example>
public class WebhookPostCommitAction : IAsyncPostCommitAction
{
    private readonly HttpClient _httpClient;
    private readonly WebhookPostCommitOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookPostCommitAction"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for sending webhook requests.</param>
    /// <param name="options">The webhook configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public WebhookPostCommitAction(HttpClient httpClient, WebhookPostCommitOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Url);

        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc />
    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(document);

        var eventsList = events.ToList();
        if (eventsList.Count == 0)
        {
            return;
        }

        var payload = new WebhookPayload
        {
            StreamId = document.Active.StreamIdentifier,
            ObjectName = document.ObjectName,
            ObjectId = document.ObjectId,
            Events = eventsList.Select(e => new WebhookEvent
            {
                EventType = e.EventType,
                EventVersion = e.EventVersion,
                SchemaVersion = e.SchemaVersion,
                Payload = e.Payload ?? string.Empty
            }).ToList()
        };

        var json = JsonSerializer.Serialize(payload, WebhookJsonContext.Default.WebhookPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Url)
        {
            Content = content
        };

        // Add signature header if secret is configured
        if (!string.IsNullOrEmpty(_options.Secret))
        {
            var signature = ComputeHmacSignature(json, _options.Secret);
            request.Headers.Add(_options.SignatureHeaderName, $"sha256={signature}");
        }

        // Add custom headers
        foreach (var header in _options.CustomHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature for webhook payload verification.
    /// </summary>
    /// <param name="payload">The JSON payload to sign.</param>
    /// <param name="secret">The secret key.</param>
    /// <returns>The hex-encoded signature.</returns>
    private static string ComputeHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Configuration options for <see cref="WebhookPostCommitAction"/>.
/// </summary>
public class WebhookPostCommitOptions
{
    /// <summary>
    /// Gets or sets the webhook endpoint URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secret key for HMAC signature.
    /// If empty, no signature header is added.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// Gets or sets the name of the signature header.
    /// Default is "X-Signature-256".
    /// </summary>
    public string SignatureHeaderName { get; set; } = "X-Signature-256";

    /// <summary>
    /// Gets or sets custom headers to include with webhook requests.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = [];
}

/// <summary>
/// The payload sent to the webhook endpoint.
/// </summary>
internal class WebhookPayload
{
    /// <summary>
    /// Gets or sets the stream identifier.
    /// </summary>
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object name (type).
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object identifier.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of events.
    /// </summary>
    public List<WebhookEvent> Events { get; set; } = [];
}

/// <summary>
/// An individual event in the webhook payload.
/// </summary>
internal class WebhookEvent
{
    /// <summary>
    /// Gets or sets the event type name.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the event version.
    /// </summary>
    public int EventVersion { get; set; }

    /// <summary>
    /// Gets or sets the schema version.
    /// </summary>
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the serialized event payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// JSON serializer context for AOT-compatible webhook serialization.
/// </summary>
[JsonSerializable(typeof(WebhookPayload))]
[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(List<WebhookEvent>))]
internal partial class WebhookJsonContext : JsonSerializerContext
{
}
