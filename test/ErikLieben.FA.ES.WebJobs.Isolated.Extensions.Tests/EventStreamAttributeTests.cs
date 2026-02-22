using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Description;
using Xunit;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions
{
    public class EventStreamAttributeTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_initialize_object_id_property()
            {
                // Arrange
                string expectedObjectId = "testId";

                // Act
                var sut = new EventStreamAttribute(expectedObjectId);

                // Assert
                Assert.Equal(expectedObjectId, sut.ObjectId);
            }
        }

        public class ObjectIdProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("initialId");
                string expectedValue = "updatedId";

                // Act
                sut.ObjectId = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ObjectId);
            }

            [Fact]
            public void Should_have_auto_resolve_attribute()
            {
                // Arrange & Act
                var property = typeof(EventStreamAttribute).GetProperty(nameof(EventStreamAttribute.ObjectId));

                // Assert
                Assert.NotNull(property);
                Assert.True(property.IsDefined(typeof(AutoResolveAttribute), false));
            }
        }

        public class ObjectTypeProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                string expectedValue = "testType";

                // Act
                sut.ObjectType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ObjectType);
            }

            [Fact]
            public void Should_have_auto_resolve_attribute()
            {
                // Arrange & Act
                var property = typeof(EventStreamAttribute).GetProperty(nameof(EventStreamAttribute.ObjectType));

                // Assert
                Assert.NotNull(property);
                Assert.True(property.IsDefined(typeof(AutoResolveAttribute), false));
            }

            [Fact]
            public void Should_have_regex_validation_attribute()
            {
                // Arrange & Act
                var property = typeof(EventStreamAttribute).GetProperty(nameof(EventStreamAttribute.ObjectType));

                // Assert
                Assert.NotNull(property);
                var regexAttr = property.GetCustomAttributes(typeof(RegularExpressionAttribute), false)[0] as RegularExpressionAttribute;
                Assert.NotNull(regexAttr);
                Assert.Equal("^[A-Za-z][A-Za-z0-9]{2,62}$", regexAttr.Pattern);
            }

            [Theory]
            [InlineData("Valid123")] // Valid - starts with letter, then alphanumeric, length > 2
            [InlineData("Abc")] // Valid - min length 3
            // [InlineData("A" + new string('b', 62))] // Valid - max length 63
            public void Should_validate_correct_object_type_formats(string validValue)
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");

                // Act
                sut.ObjectType = validValue;

                // Assert
                Assert.Equal(validValue, sut.ObjectType);
            }

            // [Theory]
            // [InlineData("ab")] // Too short
            // [InlineData("123abc")] // Doesn't start with letter
            // // [InlineData("A" + new string('b', 63))] // Too long (64 chars)
            // [InlineData("A$bc")] // Contains special characters
            // public void Should_fail_validation_for_incorrect_object_type_formats(string invalidValue)
            // {
            //     // Arrange
            //     var sut = new EventStreamAttribute("testId");
            //     var validationContext = new ValidationContext(sut);
            //     var property = typeof(EventStreamAttribute).GetProperty(nameof(EventStreamAttribute.ObjectType));
            //     var regexAttr = property.GetCustomAttributes(typeof(RegularExpressionAttribute), false)[0] as RegularExpressionAttribute;
            //
            //     // Act
            //     sut.ObjectType = invalidValue;
            //     var validationResult = regexAttr.GetValidationResult(invalidValue, validationContext);
            //
            //     // Assert
            //     Assert.NotEqual(ValidationResult.Success, validationResult);
            // }
        }

        public class ConnectionProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                string expectedValue = "testConnection";

                // Act
                sut.Connection = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.Connection);
            }

            [Fact]
            public void Should_default_to_null()
            {
                // Arrange & Act
                var sut = new EventStreamAttribute("testId");

                // Assert
                Assert.Null(sut.Connection);
            }
        }

        public class DocumentTypeProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                string expectedValue = "testDocumentType";

                // Act
                sut.DocumentType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DocumentType);
            }

            [Fact]
            public void Should_default_to_null()
            {
                // Arrange & Act
                var sut = new EventStreamAttribute("testId");

                // Assert
                Assert.Null(sut.DocumentType);
            }
        }

        public class DefaultStreamTypeProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                string expectedValue = "testStreamType";

                // Act
                sut.DefaultStreamType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DefaultStreamType);
            }

            [Fact]
            public void Should_default_to_null()
            {
                // Arrange & Act
                var sut = new EventStreamAttribute("testId");

                // Assert
                Assert.Null(sut.DefaultStreamType);
            }
        }

        public class DefaultStreamConnectionProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                string expectedValue = "testStreamConnection";

                // Act
                sut.DefaultStreamConnection = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DefaultStreamConnection);
            }

            [Fact]
            public void Should_default_to_null()
            {
                // Arrange & Act
                var sut = new EventStreamAttribute("testId");

                // Assert
                Assert.Null(sut.DefaultStreamConnection);
            }
        }

        public class CreateEmptyObjectWhenNonExistentProperty
        {
            [Fact]
            public void Should_set_and_get_value()
            {
                // Arrange
                var sut = new EventStreamAttribute("testId");
                bool expectedValue = true;

                // Act
                sut.CreateEmptyObjectWhenNonExistent = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.CreateEmptyObjectWhenNonExistent);
            }

            [Fact]
            public void Should_default_to_false()
            {
                // Arrange & Act
                var sut = new EventStreamAttribute("testId");

                // Assert
                Assert.False(sut.CreateEmptyObjectWhenNonExistent);
            }
        }

        public class AttributeUsage
        {
            [Fact]
            public void Should_have_correct_usage_targets()
            {
                // Arrange & Act
                var attributeUsage = typeof(EventStreamAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0] as AttributeUsageAttribute;

                // Assert
                Assert.NotNull(attributeUsage);
                Assert.Equal(AttributeTargets.Parameter | AttributeTargets.ReturnValue, attributeUsage.ValidOn);
            }
        }

        public class ClassAttributes
        {
            [Fact]
            public void Should_have_binding_attribute()
            {
                // Arrange & Act
                var bindingAttr = typeof(EventStreamAttribute).GetCustomAttributes(typeof(BindingAttribute), false);

                // Assert
                Assert.Single(bindingAttr);
            }

            [Fact]
            public void Should_have_connection_provider_attribute_with_storage_account_type()
            {
                // Arrange & Act
                var connProviderAttr = typeof(EventStreamAttribute).GetCustomAttributes(typeof(ConnectionProviderAttribute), false)[0] as ConnectionProviderAttribute;

                // Assert
                Assert.NotNull(connProviderAttr);
                Assert.Equal(typeof(StorageAccountAttribute), connProviderAttr.ProviderType);
            }
        }

        public class InheritanceHierarchy
        {
            [Fact]
            public void Should_inherit_from_attribute_and_implement_connection_provider()
            {
                // Arrange & Act
                var type = typeof(EventStreamAttribute);

                // Assert
                Assert.True(type.IsSubclassOf(typeof(Attribute)));
                Assert.True(typeof(IConnectionProvider).IsAssignableFrom(type));
            }
        }
    }
}
