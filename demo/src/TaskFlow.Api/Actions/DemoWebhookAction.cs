using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Actions;
using ErikLieben.FA.ES.Documents;

namespace TaskFlow.Api.Actions;

/// <summary>
/// Demo webhook action that can be configured to fail for testing post-commit error handling.
/// </summary>
/// <remarks>
/// This action demonstrates:
/// <list type="bullet">
/// <item>How to implement <see cref="IAsyncPostCommitAction"/></item>
/// <item>How to test post-commit failure scenarios</item>
/// <item>How the exception handling works when post-commit actions fail</item>
/// </list>
/// </remarks>
public class DemoWebhookAction : IAsyncPostCommitAction
{
    private readonly DemoWebhookOptions _options;
    private readonly ILogger<DemoWebhookAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoWebhookAction"/> class.
    /// </summary>
    public DemoWebhookAction(DemoWebhookOptions options, ILogger<DemoWebhookAction> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PostCommitAsync(IEnumerable<JsonEvent> events, IObjectDocument document)
    {
        var eventsList = events.ToList();

        _logger.LogInformation(
            "DemoWebhookAction: Processing {EventCount} events for stream {StreamId}",
            eventsList.Count,
            document.Active.StreamIdentifier);

        // Simulate network latency
        if (_options.SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(_options.SimulatedLatency);
        }

        // Simulate configurable failure
        if (_options.SimulateFailure)
        {
            _logger.LogWarning("DemoWebhookAction: Simulating failure as configured");
            throw new HttpRequestException($"Simulated webhook failure for stream {document.Active.StreamIdentifier}");
        }

        // Simulate transient failure (fails first N attempts)
        if (_options.FailFirstNAttempts > 0)
        {
            var attemptKey = $"{document.Active.StreamIdentifier}_{document.Active.CurrentStreamVersion}";
            var attempts = IncrementAttempts(attemptKey);

            if (attempts <= _options.FailFirstNAttempts)
            {
                _logger.LogWarning(
                    "DemoWebhookAction: Simulating transient failure (attempt {Attempt} of {MaxFailures})",
                    attempts,
                    _options.FailFirstNAttempts);
                throw new HttpRequestException($"Simulated transient failure (attempt {attempts})");
            }
        }

        _logger.LogInformation(
            "DemoWebhookAction: Successfully processed events for {StreamId}",
            document.Active.StreamIdentifier);
    }

    private static readonly Dictionary<string, int> _attemptTracker = new();
    private static readonly object _lock = new();

    private static int IncrementAttempts(string key)
    {
        lock (_lock)
        {
            _attemptTracker.TryGetValue(key, out var count);
            _attemptTracker[key] = ++count;
            return count;
        }
    }
}

/// <summary>
/// Configuration options for <see cref="DemoWebhookAction"/>.
/// </summary>
public class DemoWebhookOptions
{
    /// <summary>
    /// Gets or sets whether to simulate a permanent failure.
    /// </summary>
    public bool SimulateFailure { get; set; }

    /// <summary>
    /// Gets or sets the number of initial attempts that should fail (for transient failure testing).
    /// </summary>
    public int FailFirstNAttempts { get; set; }

    /// <summary>
    /// Gets or sets simulated network latency.
    /// </summary>
    public TimeSpan SimulatedLatency { get; set; } = TimeSpan.FromMilliseconds(50);
}
