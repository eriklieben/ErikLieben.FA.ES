using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Testing.Time;

namespace ErikLieben.FA.ES.Testing.Builders;

/// <summary>
/// Provides time manipulation extensions for aggregate test builders.
/// </summary>
public static class TimeTestingExtensions
{
    /// <summary>
    /// Sets the test clock to a specific time before executing the next action.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builderTask">The aggregate test builder task.</param>
    /// <param name="time">The time to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static async Task<AggregateTestBuilder<TAggregate>> AtTime<TAggregate>(
        this Task<AggregateTestBuilder<TAggregate>> builderTask,
        DateTimeOffset time) where TAggregate : IBase
    {
        var builder = await builderTask;
        return builder.AtTime(time);
    }

    /// <summary>
    /// Sets the test clock to a specific time.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builder">The aggregate test builder.</param>
    /// <param name="time">The time to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static AggregateTestBuilder<TAggregate> AtTime<TAggregate>(
        this AggregateTestBuilder<TAggregate> builder,
        DateTimeOffset time) where TAggregate : IBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        var testClock = GetTestClockFromBuilder(builder);
        testClock.SetTime(time);

        return builder;
    }

    /// <summary>
    /// Advances the test clock by the specified duration before executing the next action.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builderTask">The aggregate test builder task.</param>
    /// <param name="duration">The duration to advance.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static async Task<AggregateTestBuilder<TAggregate>> AdvanceTimeBy<TAggregate>(
        this Task<AggregateTestBuilder<TAggregate>> builderTask,
        TimeSpan duration) where TAggregate : IBase
    {
        var builder = await builderTask;
        return builder.AdvanceTimeBy(duration);
    }

    /// <summary>
    /// Advances the test clock by the specified duration.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builder">The aggregate test builder.</param>
    /// <param name="duration">The duration to advance.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static AggregateTestBuilder<TAggregate> AdvanceTimeBy<TAggregate>(
        this AggregateTestBuilder<TAggregate> builder,
        TimeSpan duration) where TAggregate : IBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        var testClock = GetTestClockFromBuilder(builder);
        testClock.AdvanceBy(duration);

        return builder;
    }

    /// <summary>
    /// Freezes the test clock at the current time.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builderTask">The aggregate test builder task.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static async Task<AggregateTestBuilder<TAggregate>> FreezeTime<TAggregate>(
        this Task<AggregateTestBuilder<TAggregate>> builderTask) where TAggregate : IBase
    {
        var builder = await builderTask;
        return builder.FreezeTime();
    }

    /// <summary>
    /// Freezes the test clock at the current time.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builder">The aggregate test builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static AggregateTestBuilder<TAggregate> FreezeTime<TAggregate>(
        this AggregateTestBuilder<TAggregate> builder) where TAggregate : IBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        var testClock = GetTestClockFromBuilder(builder);
        testClock.Freeze();

        return builder;
    }

    /// <summary>
    /// Unfreezes the test clock, allowing time to advance.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builderTask">The aggregate test builder task.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static async Task<AggregateTestBuilder<TAggregate>> UnfreezeTime<TAggregate>(
        this Task<AggregateTestBuilder<TAggregate>> builderTask) where TAggregate : IBase
    {
        var builder = await builderTask;
        return builder.UnfreezeTime();
    }

    /// <summary>
    /// Unfreezes the test clock, allowing time to advance.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type.</typeparam>
    /// <param name="builder">The aggregate test builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the test context doesn't have a test clock configured.</exception>
    public static AggregateTestBuilder<TAggregate> UnfreezeTime<TAggregate>(
        this AggregateTestBuilder<TAggregate> builder) where TAggregate : IBase
    {
        ArgumentNullException.ThrowIfNull(builder);

        var testClock = GetTestClockFromBuilder(builder);
        testClock.Unfreeze();

        return builder;
    }

    // AOT-friendly: uses internal Context property instead of reflection
    private static ITestClock GetTestClockFromBuilder<TAggregate>(AggregateTestBuilder<TAggregate> builder)
        where TAggregate : IBase
    {
        var testClock = builder.Context.TestClock;
        if (testClock == null)
        {
            throw new InvalidOperationException(
                "Test clock is not configured. Create TestContext with a TestClock instance.");
        }

        return testClock;
    }
}
