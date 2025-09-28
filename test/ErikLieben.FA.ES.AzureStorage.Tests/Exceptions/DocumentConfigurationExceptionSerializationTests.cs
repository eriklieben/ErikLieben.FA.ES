using System;
using System.Reflection;
using System.Runtime.Serialization;
using ErikLieben.FA.ES.AzureStorage.Exceptions;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Exceptions
{
    public class DocumentConfigurationExceptionSerializationTests
    {
        public class Serialization
        {
            [Fact]
            public void Should_roundtrip_via_serialization_constructor()
            {
                // Arrange
                var original = new DocumentConfigurationException("bad cfg");
                var info = new SerializationInfo(typeof(DocumentConfigurationException), new FormatterConverter());
                var context = new StreamingContext(StreamingContextStates.All);

                // Act
                original.GetObjectData(info, context);
                var sut = (DocumentConfigurationException)Activator.CreateInstance(
                    typeof(DocumentConfigurationException),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { info, context },
                    culture: null)!;

                // Assert
                Assert.Equal("[ELFAES-CFG-0006] bad cfg", sut.Message);
                Assert.Equal("ELFAES-CFG-0006", info.GetString(nameof(EsException.ErrorCode)));
            }

            [Fact]
            public void Should_have_serializable_attribute()
            {
                // Arrange & Act
                var attr = typeof(DocumentConfigurationException).GetCustomAttributes(typeof(SerializableAttribute), false);

                // Assert
                Assert.NotEmpty(attr);
            }
        }
    }
}
