using ErikLieben.FA.ES.AzureStorage.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class DocumentConfigurationExceptionTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_set_message_correctly()
            {
                // Arrange
                string expectedMessage = "Test error message";

                // Act
                var sut = new DocumentConfigurationException(expectedMessage);

                // Assert
                Assert.Equal("[ELFAES-CFG-0006] " + expectedMessage, sut.Message);
            }
        }

        public class ThrowIfNull
        {
            [Fact]
            public void Should_not_throw_when_argument_is_not_null()
            {
                // Arrange
                object notNullObject = new object();

                // Act & Assert
                var exception = Record.Exception(() => DocumentConfigurationException.ThrowIfNull(notNullObject));

                Assert.Null(exception);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_argument_is_null()
            {
                // Arrange
                object? nullObject = null;
                string expectedParamName = "nullObject";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfNull(nullObject));

                Assert.Equal(expectedParamName, exception.ParamName);
            }

            [Fact]
            public void Should_use_caller_expression_for_parameter_name()
            {
                // Arrange
                TestObject? testObj = null;
                string expectedParamName = "testObj";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfNull(testObj));

                Assert.Equal(expectedParamName, exception.ParamName);
            }
        }

        public class ThrowIfIsNullOrWhiteSpace
        {
            [Fact]
            public void Should_not_throw_when_string_has_content()
            {
                // Arrange
                string validString = "Valid content";

                // Act & Assert
                var exception = Record.Exception(() =>
                    DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(validString));

                Assert.Null(exception);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_string_is_null()
            {
                // Arrange
                string? nullString = null;
                string expectedParamName = "nullString";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(nullString));

                Assert.Equal(expectedParamName, exception.ParamName);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_string_is_empty()
            {
                // Arrange
                string emptyString = string.Empty;
                string expectedParamName = "emptyString";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(emptyString));

                Assert.Equal(expectedParamName, exception.ParamName);
            }

            [Fact]
            public void Should_throw_ArgumentNullException_when_string_is_whitespace()
            {
                // Arrange
                string whitespaceString = "   ";
                string expectedParamName = "whitespaceString";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(whitespaceString));

                Assert.Equal(expectedParamName, exception.ParamName);
            }

            [Fact]
            public void Should_use_caller_expression_for_parameter_name()
            {
                // Arrange
                string? testString = null;
                string expectedParamName = "testString";

                // Act & Assert
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    DocumentConfigurationException.ThrowIfIsNullOrWhiteSpace(testString));

                Assert.Equal(expectedParamName, exception.ParamName);
            }
        }

        // Helper class for testing parameter names
        private class TestObject { }
    }
}
