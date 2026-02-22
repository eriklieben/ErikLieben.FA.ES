namespace ErikLieben.FA.ES.Testing.Time;

/// <summary>
/// Provides a testable clock for controlling time in tests.
/// </summary>
public interface ITestClock
{
    /// <summary>
    /// Gets the current date and time.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Sets the current time to the specified value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    void SetTime(DateTimeOffset time);

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    /// <param name="duration">The duration to advance.</param>
    void AdvanceBy(TimeSpan duration);

    /// <summary>
    /// Freezes the clock at the current time.
    /// </summary>
    void Freeze();

    /// <summary>
    /// Unfreezes the clock, allowing time to advance.
    /// </summary>
    void Unfreeze();

    /// <summary>
    /// Gets a value indicating whether the clock is frozen.
    /// </summary>
    bool IsFrozen { get; }
}
