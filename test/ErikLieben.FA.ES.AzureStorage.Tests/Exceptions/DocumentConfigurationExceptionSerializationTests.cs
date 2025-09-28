using ErikLieben.FA.ES.AzureStorage.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class DocumentConfigurationExceptionSerializationTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_set_message()
            {
                // Arrange & Act
                var sut = new DocumentConfigurationException("bad cfg");

                // Assert
                Assert.Equal("[ELFAES-CFG-0006] bad cfg", sut.Message);
            }
        }
    }
}
