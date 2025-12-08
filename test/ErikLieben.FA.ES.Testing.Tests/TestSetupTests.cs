using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Time;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests;

public class TestSetupTests
{
    public class GetContextWithServiceProvider
    {
        [Fact]
        public void Should_create_context_with_service_provider()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();

            var context = TestSetup.GetContext(serviceProvider);

            Assert.NotNull(context);
        }

        [Fact]
        public void Should_create_context_with_document_factory()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();

            var context = TestSetup.GetContext(serviceProvider);

            Assert.NotNull(context.DocumentFactory);
        }

        [Fact]
        public void Should_create_context_with_event_stream_factory()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();

            var context = TestSetup.GetContext(serviceProvider);

            Assert.NotNull(context.EventStreamFactory);
        }

        [Fact]
        public void Should_create_context_without_test_clock_by_default()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();

            var context = TestSetup.GetContext(serviceProvider);

            Assert.Null(context.TestClock);
        }
    }

    public class GetContextWithTestClock
    {
        [Fact]
        public void Should_create_context_with_test_clock()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var testClock = new TestClock();

            var context = TestSetup.GetContext(serviceProvider, testClock);

            Assert.Same(testClock, context.TestClock);
        }

        [Fact]
        public void Should_create_context_with_null_test_clock()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();

            var context = TestSetup.GetContext(serviceProvider, (ITestClock?)null);

            Assert.Null(context.TestClock);
        }
    }

    public class GetContextWithAggregateFactorGets
    {
        [Fact]
        public void Should_create_context_with_aggregate_factor_gets()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            Func<Type?, Type> factoryGet = _ => typeof(object);

            var context = TestSetup.GetContext(serviceProvider, factoryGet);

            Assert.NotNull(context);
        }

        [Fact]
        public void Should_create_context_with_multiple_aggregate_factor_gets()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            Func<Type?, Type> factoryGet1 = _ => typeof(object);
            Func<Type?, Type> factoryGet2 = _ => typeof(string);

            var context = TestSetup.GetContext(serviceProvider, factoryGet1, factoryGet2);

            Assert.NotNull(context);
        }
    }

    public class GetContextWithSettings
    {
        [Fact]
        public void Should_create_context_from_settings()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var settings = new TestContextSettings(serviceProvider);

            var context = TestSetup.GetContext(settings);

            Assert.NotNull(context);
        }

        [Fact]
        public void Should_create_context_with_aggregate_factor_gets_from_settings()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            Func<Type?, Type> factoryGet = _ => typeof(object);
            var settings = new TestContextSettings(serviceProvider, factoryGet);

            var context = TestSetup.GetContext(settings);

            Assert.NotNull(context);
        }
    }

    public class TestContextSettingsRecord
    {
        [Fact]
        public void Should_expose_service_provider()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var settings = new TestContextSettings(serviceProvider);

            Assert.Same(serviceProvider, settings.ServiceProvider);
        }

        [Fact]
        public void Should_expose_aggregate_factor_gets()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            Func<Type?, Type> factoryGet = _ => typeof(object);
            var settings = new TestContextSettings(serviceProvider, factoryGet);

            Assert.Single(settings.AggregateFactorGets);
        }

        [Fact]
        public void Should_support_equality()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var settings1 = new TestContextSettings(serviceProvider);
            var settings2 = new TestContextSettings(serviceProvider);

            Assert.Equal(settings1, settings2);
        }
    }
}
