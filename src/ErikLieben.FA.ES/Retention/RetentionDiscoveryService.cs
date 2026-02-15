using System.Runtime.CompilerServices;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.Retention;

/// <summary>
/// Default implementation of <see cref="IRetentionDiscoveryService"/> that discovers
/// streams exceeding their retention policies and processes violations.
/// </summary>
public class RetentionDiscoveryService : IRetentionDiscoveryService
{
    private readonly IObjectIdProvider _objectIdProvider;
    private readonly IRetentionPolicyProvider _policyProvider;
    private readonly IStreamMetadataProvider _metadataProvider;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionDiscoveryService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionDiscoveryService"/> class.
    /// </summary>
    /// <param name="objectIdProvider">Provider for enumerating object IDs.</param>
    /// <param name="policyProvider">Provider for retention policies.</param>
    /// <param name="metadataProvider">Provider for stream metadata.</param>
    /// <param name="options">Retention options containing policy overrides.</param>
    /// <param name="logger">Optional logger.</param>
    public RetentionDiscoveryService(
        IObjectIdProvider objectIdProvider,
        IRetentionPolicyProvider policyProvider,
        IStreamMetadataProvider metadataProvider,
        IOptions<RetentionOptions>? options = null,
        ILogger<RetentionDiscoveryService>? logger = null)
    {
        _objectIdProvider = objectIdProvider ?? throw new ArgumentNullException(nameof(objectIdProvider));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _options = options?.Value ?? RetentionOptions.Default;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RetentionViolation> DiscoverViolationsAsync(
        RetentionDiscoveryOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new RetentionDiscoveryOptions();

        using var activity = FaesInstrumentation.Projections.StartActivity("Retention.DiscoverViolations");

        var types = options.AggregateTypes ?? _policyProvider.GetRegisteredTypes();
        var yielded = 0;

        foreach (var typeName in types)
        {
            if (yielded >= options.MaxResults)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var policy = GetPolicyByName(typeName);
            if (policy == null || !policy.Enabled)
            {
                continue;
            }

            if (_logger is not null)
            {
                _logger.LogDebug("Checking retention policy for type {TypeName}", typeName);
            }

            string? continuationToken = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await _objectIdProvider.GetObjectIdsAsync(
                    typeName,
                    continuationToken,
                    100,
                    cancellationToken);

                foreach (var objectId in page.Items)
                {
                    if (yielded >= options.MaxResults)
                    {
                        yield break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var metadata = await _metadataProvider.GetStreamMetadataAsync(
                        typeName, objectId, cancellationToken);

                    if (metadata == null)
                    {
                        continue;
                    }

                    var oldestEventDate = metadata.OldestEventDate ?? DateTimeOffset.UtcNow;
                    var violationType = policy.CheckViolation(metadata.EventCount, oldestEventDate);

                    if (violationType.HasValue)
                    {
                        yielded++;
                        yield return new RetentionViolation(
                            objectId,
                            typeName,
                            policy,
                            metadata.EventCount,
                            oldestEventDate,
                            violationType.Value);
                    }
                }

                continuationToken = page.ContinuationToken;
            } while (!string.IsNullOrEmpty(continuationToken));
        }

        if (_logger is not null)
        {
            _logger.LogInformation(
                "Retention discovery completed. Found {ViolationCount} violations",
                yielded);
        }
    }

    /// <inheritdoc />
    public async Task<RetentionProcessingResult> ProcessViolationAsync(
        RetentionViolation violation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(violation);

        using var activity = FaesInstrumentation.Projections.StartActivity("Retention.ProcessViolation");

        try
        {
            switch (violation.Policy.Action)
            {
                case RetentionAction.FlagForReview:
                    if (_logger is not null)
                    {
                        _logger.LogInformation(
                            "Flagged stream {StreamId} ({ObjectName}) for review: {ViolationType}, events={EventCount}",
                            violation.StreamId, violation.ObjectName, violation.ViolationType, violation.CurrentEventCount);
                    }

                    return RetentionProcessingResult.Succeeded(violation.StreamId, RetentionAction.FlagForReview);

                case RetentionAction.Archive:
                    if (_logger is not null)
                    {
                        _logger.LogInformation(
                            "Archive requested for stream {StreamId} ({ObjectName}): {ViolationType}",
                            violation.StreamId, violation.ObjectName, violation.ViolationType);
                    }

                    return RetentionProcessingResult.Succeeded(violation.StreamId, RetentionAction.Archive);

                case RetentionAction.Delete:
                    if (_logger is not null)
                    {
                        _logger.LogWarning(
                            "Delete requested for stream {StreamId} ({ObjectName}): {ViolationType}",
                            violation.StreamId, violation.ObjectName, violation.ViolationType);
                    }

                    return RetentionProcessingResult.Succeeded(violation.StreamId, RetentionAction.Delete);

                case RetentionAction.Migrate:
                    if (_logger is not null)
                    {
                        _logger.LogInformation(
                            "Migration requested for stream {StreamId} ({ObjectName}): {ViolationType}",
                            violation.StreamId, violation.ObjectName, violation.ViolationType);
                    }

                    return RetentionProcessingResult.Succeeded(violation.StreamId, RetentionAction.Migrate);

                default:
                    return RetentionProcessingResult.Failed(
                        violation.StreamId,
                        violation.Policy.Action,
                        $"Unknown retention action: {violation.Policy.Action}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Failed to process retention violation for stream {StreamId}",
                violation.StreamId);
            return RetentionProcessingResult.Failed(
                violation.StreamId,
                violation.Policy.Action,
                ex.Message);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<RetentionProcessingResult> ProcessViolationsAsync(
        IEnumerable<RetentionViolation> violations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(violations);
        return ProcessViolationsAsyncCore(violations, cancellationToken);
    }

    private async IAsyncEnumerable<RetentionProcessingResult> ProcessViolationsAsyncCore(
        IEnumerable<RetentionViolation> violations,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var violation in violations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await ProcessViolationAsync(violation, cancellationToken);
        }
    }

    private RetentionPolicy? GetPolicyByName(string typeName)
    {
        // Check policy overrides from options (keyed by type name)
        if (_options.PolicyOverrides.TryGetValue(typeName, out var policy))
        {
            return policy;
        }

        // Fall back to default policy
        return _options.DefaultPolicy;
    }
}
