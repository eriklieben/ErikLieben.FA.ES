using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions
{
    public class EventStreamExtensionConfigProviderTests
    {
        public class Constructor
        {
            [Fact]
            public void Should_create_instance_without_exceptions()
            {
                // Act & Assert
                var exception = Record.Exception(() => new EventStreamExtensionConfigProvider());
                Assert.Null(exception);
            }
        }
    }

    public class Initialize
    {
        [Fact]
        public void Should_add_binding_rule_for_event_stream_attribute()
        {
            // Arrange
            var sut = new EventStreamExtensionConfigProvider();
#pragma warning disable 0618 // IWebHookProvider is obsolete in the test surface; suppression keeps tests stable without public API changes
            var context = Substitute.For<ExtensionConfigContext>(
                Substitute.For<IConfiguration>(),
                Substitute.For<INameResolver>(),
                Substitute.For<IConverterManager>(),
                Substitute.For<IWebHookProvider>(),
                Substitute.For<IExtensionRegistry>());
#pragma warning restore 0618

            // Act
            sut.Initialize(context);

            // Assert
            context.Received(1).AddBindingRule<EventStreamAttribute>();
        }
    }
}
