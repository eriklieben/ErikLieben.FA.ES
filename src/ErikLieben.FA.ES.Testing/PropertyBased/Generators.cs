namespace ErikLieben.FA.ES.Testing.PropertyBased;

/// <summary>
/// Provides built-in generators for common types and patterns.
/// </summary>
public static class Generators
{
    /// <summary>
    /// Creates a generator for random strings.
    /// </summary>
    /// <param name="minLength">The minimum string length.</param>
    /// <param name="maxLength">The maximum string length.</param>
    /// <param name="chars">Optional character set to use. If null, uses alphanumeric characters.</param>
    /// <returns>A string generator.</returns>
    public static IGenerator<string> String(int minLength = 0, int maxLength = 100, string? chars = null)
    {
        chars ??= "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new StringGenerator(minLength, maxLength, chars);
    }

    /// <summary>
    /// Creates a generator for random integers.
    /// </summary>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <returns>An integer generator.</returns>
    public static IGenerator<int> Int(int min = int.MinValue, int max = int.MaxValue)
    {
        return new IntGenerator(min, max);
    }

    /// <summary>
    /// Creates a generator for random decimals.
    /// </summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>A decimal generator.</returns>
    public static IGenerator<decimal> Decimal(decimal min = 0m, decimal max = 1000000m)
    {
        return new DecimalGenerator(min, max);
    }

    /// <summary>
    /// Creates a generator that randomly selects from a set of values.
    /// </summary>
    /// <typeparam name="T">The type of values to select from.</typeparam>
    /// <param name="values">The values to choose from.</param>
    /// <returns>A generator that selects one of the provided values.</returns>
    public static IGenerator<T> OneOf<T>(params T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException("Must provide at least one value.", nameof(values));
        }

        return new OneOfGenerator<T>(values);
    }

    /// <summary>
    /// Creates a generator for lists of values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="elementGenerator">The generator for list elements.</param>
    /// <param name="minCount">The minimum list size.</param>
    /// <param name="maxCount">The maximum list size.</param>
    /// <returns>A list generator.</returns>
    public static IGenerator<List<T>> ListOf<T>(
        IGenerator<T> elementGenerator,
        int minCount = 0,
        int maxCount = 10)
    {
        ArgumentNullException.ThrowIfNull(elementGenerator);
        return new ListGenerator<T>(elementGenerator, minCount, maxCount);
    }

    /// <summary>
    /// Creates a custom generator from a function.
    /// </summary>
    /// <typeparam name="T">The type to generate.</typeparam>
    /// <param name="generator">The generation function.</param>
    /// <returns>A custom generator.</returns>
    public static IGenerator<T> Custom<T>(Func<Random, T> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        return new CustomGenerator<T>(generator);
    }

    /// <summary>
    /// Creates a generator for boolean values.
    /// </summary>
    /// <returns>A boolean generator.</returns>
    public static IGenerator<bool> Bool()
    {
        return new BoolGenerator();
    }

    /// <summary>
    /// Creates a generator for dates within a range.
    /// </summary>
    /// <param name="min">The minimum date.</param>
    /// <param name="max">The maximum date.</param>
    /// <returns>A DateTimeOffset generator.</returns>
    public static IGenerator<DateTimeOffset> Date(DateTimeOffset? min = null, DateTimeOffset? max = null)
    {
        var minDate = min ?? new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var maxDate = max ?? new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero);
        return new DateGenerator(minDate, maxDate);
    }

    /// <summary>
    /// Creates a generator for GUIDs.
    /// </summary>
    /// <returns>A GUID generator.</returns>
    public static IGenerator<Guid> Guid()
    {
        return new GuidGenerator();
    }

    // Generator implementations
    private sealed class StringGenerator : IGenerator<string>
    {
        private readonly int _minLength;
        private readonly int _maxLength;
        private readonly string _chars;

        public StringGenerator(int minLength, int maxLength, string chars)
        {
            _minLength = minLength;
            _maxLength = maxLength;
            _chars = chars;
        }

        public string Generate(Random random)
        {
            var length = random.Next(_minLength, _maxLength + 1);
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = _chars[random.Next(_chars.Length)];
            }
            return new string(chars);
        }
    }

    private sealed class IntGenerator : IGenerator<int>
    {
        private readonly int _min;
        private readonly int _max;

        public IntGenerator(int min, int max)
        {
            _min = min;
            _max = max;
        }

        public int Generate(Random random)
        {
            if (_min == int.MinValue && _max == int.MaxValue)
            {
                return random.Next();
            }
            return random.Next(_min, _max);
        }
    }

    private sealed class DecimalGenerator : IGenerator<decimal>
    {
        private readonly decimal _min;
        private readonly decimal _max;

        public DecimalGenerator(decimal min, decimal max)
        {
            _min = min;
            _max = max;
        }

        public decimal Generate(Random random)
        {
            var range = _max - _min;
            var sample = (decimal)random.NextDouble();
            return _min + (sample * range);
        }
    }

    private sealed class OneOfGenerator<T> : IGenerator<T>
    {
        private readonly T[] _values;

        public OneOfGenerator(T[] values)
        {
            _values = values;
        }

        public T Generate(Random random)
        {
            return _values[random.Next(_values.Length)];
        }
    }

    private sealed class ListGenerator<T> : IGenerator<List<T>>
    {
        private readonly IGenerator<T> _elementGenerator;
        private readonly int _minCount;
        private readonly int _maxCount;

        public ListGenerator(IGenerator<T> elementGenerator, int minCount, int maxCount)
        {
            _elementGenerator = elementGenerator;
            _minCount = minCount;
            _maxCount = maxCount;
        }

        public List<T> Generate(Random random)
        {
            var count = random.Next(_minCount, _maxCount + 1);
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(_elementGenerator.Generate(random));
            }
            return list;
        }
    }

    private sealed class CustomGenerator<T> : IGenerator<T>
    {
        private readonly Func<Random, T> _generator;

        public CustomGenerator(Func<Random, T> generator)
        {
            _generator = generator;
        }

        public T Generate(Random random)
        {
            return _generator(random);
        }
    }

    private sealed class BoolGenerator : IGenerator<bool>
    {
        public bool Generate(Random random)
        {
            return random.Next(2) == 1;
        }
    }

    private sealed class DateGenerator : IGenerator<DateTimeOffset>
    {
        private readonly DateTimeOffset _min;
        private readonly DateTimeOffset _max;

        public DateGenerator(DateTimeOffset min, DateTimeOffset max)
        {
            _min = min;
            _max = max;
        }

        public DateTimeOffset Generate(Random random)
        {
            var range = _max - _min;
            var randomTicks = (long)(random.NextDouble() * range.Ticks);
            return _min.AddTicks(randomTicks);
        }
    }

    private sealed class GuidGenerator : IGenerator<Guid>
    {
        public Guid Generate(Random random)
        {
            return System.Guid.NewGuid();
        }
    }
}
