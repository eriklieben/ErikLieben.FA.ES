using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.EventStream;
using Polly;

namespace ErikLieben.FA.ES.Tests.EventStream;

/// <summary>
/// Tests for the ResilientDataStore decorator.
/// </summary>
public class ResilientDataStoreTests : IDisposable
{
    public ResilientDataStoreTests()
    {
        // Register custom status code extractor for test exception type (AOT-compatible)
        ResilientDataStore.RegisterStatusCodeExtractor(ex =>
            ex is TransientHttpException httpEx ? httpEx.Status : null);
    }

    public void Dispose()
    {
        // Clean up registered extractors after each test
        ResilientDataStore.ClearStatusCodeExtractors();
    }

    #region Test Helpers

    /// <summary>
    /// A configurable IDataStore implementation for testing retry behavior.
    /// </summary>
    private sealed class ConfigurableDataStore : IDataStore, IDataStoreRecovery
    {
        private readonly Queue<Exception?> appendExceptions = new();
        private readonly Queue<Exception?> readExceptions = new();
        private readonly Queue<Exception?> removeExceptions = new();

        public int AppendCallCount { get; private set; }
        public int ReadCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }

        public List<IEvent> StoredEvents { get; } = [];

        public void SetAppendBehavior(params Exception?[] exceptions)
        {
            foreach (var ex in exceptions)
                appendExceptions.Enqueue(ex);
        }

        public void SetReadBehavior(params Exception?[] exceptions)
        {
            foreach (var ex in exceptions)
                readExceptions.Enqueue(ex);
        }

        public void SetRemoveBehavior(params Exception?[] exceptions)
        {
            foreach (var ex in exceptions)
                removeExceptions.Enqueue(ex);
        }

        public Task AppendAsync(IObjectDocument document, params IEvent[] events)
        {
            AppendCallCount++;
            if (appendExceptions.TryDequeue(out var ex) && ex != null)
                throw ex;
            StoredEvents.AddRange(events);
            return Task.CompletedTask;
        }

        public Task AppendAsync(IObjectDocument document, bool preserveTimestamp, params IEvent[] events)
            => AppendAsync(document, events);

        public Task<IEnumerable<IEvent>?> ReadAsync(IObjectDocument document, int startVersion = 0, int? untilVersion = null, int? chunk = null)
        {
            ReadCallCount++;
            if (readExceptions.TryDequeue(out var ex) && ex != null)
                throw ex;
            return Task.FromResult<IEnumerable<IEvent>?>(StoredEvents.Where(e => e.EventVersion >= startVersion));
        }

