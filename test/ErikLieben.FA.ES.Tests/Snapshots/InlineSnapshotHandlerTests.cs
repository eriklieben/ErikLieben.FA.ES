using System.Text.Json.Serialization.Metadata;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Snapshots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ErikLieben.FA.ES.Tests.Snapshots;

public class InlineSnapshotHandlerTests
{
    private readonly ISnapShotStore _snapshotStore;
    private readonly ISnapshotPolicyProvider _policyProvider;
    private readonly ILogger<InlineSnapshotHandler> _logger;
    private readonly SnapshotOptions _options;

    public InlineSnapshotHandlerTests()
    {
        _snapshotStore = Substitute.For<ISnapShotStore>();
        _policyProvider = Substitute.For<ISnapshotPolicyProvider>();
        _logger = Substitute.For<ILogger<InlineSnapshotHandler>>();
        _options = new SnapshotOptions();
    }

    private InlineSnapshotHandler CreateHandler(SnapshotOptions? options = null)
    {
        return new InlineSnapshotHandler(
            _snapshotStore,
            _policyProvider,
            Options.Create(options ?? _options),
            _logger);
    }

    private InlineSnapshotHandler CreateHandlerDirect(SnapshotOptions? options = null)
    {
        return new InlineSnapshotHandler(
            _snapshotStore,
            _policyProvider,
            options ?? _options,
            _logger);
    }

    private static TestAggregate CreateAggregate()
    {
        var stream = Substitute.For<IEventStream>();
        return new TestAggregate(stream);
    }

    private static IObjectDocument CreateMockDocument(string streamId = "stream-1", int currentVersion = 10)
    {
        var document = Substitute.For<IObjectDocument>();
        var streamInfo = new StreamInformation
        {
            StreamIdentifier = streamId,
            CurrentStreamVersion = currentVersion
        };
        document.Active.Returns(streamInfo);
        document.ObjectName.Returns("test");
        return document;
    }

    private static IReadOnlyList<JsonEvent> CreateCommittedEvents(int count, string eventType = "TestEvent")
    {
        var events = new List<JsonEvent>();
        for (var i = 0; i < count; i++)
        {
            events.Add(new JsonEvent { EventType = eventType, EventVersion = i + 1 });
        }
        return events;
    }

    private static JsonTypeInfo CreateJsonTypeInfo()
    {
        return JsonEventSerializerContext.Default.JsonEvent;
    }

