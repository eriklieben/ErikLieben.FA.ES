using ErikLieben.FA.ES.Results;

namespace ErikLieben.FA.ES.Tests.Results;

public class ResultOfTTests
{
    public class SuccessMethod
    {
        [Fact]
        public void Should_create_successful_result_with_value()
        {
            // Arrange & Act
            var sut = Result<string>.Success("hello");

            // Assert
            Assert.True(sut.IsSuccess);
            Assert.False(sut.IsFailure);
            Assert.Equal("hello", sut.Value);
            Assert.Null(sut.Error);
        }

        [Fact]
        public void Should_create_successful_result_with_integer_value()
        {
            // Arrange & Act
            var sut = Result<int>.Success(42);

            // Assert
            Assert.True(sut.IsSuccess);
            Assert.Equal(42, sut.Value);
        }
    }

    public class FailureMethod
    {
        [Fact]
        public void Should_create_failed_result_with_error()
        {
            // Arrange
            var error = new Error("FAIL", "Failed");

            // Act
            var sut = Result<string>.Failure(error);

            // Assert
            Assert.False(sut.IsSuccess);
            Assert.True(sut.IsFailure);
            Assert.Equal(error, sut.Error);
        }

        [Fact]
        public void Should_create_failed_result_with_code_and_message()
        {
            // Arrange & Act
            var sut = Result<int>.Failure("CODE", "Message");

            // Assert
            Assert.False(sut.IsSuccess);
            Assert.True(sut.IsFailure);
            Assert.NotNull(sut.Error);
            Assert.Equal("CODE", sut.Error!.Code);
            Assert.Equal("Message", sut.Error.Message);
        }
    }

    public class ValueProperty
    {
        [Fact]
        public void Should_return_value_on_success()
        {
            // Arrange
            var sut = Result<string>.Success("test-value");

            // Act
            var value = sut.Value;

            // Assert
            Assert.Equal("test-value", value);
        }