        public Task<int> RemoveEventsForFailedCommitAsync(IObjectDocument document, int fromVersion, int toVersion)
        {
            RemoveCallCount++;
            if (removeExceptions.TryDequeue(out var ex) && ex != null)
                throw ex;
            var removed = StoredEvents.RemoveAll(e => e.EventVersion >= fromVersion && e.EventVersion <= toVersion);
            return Task.FromResult(removed);
        }
    }

    /// <summary>
    /// An exception that simulates a transient HTTP error with a status code.
    /// </summary>
    private sealed class TransientHttpException : Exception
    {
        public int Status { get; }
        public TransientHttpException(int status, string message) : base(message)
        {
            Status = status;
        }
    }

    /// <summary>
    /// A simple test event.
    /// </summary>
    private sealed class TestEvent : IEvent
    {
        public string EventType { get; set; } = "Test";
        public int EventVersion { get; set; }
        public int SchemaVersion { get; set; } = 1;
        public string? ExternalSequencer { get; } = null;
        public ActionMetadata? ActionMetadata { get; } = null;
        public Dictionary<string, string> Metadata { get; } = [];
        public string? Payload { get; set; }
    }

    /// <summary>
    /// A minimal document implementation for testing.
    /// </summary>
    private sealed class TestDocument : IObjectDocument
    {
        public string ObjectId { get; set; } = "test-id";
        public string ObjectName { get; set; } = "TestObject";
        public StreamInformation Active { get; set; } = new() { StreamIdentifier = "test-stream" };
        public List<TerminatedStream> TerminatedStreams { get; } = [];
        public string? SchemaVersion { get; } = "1.0.0";
        public string? Hash { get; set; }
        public string? PrevHash { get; set; }
        public void SetHash(string? hash, string? prevHash = null)
        {
            Hash = hash;
            if (prevHash != null) PrevHash = prevHash;
        }
    }

    #endregion

    #region Retry Behavior Tests

    [Fact]
    public async Task AppendAsync_should_retry_on_transient_timeout()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(
            new TimeoutException("Connection timed out"),
            null); // Second call succeeds

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();
        var testEvent = new TestEvent { EventVersion = 0 };

        // Act
        await resilient.AppendAsync(document, testEvent);

        // Assert
        Assert.Equal(2, inner.AppendCallCount); // First failed, second succeeded
        Assert.Single(inner.StoredEvents);
    }

    [Fact]
    public async Task AppendAsync_should_retry_on_503_service_unavailable()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(
            new TransientHttpException(503, "Service Unavailable"),
            new TransientHttpException(503, "Service Unavailable"),
            null); // Third call succeeds

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act
        await resilient.AppendAsync(document, new TestEvent());

        // Assert
        Assert.Equal(3, inner.AppendCallCount);
    }

    [Fact]
    public async Task AppendAsync_should_retry_on_429_rate_limit()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(
            new TransientHttpException(429, "Too Many Requests"),
            null);

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act
        await resilient.AppendAsync(document, new TestEvent());

        // Assert
        Assert.Equal(2, inner.AppendCallCount);
    }

    [Fact]
    public async Task AppendAsync_should_not_retry_on_404_not_found()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(new TransientHttpException(404, "Not Found"));

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert
        await Assert.ThrowsAsync<TransientHttpException>(() =>
            resilient.AppendAsync(document, new TestEvent()));
        Assert.Equal(1, inner.AppendCallCount); // No retries
    }

    [Fact]
    public async Task AppendAsync_should_not_retry_on_409_conflict()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(new TransientHttpException(409, "Conflict"));

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert
        await Assert.ThrowsAsync<TransientHttpException>(() =>
            resilient.AppendAsync(document, new TestEvent()));
        Assert.Equal(1, inner.AppendCallCount); // No retries for conflict
    }

    [Fact]
    public async Task AppendAsync_should_not_retry_on_400_bad_request()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(new TransientHttpException(400, "Bad Request"));

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert
        await Assert.ThrowsAsync<TransientHttpException>(() =>
            resilient.AppendAsync(document, new TestEvent()));
        Assert.Equal(1, inner.AppendCallCount);
    }

    [Fact]
    public async Task AppendAsync_should_throw_after_max_retries_exhausted()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(
            new TimeoutException("Timeout 1"),
            new TimeoutException("Timeout 2"),
            new TimeoutException("Timeout 3"),
            new TimeoutException("Timeout 4")); // More than max retries

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            resilient.AppendAsync(document, new TestEvent()));
        Assert.Equal(4, inner.AppendCallCount); // 1 initial + 3 retries
    }

    #endregion

    #region ReadAsync Retry Tests

    [Fact]
    public async Task ReadAsync_should_retry_on_transient_failure()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.StoredEvents.Add(new TestEvent { EventType = "E1", EventVersion = 0 });
        inner.SetReadBehavior(
            new TransientHttpException(500, "Internal Server Error"),
            null);

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act
        var result = await resilient.ReadAsync(document);

        // Assert
        Assert.Equal(2, inner.ReadCallCount);
        Assert.NotNull(result);
        Assert.Single(result!);
    }

    [Fact]
    public async Task ReadAsync_should_not_retry_on_non_transient_failure()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetReadBehavior(new TransientHttpException(401, "Unauthorized"));

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert
        await Assert.ThrowsAsync<TransientHttpException>(() => resilient.ReadAsync(document));
        Assert.Equal(1, inner.ReadCallCount);
    }

    #endregion

    #region RemoveEventsForFailedCommitAsync Retry Tests

    [Fact]
    public async Task RemoveEventsForFailedCommitAsync_should_retry_on_transient_failure()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.StoredEvents.Add(new TestEvent { EventVersion = 0 });
        inner.StoredEvents.Add(new TestEvent { EventVersion = 1 });
        inner.SetRemoveBehavior(
            new TransientHttpException(502, "Bad Gateway"),
            null);

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 3 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act
        var removed = await resilient.RemoveEventsForFailedCommitAsync(document, 0, 1);

        // Assert
        Assert.Equal(2, inner.RemoveCallCount);
        Assert.Equal(2, removed);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Should_respect_custom_max_retry_attempts()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.SetAppendBehavior(
            new TimeoutException(),
            new TimeoutException(),
            new TimeoutException(),
            new TimeoutException(),
            new TimeoutException(),
            null); // Would succeed on 6th attempt

        var options = new DataStoreResilienceOptions { MaxRetryAttempts = 2 };
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act & Assert - should fail because max retries is 2 (1 initial + 2 retries = 3 attempts)
        await Assert.ThrowsAsync<TimeoutException>(() =>
            resilient.AppendAsync(document, new TestEvent()));
        Assert.Equal(3, inner.AppendCallCount);
    }

    [Fact]
    public void CreateDefaultPipeline_should_return_valid_pipeline()
    {
        // Arrange
        var options = new DataStoreResilienceOptions
        {
            MaxRetryAttempts = 5,
            InitialDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(5)
        };

        // Act
        var pipeline = ResilientDataStore.CreateDefaultPipeline(options);

        // Assert
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void Constructor_should_throw_on_null_inner()
    {
        // Arrange
        var options = new DataStoreResilienceOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ResilientDataStore(null!, options));
    }

    [Fact]
    public void Constructor_should_throw_on_null_options()
    {
        // Arrange
        var inner = new ConfigurableDataStore();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ResilientDataStore(inner, (DataStoreResilienceOptions)null!));
    }

    [Fact]
    public void Constructor_with_pipeline_should_throw_on_null_pipeline()
    {
        // Arrange
        var inner = new ConfigurableDataStore();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ResilientDataStore(inner, (ResiliencePipeline)null!));
    }

    #endregion

    #region Delegation Tests

    [Fact]
    public async Task AppendAsync_with_preserveTimestamp_should_delegate_correctly()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        var options = new DataStoreResilienceOptions();
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();
        var testEvent = new TestEvent { EventVersion = 0 };

        // Act
        await resilient.AppendAsync(document, preserveTimestamp: true, testEvent);

        // Assert
        Assert.Equal(1, inner.AppendCallCount);
        Assert.Single(inner.StoredEvents);
    }

    [Fact]
    public async Task ReadAsync_should_pass_all_parameters()
    {
        // Arrange
        var inner = new ConfigurableDataStore();
        inner.StoredEvents.Add(new TestEvent { EventVersion = 0 });
        inner.StoredEvents.Add(new TestEvent { EventVersion = 1 });
        inner.StoredEvents.Add(new TestEvent { EventVersion = 2 });

        var options = new DataStoreResilienceOptions();
        var resilient = new ResilientDataStore(inner, options);
        var document = new TestDocument();

        // Act
        var result = await resilient.ReadAsync(document, startVersion: 1, untilVersion: 2, chunk: null);

        // Assert
        Assert.Equal(1, inner.ReadCallCount);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Count());
    }

    #endregion
}
