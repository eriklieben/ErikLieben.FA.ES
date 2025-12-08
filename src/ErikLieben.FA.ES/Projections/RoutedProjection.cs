using ErikLieben.FA.ES.Documents;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// A projection that routes events to child destination projections.
/// The projection itself has When methods that can create destinations and route events.
/// Each destination is serialized inline within this projection under the "$metadata" property.
/// </summary>
public abstract class RoutedProjection : Projection
{
    /// <summary>
    /// All loaded destination projections, keyed by destination key.
    /// Type-erased storage for multi-type destination support.
    /// Serialized as top-level properties (one property per destination key).
    /// </summary>
    private readonly ConcurrentDictionary<string, Projection> _destinations = new();

    /// <summary>
    /// Gets all loaded destination projections.
    /// These are serialized as top-level properties in the JSON output.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, Projection> Destinations => _destinations;

    /// <summary>
    /// Maps destination keys to their destination types.
    /// </summary>
    private readonly ConcurrentDictionary<string, Type> _destinationTypes = new();

    /// <summary>
    /// Routing metadata containing registry, path template, and other routing state.
    /// Serialized under "$metadata" property.
    /// </summary>
    [JsonPropertyName("$metadata")]
    public RoutedProjectionMetadata RoutingMetadata { get; set; } = new();

    /// <summary>
    /// Registry tracking all destinations and version tokens.
    /// Convenience accessor for RoutingMetadata.Registry.
    /// </summary>
    [JsonIgnore]
    public DestinationRegistry Registry
    {
        get => RoutingMetadata.Registry;
        set => RoutingMetadata.Registry = value;
    }

    /// <summary>
    /// Path template used for resolving destination blob paths.
    /// Set by generated code or factory from [BlobJsonProjection] attribute.
    /// Not serialized - it's derived from the attribute.
    /// </summary>
    [JsonIgnore]
    public string PathTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Optional metadata shared across destinations.
    /// </summary>
    [JsonIgnore]
    public virtual object? Metadata { get; set; }

    /// <summary>
    /// Context for routing (used during When method execution).
    /// </summary>
    private RoutingContext? _currentContext;

    /// <summary>
    /// Initializes a new instance of the RoutedProjection class.
    /// </summary>
    protected RoutedProjection()
    {
    }

    /// <summary>
    /// Initializes a new instance of the RoutedProjection class with required factories.
    /// </summary>
    /// <param name="documentFactory">Factory for managing object documents.</param>
    /// <param name="eventStreamFactory">Factory for creating event streams.</param>
    protected RoutedProjection(
        IObjectDocumentFactory documentFactory,
        IEventStreamFactory eventStreamFactory)
        : base(documentFactory, eventStreamFactory)
    {
    }

    /// <summary>
    /// Folds event into routed projections using document-based signature.
    /// </summary>
    [Obsolete("Use Fold(IEvent, VersionToken, T?, IExecutionContext?) instead. This overload will be removed in a future major version.")]
    public override async Task Fold<T>(
        IEvent @event,
        IObjectDocument document,
        T? data = null,
        IExecutionContext? parentContext = null) where T : class
    {
        // Create version token and delegate to VersionToken overload
        var versionToken = new VersionToken(@event, document);
        await Fold(@event, versionToken, data, parentContext);
    }