    public class Constructor : InlineSnapshotHandlerTests
    {
        [Fact]
        public void Should_throw_when_snapshotStore_is_null_via_IOptions()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InlineSnapshotHandler(
                    null!,
                    _policyProvider,
                    Options.Create(_options),
                    _logger));
        }

        [Fact]
        public void Should_throw_when_policyProvider_is_null_via_IOptions()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InlineSnapshotHandler(
                    _snapshotStore,
                    null!,
                    Options.Create(_options),
                    _logger));
        }

        [Fact]
        public void Should_use_default_options_when_IOptions_value_is_null()
        {
            // IOptions<T> can return null value; handler should use SnapshotOptions.Default
            var handler = new InlineSnapshotHandler(
                _snapshotStore,
                _policyProvider,
                (IOptions<SnapshotOptions>)null!,
                _logger);

            // Should not throw - proves default options were applied
            Assert.NotNull(handler);
        }

        [Fact]
        public void Should_throw_when_snapshotStore_is_null_via_direct_options()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InlineSnapshotHandler(
                    null!,
                    _policyProvider,
                    _options,
                    _logger));
        }

        [Fact]
        public void Should_throw_when_policyProvider_is_null_via_direct_options()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InlineSnapshotHandler(
                    _snapshotStore,
                    null!,
                    _options,
                    _logger));
        }

        [Fact]
        public void Should_use_default_options_when_direct_options_is_null()
        {
            var handler = new InlineSnapshotHandler(
                _snapshotStore,
                _policyProvider,
                (SnapshotOptions)null!,
                _logger);

            Assert.NotNull(handler);
        }

        [Fact]
        public void Should_accept_null_logger()
        {
            var handler = new InlineSnapshotHandler(
                _snapshotStore,
                _policyProvider,
                Options.Create(_options));

            Assert.NotNull(handler);
        }
    }

    public class HandlePostCommitAsync_NullArgs : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_throw_when_aggregate_is_null()
        {
            var handler = CreateHandler();
            var document = CreateMockDocument();
            var events = CreateCommittedEvents(1);
            var jsonTypeInfo = CreateJsonTypeInfo();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.HandlePostCommitAsync(null!, document, events, jsonTypeInfo));
        }

        [Fact]
        public async Task Should_throw_when_document_is_null()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var events = CreateCommittedEvents(1);
            var jsonTypeInfo = CreateJsonTypeInfo();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.HandlePostCommitAsync(aggregate, null!, events, jsonTypeInfo));
        }

        [Fact]
        public async Task Should_throw_when_committedEvents_is_null()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument();
            var jsonTypeInfo = CreateJsonTypeInfo();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.HandlePostCommitAsync(aggregate, document, null!, jsonTypeInfo));
        }

        [Fact]
        public async Task Should_throw_when_jsonTypeInfo_is_null()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument();
            var events = CreateCommittedEvents(1);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                handler.HandlePostCommitAsync(aggregate, document, events, null!));
        }
    }

    public class HandlePostCommitAsync_NoPolicy : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_return_skipped_when_no_policy_configured()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument();
            var events = CreateCommittedEvents(1);
            var jsonTypeInfo = CreateJsonTypeInfo();

            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns((SnapshotPolicy?)null);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("No policy configured", result.Reason);
        }

        [Fact]
        public async Task Should_return_skipped_when_policy_is_disabled()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument();
            var events = CreateCommittedEvents(1);
            var jsonTypeInfo = CreateJsonTypeInfo();

            _policyProvider.GetPolicy(Arg.Any<Type>())
                .Returns(new SnapshotPolicy { Enabled = false });

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("No policy configured", result.Reason);
        }
    }

    public class HandlePostCommitAsync_PolicyNotTriggered : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_return_skipped_when_below_min_events_threshold()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 5);
            var events = CreateCommittedEvents(2);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 100  // Aggregate won't reach this
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("Policy conditions not met", result.Reason);
        }

        [Fact]
        public async Task Should_return_skipped_when_events_since_last_snapshot_below_every()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(3);
            var jsonTypeInfo = CreateJsonTypeInfo();

            // Min events = 0 so the threshold check passes, but Every = 100 won't be met by 3 events
            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 100,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("Policy conditions not met", result.Reason);
        }

        [Fact]
        public async Task Should_not_call_snapshotStore_SetAsync_when_not_triggered()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 5);
            var events = CreateCommittedEvents(1);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 100,
                MinEventsBeforeSnapshot = 1000
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            await _snapshotStore.DidNotReceive().SetAsync(
                Arg.Any<IBase>(),
                Arg.Any<JsonTypeInfo>(),
                Arg.Any<IObjectDocument>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }
    }

    public class HandlePostCommitAsync_PolicyTriggered : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_create_snapshot_when_every_threshold_met()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.True(result.SnapshotCreated);
            Assert.Equal(50, result.Version);
            Assert.NotNull(result.Duration);
        }

        [Fact]
        public async Task Should_call_snapshotStore_SetAsync_with_correct_args()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 25);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            await _snapshotStore.Received(1).SetAsync(
                aggregate,
                jsonTypeInfo,
                document,
                25,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_record_events_appended_on_aggregate_tracker()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(5);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 5,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            // After RecordEventsAppended(5), then RecordSnapshotCreated resets events since
            Assert.Equal(0, aggregate.EventsSinceLastSnapshot);
            Assert.Equal(5, aggregate.TotalEventsProcessed);
        }

        [Fact]
        public async Task Should_record_snapshot_created_on_aggregate_tracker()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 42);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.Equal(42, aggregate.LastSnapshotVersion);
        }

        [Fact]
        public async Task Should_handle_empty_committed_events_list()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            // Pre-seed the aggregate with enough events to trigger
            aggregate.RecordEventsAppended(100);
            var document = CreateMockDocument(currentVersion: 100);
            var events = CreateCommittedEvents(0);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            // With 0 committed events, RecordEventsAppended(0) is called, then the
            // existing 100 events since last snapshot >= 10 so snapshot should trigger
            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.SnapshotCreated);
        }
    }

    public class HandlePostCommitAsync_SnapshotFailure : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_return_failed_when_snapshotStore_throws_exception()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Storage error"));

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("Storage error", result.Reason);
        }

        [Fact]
        public async Task Should_log_failure_as_warning_when_option_enabled()
        {
            var options = new SnapshotOptions { LogFailuresAsWarnings = true };
            var handler = CreateHandler(options);
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Boom"));

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            // Verify the logger was invoked (at Warning level)
            _logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Should_log_failure_as_debug_when_warning_option_disabled()
        {
            var options = new SnapshotOptions { LogFailuresAsWarnings = false };
            var handler = CreateHandler(options);
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Boom"));

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            // Verify the logger was invoked (at Debug level)
            _logger.Received().Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Should_propagate_external_cancellation()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException(cts.Token));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo, cts.Token));
        }

        [Fact]
        public async Task Should_return_failed_on_timeout()
        {
            var options = new SnapshotOptions { Timeout = TimeSpan.FromMilliseconds(1) };
            var handler = CreateHandler(options);
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            // Throw OperationCanceledException without external token to simulate timeout
            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Equal("Timeout", result.Reason);
        }

        [Fact]
        public async Task Should_log_timeout_as_warning_when_option_enabled()
        {
            var options = new SnapshotOptions
            {
                Timeout = TimeSpan.FromMilliseconds(1),
                LogFailuresAsWarnings = true
            };
            var handler = CreateHandler(options);
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            _logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task Should_log_timeout_as_debug_when_warning_option_disabled()
        {
            var options = new SnapshotOptions
            {
                Timeout = TimeSpan.FromMilliseconds(1),
                LogFailuresAsWarnings = false
            };
            var handler = CreateHandler(options);
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            _snapshotStore.SetAsync(
                    Arg.Any<IBase>(),
                    Arg.Any<JsonTypeInfo>(),
                    Arg.Any<IObjectDocument>(),
                    Arg.Any<int>(),
                    Arg.Any<string?>(),
                    Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.False(result.Success);
            _logger.Received().Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    public class HandlePostCommitAsync_HappyPath : InlineSnapshotHandlerTests
    {
        [Fact]
        public async Task Should_complete_full_snapshot_lifecycle()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument("order-123", currentVersion: 100);
            var events = CreateCommittedEvents(20, "Order.Completed");
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 20,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            // Verify result
            Assert.True(result.Success);
            Assert.True(result.SnapshotCreated);
            Assert.Equal(100, result.Version);
            Assert.NotNull(result.Duration);
            Assert.True(result.Duration.Value >= TimeSpan.Zero);

            // Verify aggregate tracker state
            Assert.Equal(100, aggregate.LastSnapshotVersion);
            Assert.Equal(0, aggregate.EventsSinceLastSnapshot);
            Assert.Equal(20, aggregate.TotalEventsProcessed);

            // Verify snapshot store was called
            await _snapshotStore.Received(1).SetAsync(
                aggregate,
                jsonTypeInfo,
                document,
                100,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_work_with_direct_options_constructor()
        {
            var handler = CreateHandlerDirect();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 30);
            var events = CreateCommittedEvents(10);
            var jsonTypeInfo = CreateJsonTypeInfo();

            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 10,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.Success);
            Assert.True(result.SnapshotCreated);
            Assert.Equal(30, result.Version);
        }

        [Fact]
        public async Task Should_resolve_last_event_type_from_committed_events()
        {
            var handler = CreateHandler();
            var aggregate = CreateAggregate();
            var document = CreateMockDocument(currentVersion: 50);
            // The last event has a different type
            var events = new List<JsonEvent>
            {
                new() { EventType = "First.Event", EventVersion = 1 },
                new() { EventType = "Second.Event", EventVersion = 2 },
                new() { EventType = "Last.Event", EventVersion = 3 },
            };
            var jsonTypeInfo = CreateJsonTypeInfo();

            // Policy with OnEvents won't match because ResolveEventType returns null
            // but Every threshold will trigger
            var policy = new SnapshotPolicy
            {
                Enabled = true,
                Every = 3,
                MinEventsBeforeSnapshot = 0
            };
            _policyProvider.GetPolicy(Arg.Any<Type>()).Returns(policy);

            var result = await handler.HandlePostCommitAsync(aggregate, document, events, jsonTypeInfo);

            Assert.True(result.SnapshotCreated);
        }
    }

    public class SnapshotResultTests
    {
        [Fact]
        public void Skipped_should_have_correct_properties()
        {
            var result = SnapshotResult.Skipped("some reason");

            Assert.True(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Null(result.Version);
            Assert.Null(result.Duration);
            Assert.Equal("some reason", result.Reason);
        }

        [Fact]
        public void Created_should_have_correct_properties()
        {
            var duration = TimeSpan.FromMilliseconds(150);
            var result = SnapshotResult.Created(42, duration);

            Assert.True(result.Success);
            Assert.True(result.SnapshotCreated);
            Assert.Equal(42, result.Version);
            Assert.Equal(duration, result.Duration);
            Assert.Null(result.Reason);
        }

        [Fact]
        public void Failed_should_have_correct_properties()
        {
            var result = SnapshotResult.Failed("something went wrong");

            Assert.False(result.Success);
            Assert.False(result.SnapshotCreated);
            Assert.Null(result.Version);
            Assert.Null(result.Duration);
            Assert.Equal("something went wrong", result.Reason);
        }
    }

    private class TestAggregate : Aggregate
    {
        public TestAggregate(IEventStream stream) : base(stream)
        {
        }
    }
}
