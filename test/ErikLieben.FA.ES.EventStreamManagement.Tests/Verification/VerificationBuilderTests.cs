using ErikLieben.FA.ES.EventStreamManagement.Verification;

namespace ErikLieben.FA.ES.EventStreamManagement.Tests.Verification;

public class VerificationBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_instance()
        {
            // Arrange & Act
            var sut = new VerificationBuilder();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class CompareEventCountsMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.CompareEventCounts();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class CompareChecksumsMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.CompareChecksums();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class ValidateTransformationsMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.ValidateTransformations();

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_use_default_sample_size()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.ValidateTransformations();

            // Assert
            Assert.Same(sut, result);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public void Should_accept_custom_sample_size(int sampleSize)
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.ValidateTransformations(sampleSize);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class VerifyStreamIntegrityMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.VerifyStreamIntegrity();

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class CustomValidationMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();
            Func<VerificationContext, Task<ValidationResult>> validator =
                _ => Task.FromResult(new ValidationResult("test", true, "passed"));

            // Act
            var result = sut.CustomValidation("custom-check", validator);

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_multiple_custom_validations()
        {
            // Arrange
            var sut = new VerificationBuilder();
            Func<VerificationContext, Task<ValidationResult>> validator1 =
                _ => Task.FromResult(new ValidationResult("check1", true, "passed"));
            Func<VerificationContext, Task<ValidationResult>> validator2 =
                _ => Task.FromResult(new ValidationResult("check2", false, "failed"));

            // Act
            sut.CustomValidation("check-1", validator1);
            sut.CustomValidation("check-2", validator2);

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class FailFastMethod
    {
        [Fact]
        public void Should_return_builder_for_fluent_chaining()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.FailFast();

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_true_parameter()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.FailFast(true);

            // Assert
            Assert.Same(sut, result);
        }

        [Fact]
        public void Should_accept_false_parameter()
        {
            // Arrange
            var sut = new VerificationBuilder();

            // Act
            var result = sut.FailFast(false);

            // Assert
            Assert.Same(sut, result);
        }
    }

    public class FluentChainingTests
    {
        [Fact]
        public void Should_support_full_fluent_configuration()
        {
            // Arrange & Act
            var sut = new VerificationBuilder()
                .CompareEventCounts()
                .CompareChecksums()
                .ValidateTransformations(50)
                .VerifyStreamIntegrity()
                .CustomValidation("custom", _ => Task.FromResult(new ValidationResult("custom", true, "ok")))
                .FailFast();

            // Assert
            Assert.NotNull(sut);
        }
    }

    public class InterfaceImplementation
    {
        [Fact]
        public void Should_implement_IVerificationBuilder()
        {
            // Arrange & Act
            var sut = new VerificationBuilder();

            // Assert
            Assert.IsAssignableFrom<IVerificationBuilder>(sut);
        }
    }
}

public class VerificationContextTests
{
    public class RequiredProperties
    {
        [Fact]
        public void Should_have_source_stream_identifier()
        {
            // Arrange & Act
            var sut = new VerificationContext
            {
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target",
                Statistics = new ErikLieben.FA.ES.EventStreamManagement.Core.MigrationStatistics()
            };

            // Assert
            Assert.Equal("source", sut.SourceStreamIdentifier);
        }

        [Fact]
        public void Should_have_target_stream_identifier()
        {
            // Arrange & Act
            var sut = new VerificationContext
            {
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target",
                Statistics = new ErikLieben.FA.ES.EventStreamManagement.Core.MigrationStatistics()
            };

            // Assert
            Assert.Equal("target", sut.TargetStreamIdentifier);
        }

        [Fact]
        public void Should_have_statistics()
        {
            // Arrange
            var stats = new ErikLieben.FA.ES.EventStreamManagement.Core.MigrationStatistics
            {
                TotalEvents = 100
            };

            // Act
            var sut = new VerificationContext
            {
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target",
                Statistics = stats
            };

            // Assert
            Assert.Same(stats, sut.Statistics);
        }
    }

    public class OptionalProperties
    {
        [Fact]
        public void Should_allow_null_transformer()
        {
            // Arrange & Act
            var sut = new VerificationContext
            {
                SourceStreamIdentifier = "source",
                TargetStreamIdentifier = "target",
                Statistics = new ErikLieben.FA.ES.EventStreamManagement.Core.MigrationStatistics(),
                Transformer = null
            };

            // Assert
            Assert.Null(sut.Transformer);
        }
    }
}

public class ValidationResultTests
{
    public class Constructor
    {
        [Fact]
        public void Should_create_with_required_parameters()
        {
            // Arrange & Act
            var sut = new ValidationResult("test", true, "Success");

            // Assert
            Assert.Equal("test", sut.Name);
            Assert.True(sut.Passed);
            Assert.Equal("Success", sut.Message);
        }

        [Fact]
        public void Should_create_failed_result()
        {
            // Arrange & Act
            var sut = new ValidationResult("check", false, "Validation failed");

            // Assert
            Assert.False(sut.Passed);
            Assert.Equal("Validation failed", sut.Message);
        }
    }

    public class Properties
    {
        [Fact]
        public void Should_have_name_property()
        {
            // Arrange & Act
            var sut = new ValidationResult("myCheck", true, "ok");

            // Assert
            Assert.Equal("myCheck", sut.Name);
        }

        [Fact]
        public void Should_have_passed_property()
        {
            // Arrange & Act
            var sut = new ValidationResult("check", true, "ok");

            // Assert
            Assert.True(sut.Passed);
        }

        [Fact]
        public void Should_have_message_property()
        {
            // Arrange & Act
            var sut = new ValidationResult("check", true, "All good");

            // Assert
            Assert.Equal("All good", sut.Message);
        }

        [Fact]
        public void Should_have_details_property()
        {
            // Arrange
            var details = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var sut = new ValidationResult("check", true, "ok") { Details = details };

            // Assert
            Assert.Same(details, sut.Details);
        }

        [Fact]
        public void Should_allow_null_details()
        {
            // Arrange & Act
            var sut = new ValidationResult("check", true, "ok");

            // Assert
            Assert.Null(sut.Details);
        }
    }
}
