using ErikLieben.FA.ES.Testing.Performance;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Performance;

public class PerformanceMeasurementTests
{
    public class Measure
    {
        [Fact]
        public void Should_throw_when_operation_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => PerformanceMeasurement.Measure(null!));
        }

        [Fact]
        public void Should_throw_when_operation_count_is_zero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.Measure(() => { }, operationCount: 0));
        }

        [Fact]
        public void Should_throw_when_operation_count_is_negative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.Measure(() => { }, operationCount: -1));
        }

        [Fact]
        public async Task Should_measure_elapsed_time()
        {
            var metrics = await PerformanceMeasurement.MeasureAsync(async () =>
            {
                await Task.Delay(50);
            }, warmup: false);

            Assert.True(metrics.ElapsedTime.TotalMilliseconds >= 40);
        }

        [Fact]
        public void Should_set_operation_count()
        {
            var metrics = PerformanceMeasurement.Measure(() => { }, operationCount: 42, warmup: false);

            Assert.Equal(42, metrics.OperationCount);
        }

        [Fact]
        public void Should_run_warmup_when_enabled()
        {
            var runCount = 0;

            PerformanceMeasurement.Measure(() => runCount++, warmup: true);

            Assert.Equal(2, runCount); // warmup + actual
        }

        [Fact]
        public void Should_skip_warmup_when_disabled()
        {
            var runCount = 0;

            PerformanceMeasurement.Measure(() => runCount++, warmup: false);

            Assert.Equal(1, runCount); // actual only
        }
    }

    public class MeasureAsync
    {
        [Fact]
        public async Task Should_throw_when_operation_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                PerformanceMeasurement.MeasureAsync(null!));
        }

        [Fact]
        public async Task Should_throw_when_operation_count_is_zero()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureAsync(() => Task.CompletedTask, operationCount: 0));
        }

        [Fact]
        public async Task Should_throw_when_operation_count_is_negative()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureAsync(() => Task.CompletedTask, operationCount: -1));
        }

        [Fact]
        public async Task Should_measure_elapsed_time()
        {
            var metrics = await PerformanceMeasurement.MeasureAsync(async () =>
            {
                await Task.Delay(50);
            }, warmup: false);

            Assert.True(metrics.ElapsedTime.TotalMilliseconds >= 40);
        }

        [Fact]
        public async Task Should_set_operation_count()
        {
            var metrics = await PerformanceMeasurement.MeasureAsync(
                () => Task.CompletedTask, operationCount: 42, warmup: false);

            Assert.Equal(42, metrics.OperationCount);
        }

        [Fact]
        public async Task Should_run_warmup_when_enabled()
        {
            var runCount = 0;

            await PerformanceMeasurement.MeasureAsync(() =>
            {
                runCount++;
                return Task.CompletedTask;
            }, warmup: true);

            Assert.Equal(2, runCount); // warmup + actual
        }
    }

    public class MeasureRepeated
    {
        [Fact]
        public void Should_throw_when_operation_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PerformanceMeasurement.MeasureRepeated(null!, 10));
        }

        [Fact]
        public void Should_throw_when_iterations_is_zero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureRepeated(() => { }, 0));
        }

        [Fact]
        public void Should_throw_when_iterations_is_negative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureRepeated(() => { }, -1));
        }

        [Fact]
        public void Should_run_operation_specified_times()
        {
            var runCount = 0;

            PerformanceMeasurement.MeasureRepeated(() => runCount++, 5, warmup: false);

            Assert.Equal(5, runCount);
        }

        [Fact]
        public void Should_set_operation_count_to_iterations()
        {
            var metrics = PerformanceMeasurement.MeasureRepeated(() => { }, 10, warmup: false);

            Assert.Equal(10, metrics.OperationCount);
        }
    }

    public class MeasureRepeatedAsync
    {
        [Fact]
        public async Task Should_throw_when_operation_is_null()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                PerformanceMeasurement.MeasureRepeatedAsync(null!, 10));
        }

        [Fact]
        public async Task Should_throw_when_iterations_is_zero()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureRepeatedAsync(() => Task.CompletedTask, 0));
        }

        [Fact]
        public async Task Should_throw_when_iterations_is_negative()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                PerformanceMeasurement.MeasureRepeatedAsync(() => Task.CompletedTask, -1));
        }

        [Fact]
        public async Task Should_run_operation_specified_times()
        {
            var runCount = 0;

            await PerformanceMeasurement.MeasureRepeatedAsync(() =>
            {
                runCount++;
                return Task.CompletedTask;
            }, 5, warmup: false);

            Assert.Equal(5, runCount);
        }

        [Fact]
        public async Task Should_set_operation_count_to_iterations()
        {
            var metrics = await PerformanceMeasurement.MeasureRepeatedAsync(
                () => Task.CompletedTask, 10, warmup: false);

            Assert.Equal(10, metrics.OperationCount);
        }
    }
}
