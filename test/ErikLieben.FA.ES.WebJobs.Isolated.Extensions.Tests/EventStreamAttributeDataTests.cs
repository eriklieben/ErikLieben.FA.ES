using Xunit;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions
{
    public class EventStreamAttributeDataTests
    {
        public class PropertyTests
        {
            [Fact]
            public void Should_set_and_get_object_id()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testObjectId";

                // Act
                sut.ObjectId = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ObjectId);
            }

            [Fact]
            public void Should_set_and_get_object_type()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testObjectType";

                // Act
                sut.ObjectType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.ObjectType);
            }

            [Fact]
            public void Should_set_and_get_connection()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testConnection";

                // Act
                sut.Connection = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.Connection);
            }

            [Fact]
            public void Should_set_and_get_document_type()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testDocumentType";

                // Act
                sut.DocumentType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DocumentType);
            }

            [Fact]
            public void Should_set_and_get_default_stream_type()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testDefaultStreamType";

                // Act
                sut.DefaultStreamType = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DefaultStreamType);
            }

            [Fact]
            public void Should_set_and_get_default_stream_connection()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = "testDefaultStreamConnection";

                // Act
                sut.DefaultStreamConnection = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.DefaultStreamConnection);
            }

            [Fact]
            public void Should_have_false_as_default_value_for_create_empty_object_when_non_existent()
            {
                // Arrange & Act
                var sut = new EventStreamAttributeData();

                // Assert
                Assert.False(sut.CreateEmptyObjectWhenNonExistent);
            }

            [Fact]
            public void Should_set_and_get_create_empty_object_when_non_existent()
            {
                // Arrange
                var sut = new EventStreamAttributeData();
                var expectedValue = true;

                // Act
                sut.CreateEmptyObjectWhenNonExistent = expectedValue;

                // Assert
                Assert.Equal(expectedValue, sut.CreateEmptyObjectWhenNonExistent);
            }

            [Fact]
            public void Should_initialize_with_all_nullable_string_properties_as_null()
            {
                // Arrange & Act
                var sut = new EventStreamAttributeData();

                // Assert
                Assert.Null(sut.ObjectId);
                Assert.Null(sut.ObjectType);
                Assert.Null(sut.Connection);
                Assert.Null(sut.DocumentType);
                Assert.Null(sut.DefaultStreamType);
                Assert.Null(sut.DefaultStreamConnection);
            }
        }
    }
}
