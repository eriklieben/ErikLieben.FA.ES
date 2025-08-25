using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Tests.Projections
{
       public class VersionTokenComparerTests
    {
        public class Compare
        {
            [Fact]
            public void Should_return_zero_when_strings_are_equal()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                var token = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";

                // Act
                var result = sut.Compare(token, token);

                // Assert
                Assert.Equal(0, result);
            }

            [Fact]
            public void Should_return_minus_one_when_x_is_null_and_y_is_not()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                string? x = null;
                string y = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";

                // Act
                var result = sut.Compare(x, y);

                // Assert
                Assert.Equal(-1, result);
            }

            [Fact]
            public void Should_return_one_when_x_is_not_null_and_y_is_null()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                string x = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";
                string? y = null;

                // Act
                var result = sut.Compare(x, y);

                // Assert
                Assert.Equal(1, result);
            }

            [Fact]
            public void Should_throw_exception_when_comparing_different_objects()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                string x = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";
                string y = "Project__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";

                // Act & Assert
                var exception = Assert.Throws<Exception>(() => sut.Compare(x, y));
                Assert.Equal("it seems you are compare different streams", exception.Message);
            }

            [Theory]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                -1)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                1)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000010",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                1)]
            public void Should_compare_version_identifiers_correctly(string x, string y, int expected)
            {
                // Arrange
                var sut = new VersionTokenComparer();

                // Act
                var result = sut.Compare(x, y);

                // Assert
                Assert.Equal(expected, result);
            }

            [Theory]
            [InlineData(
                "ACCOUNT__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                "account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                0)]
            public void Should_compare_ignoring_case(string x, string y, int expected)
            {
                // Arrange
                var sut = new VersionTokenComparer();

                // Act
                var result = sut.Compare(x, y);

                // Assert
                Assert.Equal(expected, result);
            }
        }

        public class IsNewer
        {
            [Fact]
            public void Should_return_true_when_existing_is_null()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                string @new = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";
                string? existing = null;

                // Act
                var result = sut.IsNewer(@new, existing);

                // Assert
                Assert.True(result);
            }

            [Theory]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                true)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                false)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000010",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                true)]
            public void Should_correctly_determine_if_version_is_newer(string @new, string existing, bool expected)
            {
                // Arrange
                var sut = new VersionTokenComparer();

                // Act
                var result = sut.IsNewer(@new, existing);

                // Assert
                Assert.Equal(expected, result);
            }
        }

        public class IsOlder
        {
            [Theory]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                true)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001",
                false)]
            [InlineData(
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000002",
                "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000010",
                true)]
            public void Should_correctly_determine_if_version_is_older(string @new, string existing, bool expected)
            {
                // Arrange
                var sut = new VersionTokenComparer();

                // Act
                var result = sut.IsOlder(@new, existing);

                // Assert
                Assert.Equal(expected, result);
            }

            [Fact]
            public void Should_return_false_when_existing_is_null()
            {
                // Arrange
                var sut = new VersionTokenComparer();
                string @new = "Account__12345678-abcd-1234-efgh-123456789012__1234567890abcdef12345678900000000001__00000000000000000001";
                string? existing = null;

                // Act
                var result = sut.IsOlder(@new, existing);

                // Assert
                Assert.False(result);
            }
        }
    }
}
