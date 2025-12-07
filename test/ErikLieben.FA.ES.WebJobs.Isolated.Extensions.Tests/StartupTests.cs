using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using NSubstitute;
using Xunit;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions
{
    public class StartupTests
    {
        public class ConfigureMethod
        {
            [Fact]
            public void Should_add_event_stream_extension_provider_to_builder()
            {
                // Arrange
                var mockBuilder = Substitute.For<IWebJobsBuilder>();
                var sut = new Startup();

                // Act
                sut.Configure(mockBuilder);

                // Assert
                mockBuilder.ReceivedWithAnyArgs().AddExtension<EventStreamExtensionConfigProvider>();
            }

            [Fact]
            public void Should_throw_argument_null_exception_when_builder_is_null()
            {
                // Arrange
                IWebJobsBuilder? nullBuilder = null;
                var sut = new Startup();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => sut.Configure(nullBuilder!));
            }
        }

        public class WebJobsStartupAttributeTests
        {
            [Fact]
            public void Should_have_webjobs_startup_attribute_with_correct_type()
            {
                // Arrange & Act
                var assembly = typeof(Startup).Assembly;
                var attributes = assembly.GetCustomAttributes(typeof(WebJobsStartupAttribute), false);

                // Assert
                Assert.NotEmpty(attributes);
                var attribute = attributes[0] as WebJobsStartupAttribute;
                Assert.NotNull(attribute);
                Assert.Equal(typeof(Startup), attribute!.WebJobsStartupType);
            }
        }

        public class InterfaceImplementationTests
        {
            [Fact]
            public void Should_implement_iwebjobs_startup_interface()
            {
                // Arrange & Act
                var type = typeof(Startup);

                // Assert
                Assert.True(typeof(IWebJobsStartup).IsAssignableFrom(type));
            }
        }
    }
}
