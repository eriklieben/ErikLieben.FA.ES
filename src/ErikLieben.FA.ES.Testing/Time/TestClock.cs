namespace ErikLieben.FA.ES.Testing.Time;

/// <summary>
/// Provides a controllable clock implementation for testing time-dependent behavior.
/// Extends <see cref="TimeProvider"/> for compatibility with .NET 8+ time abstractions.
/// </summary>
public class TestClock : TimeProvider, ITestClock
{
    private DateTimeOffset _currentTime;
    private readonly TimeZoneInfo _localTimeZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestClock"/> class.
    /// </summary>
    /// <param name="initialTime">The initial time. If null, uses current UTC time.</param>
    /// <param name="localTimeZone">The local time zone. If null, uses the system local time zone.</param>
    public TestClock(DateTimeOffset? initialTime = null, TimeZoneInfo? localTimeZone = null)
    {
        _currentTime = initialTime ?? DateTimeOffset.UtcNow;
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
        IsFrozen = false;
    }

    /// <summary>
    /// Gets the current date and time.
    /// </summary>
    public DateTimeOffset Now => _currentTime;

    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    public DateTimeOffset UtcNow => _currentTime.ToUniversalTime();

    /// <summary>
    /// Gets a value indicating whether the clock is frozen.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Gets the local time zone.
    /// </summary>
    public override TimeZoneInfo LocalTimeZone => _localTimeZone;

    /// <summary>
    /// Gets the current UTC time (TimeProvider implementation).
    /// </summary>
    /// <returns>The current UTC time as controlled by this test clock.</returns>
    public override DateTimeOffset GetUtcNow() => UtcNow;

    /// <summary>
    /// Sets the current time to the specified value.
    /// </summary>
    /// <param name="time">The time to set.</param>
    public void SetTime(DateTimeOffset time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Sets the current UTC time to the specified value.
    /// Alias for <see cref="SetTime"/> for TimeProvider compatibility.
    /// </summary>
    /// <param name="time">The UTC time to set.</param>
    public void SetUtcNow(DateTimeOffset time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    /// <param name="duration">The duration to advance.</param>
    public void AdvanceBy(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }

    /// <summary>
    /// Advances the current time by the specified duration.
    /// Alias for <see cref="AdvanceBy"/> for TimeProvider compatibility.
    /// </summary>
    /// <param name="delta">The duration to advance.</param>
    public void Advance(TimeSpan delta)
    {
        AdvanceBy(delta);
    }

    /// <summary>
    /// Freezes the clock at the current time.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
    }

    /// <summary>
    /// Unfreezes the clock, allowing time to be advanced.
    /// </summary>
    public void Unfreeze()
    {
        IsFrozen = false;
    }
}