    /// <summary>
    /// Folds event into routed projections using version token.
    /// Generated code will override this to dispatch to When methods.
    /// After When methods execute, this routes events to partitions.
    /// </summary>
    public override async Task Fold<T>(
        IEvent @event,
        VersionToken versionToken,
        T? data = null,
        IExecutionContext? parentContext = null) where T : class
    {
        // Get document reference (lightweight - just metadata)
        IObjectDocument? document = null;
        if (DocumentFactory != null)
        {
            document = await DocumentFactory.GetAsync(
                versionToken.ObjectName,
                versionToken.ObjectId);
        }
        else
        {
            throw new InvalidOperationException("DocumentFactory not initialized");
        }

        // Set up routing context for When methods to use
        _currentContext = new RoutingContext
        {
            Event = @event,
            Document = document,
            VersionToken = versionToken
        };

        try
        {
            // Call generated DispatchToWhen method which will invoke the When methods
            // When methods can call AddDestination and RouteToDestination
            DispatchToWhen(@event, versionToken);

            // Forward to each destination using collected route targets
            foreach (var target in _currentContext.RouteTargets)
            {
                if (!_destinations.TryGetValue(target.DestinationKey, out var destination))
                {
                    throw new InvalidOperationException(
                        $"Destination '{target.DestinationKey}' does not exist. " +
                        $"Use AddDestination to create it before routing events to it.");
                }

                // Use custom event if provided, otherwise original event
                var eventToForward = target.CustomEvent ?? @event;

                // Use target's context if provided, otherwise use parent context
                var contextToUse = target.Context ?? parentContext;

                // Forward using VersionToken overload (no document lookup needed!)
                await destination.Fold(eventToForward, versionToken, data, contextToUse);

                // Update destination's checkpoint
                destination.UpdateCheckpoint(versionToken);
            }

            // Update checkpoint to track processed event
            UpdateCheckpoint(versionToken);

            // Update global registry
            UpdateRegistry(@event, versionToken);
        }
        finally
        {
            _currentContext = null;
        }
    }

    /// <summary>
    /// Creates and registers a new destination of the specified type.
    /// </summary>
    /// <typeparam name="TDestination">The type of destination projection to create.</typeparam>
    /// <param name="destinationKey">The destination key for the new destination.</param>
    protected void AddDestination<TDestination>(string destinationKey)
        where TDestination : Projection
    {
        AddDestination<TDestination>(destinationKey, null);
    }

