using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Testing.Builders.Data;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Builders;

public class EventBuilderTests
{
    [EventName("test.event.created")]
    public class TestEvent : IEvent
    {
        public string Name { get; init; } = "default";
        public int Value { get; init; } = 0;

        // IEvent implementation
        public string? Payload { get; init; }
        public string EventType { get; init; } = "test.event.created";
        public int EventVersion { get; init; }
        public int SchemaVersion { get; init; } = 1;
        public string? ExternalSequencer { get; init; }
        public ActionMetadata? ActionMetadata { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();
    }

    public class TestEventBuilder : EventBuilder<TestEvent, TestEventBuilder>
    {
        private string _name = "default";
        private int _value = 0;

        public TestEventBuilder WithName(string name)
        {
            _name = name;
            return This();
        }

        public TestEventBuilder WithValue(int value)
        {
            _value = value;
            return This();
        }

        protected override TestEvent Build()
        {
            return new TestEvent { Name = _name, Value = _value };
        }
    }

    public class ImplicitConversion
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            TestEventBuilder? builder = null;

            Assert.Throws<ArgumentNullException>(() =>
            {
                TestEvent _ = builder!;
            });
        }

        [Fact]
        public void Should_convert_builder_to_event()
        {
            var builder = new TestEventBuilder()
                .WithName("Test")
                .WithValue(42);

            TestEvent result = builder;

            Assert.Equal("Test", result.Name);
            Assert.Equal(42, result.Value);
        }
    }

    public class FluentChaining
    {
        [Fact]
        public void Should_return_same_builder_for_chaining()
        {
            var builder = new TestEventBuilder();

            var result = builder.WithName("Test").WithValue(42);

            Assert.Same(builder, result);
        }
    }
}

public class EventListBuilderTests
{
    [EventName("test.event.created")]
    public class TestEvent : IEvent
    {
        public string Name { get; init; } = "default";

        // IEvent implementation
        public string? Payload { get; init; }
        public string EventType { get; init; } = "test.event.created";
        public int EventVersion { get; init; }
        public int SchemaVersion { get; init; } = 1;
        public string? ExternalSequencer { get; init; }
        public ActionMetadata? ActionMetadata { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();
    }

    public class Add
    {
        [Fact]
        public void Should_add_single_event()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" });

            var result = builder.Build();

            Assert.Single(result);
            Assert.Equal("Event1", result[0].Name);
        }

        [Fact]
        public void Should_add_multiple_events_sequentially()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" })
                .Add(new TestEvent { Name = "Event2" })
                .Add(new TestEvent { Name = "Event3" });

            var result = builder.Build();

            Assert.Equal(3, result.Count);
        }
    }

    public class AddRange
    {
        [Fact]
        public void Should_add_multiple_events_at_once()
        {
            var builder = new EventListBuilder<TestEvent>()
                .AddRange(
                    new TestEvent { Name = "Event1" },
                    new TestEvent { Name = "Event2" },
                    new TestEvent { Name = "Event3" });

            var result = builder.Build();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Should_handle_empty_array()
        {
            var builder = new EventListBuilder<TestEvent>()
                .AddRange();

            var result = builder.Build();

            Assert.Empty(result);
        }
    }

    public class AddMany
    {
        [Fact]
        public void Should_add_events_using_factory()
        {
            var builder = new EventListBuilder<TestEvent>()
                .AddMany(3, i => new TestEvent { Name = $"Event{i}" });

            var result = builder.Build();

            Assert.Equal(3, result.Count);
            Assert.Equal("Event0", result[0].Name);
            Assert.Equal("Event1", result[1].Name);
            Assert.Equal("Event2", result[2].Name);
        }

        [Fact]
        public void Should_handle_zero_count()
        {
            var builder = new EventListBuilder<TestEvent>()
                .AddMany(0, i => new TestEvent { Name = $"Event{i}" });

            var result = builder.Build();

            Assert.Empty(result);
        }
    }

    public class Build
    {
        [Fact]
        public void Should_return_list()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" });

            var result = builder.Build();

            Assert.IsType<List<TestEvent>>(result);
        }
    }

    public class BuildArray
    {
        [Fact]
        public void Should_return_array()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" })
                .Add(new TestEvent { Name = "Event2" });

            var result = builder.BuildArray();

            Assert.IsType<TestEvent[]>(result);
            Assert.Equal(2, result.Length);
        }
    }

    public class ImplicitConversionToList
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            EventListBuilder<TestEvent>? builder = null;

            Assert.Throws<ArgumentNullException>(() =>
            {
                List<TestEvent> _ = builder!;
            });
        }

        [Fact]
        public void Should_convert_builder_to_list()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" });

            List<TestEvent> result = builder;

            Assert.Single(result);
        }
    }

    public class ImplicitConversionToArray
    {
        [Fact]
        public void Should_throw_when_builder_is_null()
        {
            EventListBuilder<TestEvent>? builder = null;

            Assert.Throws<ArgumentNullException>(() =>
            {
                TestEvent[] _ = builder!;
            });
        }

        [Fact]
        public void Should_convert_builder_to_array()
        {
            var builder = new EventListBuilder<TestEvent>()
                .Add(new TestEvent { Name = "Event1" });

            TestEvent[] result = builder;

            Assert.Single(result);
        }
    }
}

public class EventBuilderExtensionsTests
{
    [EventName("test.event.created")]
    public class TestEvent : IEvent
    {
        public string Name { get; init; } = "default";

        // IEvent implementation
        public string? Payload { get; init; }
        public string EventType { get; init; } = "test.event.created";
        public int EventVersion { get; init; }
        public int SchemaVersion { get; init; } = 1;
        public string? ExternalSequencer { get; init; }
        public ActionMetadata? ActionMetadata { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();
    }

    [Fact]
    public void Events_should_create_new_builder()
    {
        var builder = EventBuilderExtensions.Events<TestEvent>();

        Assert.NotNull(builder);
        Assert.IsType<EventListBuilder<TestEvent>>(builder);
    }

    [Fact]
    public void Events_should_create_empty_builder()
    {
        var builder = EventBuilderExtensions.Events<TestEvent>();

        var result = builder.Build();

        Assert.Empty(result);
    }
}