        [Fact]
        public void Should_throw_on_failure()
        {
            // Arrange
            var sut = Result<string>.Failure(new Error("FAIL", "Failed"));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sut.Value);
        }
    }

    public class GetValueOrDefaultMethod
    {
        [Fact]
        public void Should_return_value_on_success()
        {
            // Arrange
            var sut = Result<string>.Success("actual");

            // Act
            var value = sut.GetValueOrDefault("fallback");

            // Assert
            Assert.Equal("actual", value);
        }

        [Fact]
        public void Should_return_default_on_failure()
        {
            // Arrange
            var sut = Result<string>.Failure(new Error("FAIL", "Failed"));

            // Act
            var value = sut.GetValueOrDefault("fallback");

            // Assert
            Assert.Equal("fallback", value);
        }

        [Fact]
        public void Should_return_type_default_when_no_default_specified()
        {
            // Arrange
            var sut = Result<string>.Failure(new Error("FAIL", "Failed"));

            // Act
            var value = sut.GetValueOrDefault();

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public void Should_return_zero_for_int_failure_with_no_default()
        {
            // Arrange
            var sut = Result<int>.Failure(new Error("FAIL", "Failed"));

            // Act
            var value = sut.GetValueOrDefault();

            // Assert
            Assert.Equal(0, value);
        }
    }

    public class ImplicitConversions
    {
        [Fact]
        public void Should_convert_value_to_successful_result()
        {
            // Arrange & Act
            Result<string> sut = "implicit-value";

            // Assert
            Assert.True(sut.IsSuccess);
            Assert.Equal("implicit-value", sut.Value);
        }

        [Fact]
        public void Should_convert_error_to_failed_result()
        {
            // Arrange
            var error = new Error("IMPLICIT", "Implicit error");

            // Act
            Result<string> sut = error;

            // Assert
            Assert.True(sut.IsFailure);
            Assert.Equal(error, sut.Error);
        }

        [Fact]
        public void Should_convert_integer_to_successful_result()
        {
            // Arrange & Act
            Result<int> sut = 99;

            // Assert
            Assert.True(sut.IsSuccess);
            Assert.Equal(99, sut.Value);
        }
    }

    public class MapMethod
    {
        [Fact]
        public void Should_map_value_on_success()
        {
            // Arrange
            var sut = Result<int>.Success(5);

            // Act
            var mapped = sut.Map(v => v * 2);

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal(10, mapped.Value);
        }

        [Fact]
        public void Should_map_to_different_type()
        {
            // Arrange
            var sut = Result<int>.Success(42);

            // Act
            var mapped = sut.Map(v => v.ToString());

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal("42", mapped.Value);
        }

        [Fact]
        public void Should_propagate_error_on_failure()
        {
            // Arrange
            var error = new Error("FAIL", "Failed");
            var sut = Result<int>.Failure(error);

            // Act
            var mapped = sut.Map(v => v * 2);

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal(error, mapped.Error);
        }
    }

    public class BindMethod
    {
        [Fact]
        public void Should_bind_value_on_success()
        {
            // Arrange
            var sut = Result<int>.Success(5);

            // Act
            var bound = sut.Bind(v => Result<string>.Success($"value-{v}"));

            // Assert
            Assert.True(bound.IsSuccess);
            Assert.Equal("value-5", bound.Value);
        }

        [Fact]
        public void Should_propagate_inner_failure()
        {
            // Arrange
            var sut = Result<int>.Success(5);
            var innerError = new Error("INNER", "Inner failure");

            // Act
            var bound = sut.Bind(v => Result<string>.Failure(innerError));

            // Assert
            Assert.True(bound.IsFailure);
            Assert.Equal(innerError, bound.Error);
        }

        [Fact]
        public void Should_propagate_outer_error_on_failure()
        {
            // Arrange
            var error = new Error("OUTER", "Outer failure");
            var sut = Result<int>.Failure(error);

            // Act
            var bound = sut.Bind(v => Result<string>.Success("should not reach"));

            // Assert
            Assert.True(bound.IsFailure);
            Assert.Equal(error, bound.Error);
        }
    }

    public class OnSuccessMethod
    {
        [Fact]
        public void Should_execute_action_on_success()
        {
            // Arrange
            var sut = Result<string>.Success("hello");
            string? captured = null;

            // Act
            var returned = sut.OnSuccess(v => captured = v);

            // Assert
            Assert.Equal("hello", captured);
            Assert.True(returned.IsSuccess);
        }

        [Fact]
        public void Should_not_execute_action_on_failure()
        {
            // Arrange
            var sut = Result<string>.Failure(new Error("FAIL", "Failed"));
            var executed = false;

            // Act
            var returned = sut.OnFailure(_ => executed = true);
            sut.OnSuccess(_ => executed = false);

            // Assert - OnFailure should have set it to true, OnSuccess should not overwrite
            Assert.True(executed);
        }

        [Fact]
        public void Should_return_same_result()
        {
            // Arrange
            var sut = Result<int>.Success(42);

            // Act
            var returned = sut.OnSuccess(_ => { });

            // Assert
            Assert.True(returned.IsSuccess);
            Assert.Equal(42, returned.Value);
        }
    }

    public class OnFailureMethod
    {
        [Fact]
        public void Should_execute_action_on_failure()
        {
            // Arrange
            var error = new Error("FAIL", "Failed");
            var sut = Result<string>.Failure(error);
            Error? captured = null;

            // Act
            var returned = sut.OnFailure(e => captured = e);

            // Assert
            Assert.Equal(error, captured);
            Assert.True(returned.IsFailure);
        }

        [Fact]
        public void Should_not_execute_action_on_success()
        {
            // Arrange
            var sut = Result<string>.Success("hello");
            var executed = false;

            // Act
            sut.OnFailure(_ => executed = true);

            // Assert
            Assert.False(executed);
        }

        [Fact]
        public void Should_return_same_result()
        {
            // Arrange
            var error = new Error("FAIL", "Failed");
            var sut = Result<int>.Failure(error);

            // Act
            var returned = sut.OnFailure(_ => { });

            // Assert
            Assert.True(returned.IsFailure);
            Assert.Equal(error, returned.Error);
        }
    }

    public class Chaining
    {
        [Fact]
        public void Should_chain_map_and_on_success()
        {
            // Arrange
            var sut = Result<int>.Success(10);
            string? captured = null;

            // Act
            var result = sut
                .Map(v => v.ToString())
                .OnSuccess(v => captured = v);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("10", captured);
        }

        [Fact]
        public void Should_chain_map_and_on_failure_when_failed()
        {
            // Arrange
            var error = new Error("ERR", "Error");
            var sut = Result<int>.Failure(error);
            Error? captured = null;

            // Act
            var result = sut
                .Map(v => v.ToString())
                .OnFailure(e => captured = e);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(error, captured);
        }
    }
}
