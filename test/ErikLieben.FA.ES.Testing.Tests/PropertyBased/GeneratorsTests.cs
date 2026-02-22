using ErikLieben.FA.ES.Testing.PropertyBased;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.PropertyBased;

public class GeneratorsTests
{
    private readonly Random _random = new(42); // Fixed seed for reproducibility

    public class StringGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_generate_string_within_length_bounds()
        {
            var generator = Generators.String(minLength: 5, maxLength: 10);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.InRange(result.Length, 5, 10);
            }
        }

        [Fact]
        public void Should_generate_empty_string_when_min_is_zero()
        {
            var generator = Generators.String(minLength: 0, maxLength: 0);

            var result = generator.Generate(_random);

            Assert.Empty(result);
        }

        [Fact]
        public void Should_use_custom_character_set()
        {
            var generator = Generators.String(minLength: 10, maxLength: 10, chars: "ABC");

            var result = generator.Generate(_random);

            Assert.All(result, c => Assert.Contains(c, "ABC"));
        }

        [Fact]
        public void Should_use_alphanumeric_by_default()
        {
            var generator = Generators.String(minLength: 100, maxLength: 100);
            var alphanumeric = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            var result = generator.Generate(_random);

            Assert.All(result, c => Assert.Contains(c, alphanumeric));
        }
    }

    public class IntGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_generate_int_within_bounds()
        {
            var generator = Generators.Int(min: 10, max: 20);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.InRange(result, 10, 19); // max is exclusive
            }
        }

        [Fact]
        public void Should_handle_default_bounds()
        {
            var generator = Generators.Int();

            var result = generator.Generate(_random);

            Assert.IsType<int>(result);
        }

        [Fact]
        public void Should_generate_single_value_when_range_is_one()
        {
            var generator = Generators.Int(min: 42, max: 43);

            var result = generator.Generate(_random);

            Assert.Equal(42, result);
        }
    }

    public class DecimalGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_generate_decimal_within_bounds()
        {
            var generator = Generators.Decimal(min: 10m, max: 20m);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.InRange(result, 10m, 20m);
            }
        }

        [Fact]
        public void Should_use_default_bounds()
        {
            var generator = Generators.Decimal();

            var result = generator.Generate(_random);

            Assert.InRange(result, 0m, 1000000m);
        }
    }

    public class OneOfGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_throw_when_values_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => Generators.OneOf<string>(null!));
        }

        [Fact]
        public void Should_throw_when_values_is_empty()
        {
            Assert.Throws<ArgumentException>(() => Generators.OneOf<int>());
        }

        [Fact]
        public void Should_return_one_of_the_provided_values()
        {
            var values = new[] { "a", "b", "c" };
            var generator = Generators.OneOf(values);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.Contains(result, values);
            }
        }

        [Fact]
        public void Should_return_single_value_when_only_one_provided()
        {
            var generator = Generators.OneOf(42);

            var result = generator.Generate(_random);

            Assert.Equal(42, result);
        }
    }

    public class ListGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_throw_when_element_generator_is_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Generators.ListOf<int>(null!));
        }

        [Fact]
        public void Should_generate_list_within_size_bounds()
        {
            var elementGenerator = Generators.Int(0, 100);
            var generator = Generators.ListOf(elementGenerator, minCount: 3, maxCount: 5);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.InRange(result.Count, 3, 5);
            }
        }

        [Fact]
        public void Should_generate_empty_list_when_min_count_is_zero()
        {
            var elementGenerator = Generators.Int();
            var generator = Generators.ListOf(elementGenerator, minCount: 0, maxCount: 0);

            var result = generator.Generate(_random);

            Assert.Empty(result);
        }

        [Fact]
        public void Should_use_element_generator_for_each_item()
        {
            var elementGenerator = Generators.String(minLength: 5, maxLength: 5);
            var generator = Generators.ListOf(elementGenerator, minCount: 10, maxCount: 10);

            var result = generator.Generate(_random);

            Assert.All(result, s => Assert.Equal(5, s.Length));
        }
    }

    public class CustomGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_throw_when_generator_func_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => Generators.Custom<int>(null!));
        }

        [Fact]
        public void Should_use_custom_generator_function()
        {
            var generator = Generators.Custom(r => r.Next(100, 200));

            var result = generator.Generate(_random);

            Assert.InRange(result, 100, 199);
        }

        [Fact]
        public void Should_pass_random_to_generator_function()
        {
            Random? capturedRandom = null;
            var generator = Generators.Custom(r =>
            {
                capturedRandom = r;
                return 42;
            });

            generator.Generate(_random);

            Assert.Same(_random, capturedRandom);
        }
    }

    public class BoolGenerator
    {
        [Fact]
        public void Should_generate_both_true_and_false()
        {
            var generator = Generators.Bool();
            var random = new Random(42);
            var trueCount = 0;
            var falseCount = 0;

            for (int i = 0; i < 100; i++)
            {
                if (generator.Generate(random))
                    trueCount++;
                else
                    falseCount++;
            }

            Assert.True(trueCount > 0, "Should generate at least one true");
            Assert.True(falseCount > 0, "Should generate at least one false");
        }
    }

    public class DateGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_generate_date_within_bounds()
        {
            var min = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var max = new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero);
            var generator = Generators.Date(min, max);

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.InRange(result, min, max);
            }
        }

        [Fact]
        public void Should_use_default_date_range()
        {
            var generator = Generators.Date();
            var defaultMin = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var defaultMax = new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero);

            var result = generator.Generate(_random);

            Assert.InRange(result, defaultMin, defaultMax);
        }
    }

    public class GuidGenerator
    {
        private readonly Random _random = new(42);

        [Fact]
        public void Should_generate_unique_guids()
        {
            var generator = Generators.Guid();
            var guids = new HashSet<Guid>();

            for (int i = 0; i < 100; i++)
            {
                var result = generator.Generate(_random);
                Assert.True(guids.Add(result), "Generated duplicate GUID");
            }
        }

        [Fact]
        public void Should_not_generate_empty_guid()
        {
            var generator = Generators.Guid();

            var result = generator.Generate(_random);

            Assert.NotEqual(Guid.Empty, result);
        }
    }
}