    /// <summary>
    /// Creates and registers a new destination of the specified type with custom metadata.
    /// The metadata can be used for path template resolution (e.g., {projectId} in the path).
    /// </summary>
    /// <typeparam name="TDestination">The type of destination projection to create.</typeparam>
    /// <param name="destinationKey">The destination key for the new destination.</param>
    /// <param name="metadata">Custom metadata for path resolution and storage. Keys like "projectId" can be used in path templates.</param>
    protected void AddDestination<TDestination>(string destinationKey, Dictionary<string, string>? metadata)
        where TDestination : Projection
    {
        if (_currentContext == null)
            throw new InvalidOperationException("AddDestination can only be called from within When methods during Fold execution");

        if (_destinations.ContainsKey(destinationKey))
        {
            // Destination already exists - this is fine (idempotent)
            return;
        }

        // Create new destination instance
        var destination = CreateDestinationInstance<TDestination>(destinationKey);

        // Initialize destination with metadata if provided
        if (metadata != null)
        {
            destination.InitializeFromMetadata(metadata);
        }

        // Register destination type
        _destinationTypes[destinationKey] = typeof(TDestination);

        // Add to destinations dictionary
        _destinations[destinationKey] = destination;

        // Storage-specific metadata (like blob path) will be set by the storage factory
        Registry.Destinations[destinationKey] = new DestinationMetadata
        {
            DestinationTypeName = typeof(TDestination).Name,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow,
            UserMetadata = metadata ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Routes the current event to the specified destination.
    /// The event will be forwarded after all When methods complete.
    /// </summary>
    /// <param name="destinationKey">The destination key to route to.</param>
    protected void RouteToDestination(string destinationKey)
    {
        if (_currentContext == null)
            throw new InvalidOperationException("RouteToDestination can only be called from within When methods during Fold execution");

        _currentContext.RouteTargets.Add(new RouteTarget
        {
            DestinationKey = destinationKey
        });
    }

    /// <summary>
    /// Routes a custom event to the specified destination.
    /// </summary>
    /// <param name="destinationKey">The destination key to route to.</param>
    /// <param name="customEvent">The custom event to route (instead of the current event).</param>
    protected void RouteToDestination(string destinationKey, IEvent customEvent)
    {
        if (_currentContext == null)
            throw new InvalidOperationException("RouteToDestination can only be called from within When methods during Fold execution");

        _currentContext.RouteTargets.Add(new RouteTarget
        {
            DestinationKey = destinationKey,
            CustomEvent = customEvent
        });
    }

    /// <summary>
    /// Routes to the specified destination with a custom execution context.
    /// </summary>
    /// <param name="destinationKey">The destination key to route to.</param>
    /// <param name="context">The execution context to pass to the destination.</param>
    protected void RouteToDestination(string destinationKey, IExecutionContext context)
    {
        if (_currentContext == null)
            throw new InvalidOperationException("RouteToDestination can only be called from within When methods during Fold execution");

        _currentContext.RouteTargets.Add(new RouteTarget
        {
            DestinationKey = destinationKey,
            Context = context
        });
    }

    /// <summary>
    /// Routes to multiple destinations.
    /// </summary>
    protected void RouteToDestinations(params string[] destinationKeys)
    {
        foreach (var key in destinationKeys)
        {
            RouteToDestination(key);
        }
    }

    /// <summary>
    /// Routes to multiple destinations.
    /// </summary>
    protected void RouteToDestinations(IEnumerable<string> destinationKeys)
    {
        foreach (var key in destinationKeys)
        {
            RouteToDestination(key);
        }
    }

    /// <summary>
    /// Updates the registry after processing events.
    /// </summary>
    protected void UpdateRegistry(IEvent @event, VersionToken versionToken)
    {
        Registry.LastUpdated = DateTimeOffset.UtcNow;

        // Update destination last modified in registry
        foreach (var kvp in _destinations)
        {
            if (Registry.Destinations.TryGetValue(kvp.Key, out var metadata))
            {
                metadata.LastModified = DateTimeOffset.UtcNow;
                metadata.CheckpointFingerprint = kvp.Value.CheckpointFingerprint;
            }
        }
    }

    /// <summary>
    /// Gets all current destination keys (for querying).
    /// </summary>
    public IEnumerable<string> GetDestinationKeys() => _destinations.Keys;

    /// <summary>
    /// Tries to get a specific destination if it exists.
    /// </summary>
    public bool TryGetDestination<TDestination>(string destinationKey, out TDestination? destination)
        where TDestination : Projection
    {
        if (_destinations.TryGetValue(destinationKey, out var baseDestination) && baseDestination is TDestination typedDestination)
        {
            destination = typedDestination;
            return true;
        }

        destination = null;
        return false;
    }

    /// <summary>
    /// Clears all loaded destinations.
    /// </summary>
    public void ClearDestinations()
    {
        _destinations.Clear();
        _destinationTypes.Clear();
    }

    /// <summary>
    /// Dispatches an event to the appropriate When method.
    /// Must be implemented by generated code.
    /// </summary>
    /// <param name="event">The event to dispatch.</param>
    /// <param name="versionToken">The version token for the event.</param>
    protected abstract void DispatchToWhen(IEvent @event, VersionToken versionToken);

    /// <summary>
    /// Creates a destination instance with proper initialization.
    /// Must be implemented by generated code for AOT compatibility.
    /// </summary>
    protected abstract TDestination CreateDestinationInstance<TDestination>(string destinationKey)
        where TDestination : Projection;

    /// <summary>
    /// Backing field for checkpoint.
    /// </summary>
    private Checkpoint _checkpoint = [];

    /// <summary>
    /// Global checkpoint tracking processed events.
    /// </summary>
    [JsonIgnore]
    public override Checkpoint Checkpoint
    {
        get => _checkpoint;
        set => _checkpoint = value;
    }

    /// <summary>
    /// Updates the checkpoint to record that the given version token has been processed.
    /// Routed projections use their own checkpoint field since the base Checkpoint property is ignored.
    /// </summary>
    public override void UpdateCheckpoint(VersionToken versionToken)
    {
        _checkpoint[versionToken.ObjectIdentifier] = versionToken.VersionIdentifier;
        CheckpointFingerprint = GenerateCheckpointFingerprint();
    }

    /// <summary>
    /// Generates a fingerprint from the checkpoint for change detection.
    /// </summary>
    private string GenerateCheckpointFingerprint()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in _checkpoint.OrderBy(i => i.Key.Value))
        {
            sb.AppendLine($"{item.Key.Value}|{item.Value.Value}");
        }
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the document from the current routing context.
    /// Can only be accessed from within When methods during Fold execution.
    /// </summary>
    public IObjectDocument? CurrentDocument => _currentContext?.Document;

    /// <summary>
    /// Context for routing during When method execution.
    /// </summary>
    private class RoutingContext
    {
        public IEvent Event { get; set; } = null!;
        public IObjectDocument Document { get; set; } = null!;
        public VersionToken VersionToken { get; set; }
        public List<RouteTarget> RouteTargets { get; } = [];
    }
}
