using ErikLieben.FA.ES.Testing.Assertions;
using ErikLieben.FA.ES.Testing.Performance;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Performance;

public class PerformanceAssertionTests
{
    public class Constructor
    {
        [Fact]
        public void Should_throw_when_metrics_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new PerformanceAssertion(null!));
        }

        [Fact]
        public void Should_expose_metrics()
        {
            var metrics = new PerformanceMetrics();
            var assertion = new PerformanceAssertion(metrics);

            Assert.Same(metrics, assertion.Metrics);
        }
    }

    public class ShouldCompleteWithin
    {
        [Fact]
        public void Should_pass_when_elapsed_time_is_less_than_max()
        {
            var metrics = new PerformanceMetrics { ElapsedTime = TimeSpan.FromMilliseconds(50) };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldCompleteWithin(TimeSpan.FromMilliseconds(100));

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_pass_when_elapsed_time_equals_max()
        {
            var metrics = new PerformanceMetrics { ElapsedTime = TimeSpan.FromMilliseconds(100) };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldCompleteWithin(TimeSpan.FromMilliseconds(100));

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_elapsed_time_exceeds_max()
        {
            var metrics = new PerformanceMetrics { ElapsedTime = TimeSpan.FromMilliseconds(150) };
            var assertion = new PerformanceAssertion(metrics);

            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldCompleteWithin(TimeSpan.FromMilliseconds(100)));

            Assert.Contains("150", ex.Message);
            Assert.Contains("100", ex.Message);
        }
    }

    public class ShouldHaveThroughputOf
    {
        [Fact]
        public void Should_pass_when_throughput_exceeds_minimum()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromSeconds(1),
                OperationCount = 100
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldHaveThroughputOf(50);

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_pass_when_throughput_equals_minimum()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromSeconds(1),
                OperationCount = 50
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldHaveThroughputOf(50);

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_throughput_is_below_minimum()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromSeconds(1),
                OperationCount = 30
            };
            var assertion = new PerformanceAssertion(metrics);

            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveThroughputOf(50));

            Assert.Contains("30", ex.Message);
            Assert.Contains("50", ex.Message);
        }
    }

    public class ShouldAllocateLessThan
    {
        [Fact]
        public void Should_pass_when_allocation_is_less_than_max()
        {
            var metrics = new PerformanceMetrics { AllocatedBytes = 500 };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldAllocateLessThan(1000);

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_pass_when_allocation_equals_max()
        {
            var metrics = new PerformanceMetrics { AllocatedBytes = 1000 };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldAllocateLessThan(1000);

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_allocation_exceeds_max()
        {
            var metrics = new PerformanceMetrics { AllocatedBytes = 2000 };
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldAllocateLessThan(1000));
        }
    }

    public class ShouldAllocateLessThanPerOperation
    {
        [Fact]
        public void Should_pass_when_allocation_per_op_is_less_than_max()
        {
            var metrics = new PerformanceMetrics
            {
                AllocatedBytes = 100,
                OperationCount = 10
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldAllocateLessThanPerOperation(20);

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_allocation_per_op_exceeds_max()
        {
            var metrics = new PerformanceMetrics
            {
                AllocatedBytes = 300,
                OperationCount = 10
            };
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldAllocateLessThanPerOperation(20));
        }
    }

    public class ShouldNotCauseGarbageCollection
    {
        [Fact]
        public void Should_pass_when_no_collections_occurred()
        {
            var metrics = new PerformanceMetrics
            {
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldNotCauseGarbageCollection();

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_gen0_collection_occurred()
        {
            var metrics = new PerformanceMetrics { Gen0Collections = 1 };
            var assertion = new PerformanceAssertion(metrics);

            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotCauseGarbageCollection());

            Assert.Contains("Gen0: 1", ex.Message);
        }

        [Fact]
        public void Should_throw_when_gen1_collection_occurred()
        {
            var metrics = new PerformanceMetrics { Gen1Collections = 1 };
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotCauseGarbageCollection());
        }

        [Fact]
        public void Should_throw_when_gen2_collection_occurred()
        {
            var metrics = new PerformanceMetrics { Gen2Collections = 1 };
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotCauseGarbageCollection());
        }
    }

    public class ShouldNotCauseGen2Collection
    {
        [Fact]
        public void Should_pass_when_no_gen2_collections_occurred()
        {
            var metrics = new PerformanceMetrics
            {
                Gen0Collections = 5,
                Gen1Collections = 2,
                Gen2Collections = 0
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldNotCauseGen2Collection();

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_gen2_collection_occurred()
        {
            var metrics = new PerformanceMetrics { Gen2Collections = 1 };
            var assertion = new PerformanceAssertion(metrics);

            var ex = Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldNotCauseGen2Collection());

            Assert.Contains("Gen2", ex.Message);
        }
    }

    public class ShouldHaveAverageTimePerOperationOf
    {
        [Fact]
        public void Should_pass_when_average_time_is_less_than_max()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(50),
                OperationCount = 10
            };
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.ShouldHaveAverageTimePerOperationOf(TimeSpan.FromMilliseconds(10));

            Assert.Same(assertion, result);
        }

        [Fact]
        public void Should_throw_when_average_time_exceeds_max()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(100),
                OperationCount = 5
            };
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<TestAssertionException>(() =>
                assertion.ShouldHaveAverageTimePerOperationOf(TimeSpan.FromMilliseconds(10)));
        }
    }

    public class Should
    {
        [Fact]
        public void Should_throw_when_assertion_is_null()
        {
            var metrics = new PerformanceMetrics();
            var assertion = new PerformanceAssertion(metrics);

            Assert.Throws<ArgumentNullException>(() => assertion.Should(null!));
        }

        [Fact]
        public void Should_execute_custom_assertion()
        {
            var metrics = new PerformanceMetrics { OperationCount = 42 };
            var assertion = new PerformanceAssertion(metrics);
            var wasExecuted = false;

            assertion.Should(m =>
            {
                wasExecuted = true;
                Assert.Equal(42, m.OperationCount);
            });

            Assert.True(wasExecuted);
        }

        [Fact]
        public void Should_return_assertion_for_chaining()
        {
            var metrics = new PerformanceMetrics();
            var assertion = new PerformanceAssertion(metrics);

            var result = assertion.Should(_ => { });

            Assert.Same(assertion, result);
        }
    }

    public class Chaining
    {
        [Fact]
        public void Should_allow_chaining_multiple_assertions()
        {
            var metrics = new PerformanceMetrics
            {
                ElapsedTime = TimeSpan.FromMilliseconds(50),
                OperationCount = 100,
                AllocatedBytes = 500,
                Gen0Collections = 0,
                Gen1Collections = 0,
                Gen2Collections = 0
            };

            var assertion = new PerformanceAssertion(metrics)
                .ShouldCompleteWithin(TimeSpan.FromMilliseconds(100))
                .ShouldHaveThroughputOf(1000)
                .ShouldAllocateLessThan(1000)
                .ShouldNotCauseGarbageCollection();

            Assert.NotNull(assertion);
        }
    }
}
