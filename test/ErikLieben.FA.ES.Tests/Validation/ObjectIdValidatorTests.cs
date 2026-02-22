using ErikLieben.FA.ES.Validation;
using Xunit;

namespace ErikLieben.FA.ES.Tests.Validation;

public class ObjectIdValidatorTests
{
    public class Validate
    {
        [Theory]
        [InlineData("simple-id")]
        [InlineData("order_123")]
        [InlineData("my.object.id")]
        [InlineData("abc123")]
        [InlineData("a")]
        [InlineData("A-B_C.D")]
        [InlineData("550e8400-e29b-41d4-a716-446655440000")] // GUID format
        [InlineData("UPPERCASE")]
        [InlineData("MiXeD-CaSe_123")]
        public void Should_accept_valid_object_ids(string objectId)
        {
            // Act & Assert - should not throw
            ObjectIdValidator.Validate(objectId);
        }

        [Fact]
        public void Should_throw_when_id_is_null()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ObjectIdValidator.Validate(null!));
        }

        [Fact]
        public void Should_throw_when_id_is_empty()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => ObjectIdValidator.Validate(string.Empty));
        }

        [Fact]
        public void Should_throw_when_id_is_whitespace()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => ObjectIdValidator.Validate("   "));
        }

        [Theory]
        [InlineData("../secret")]
        [InlineData("path/../traversal")]
        [InlineData("..\\windows")]
        [InlineData("a..b")]
        public void Should_throw_when_id_contains_path_traversal(string objectId)
        {
            // Act
            var exception = Assert.Throws<ArgumentException>(() => ObjectIdValidator.Validate(objectId));

            // Assert
            Assert.Contains("path traversal", exception.Message);
            Assert.Contains("..", exception.Message);
        }

        [Theory]
        [InlineData("path/to/file")]
        [InlineData("path\\to\\file")]
        [InlineData("has spaces")]
        [InlineData("has@symbol")]
        [InlineData("has#hash")]
        [InlineData("has$dollar")]
        [InlineData("has%percent")]
        [InlineData("has&ampersand")]
        [InlineData("has+plus")]
        [InlineData("has=equals")]
        public void Should_throw_when_id_contains_invalid_characters(string objectId)
        {
            // Act
            var exception = Assert.Throws<ArgumentException>(() => ObjectIdValidator.Validate(objectId));

            // Assert
            Assert.Contains("invalid characters", exception.Message);
        }

        [Fact]
        public void Should_accept_single_dot_in_id()
        {
            // A single dot is allowed (e.g., "file.json")
            ObjectIdValidator.Validate("file.json");
        }

        [Fact]
        public void Should_reject_double_dots_even_without_slashes()
        {
            // ".." is a path traversal regardless of surrounding characters
            var exception = Assert.Throws<ArgumentException>(() => ObjectIdValidator.Validate("hello..world"));

            Assert.Contains("path traversal", exception.Message);
        }
    }
}
