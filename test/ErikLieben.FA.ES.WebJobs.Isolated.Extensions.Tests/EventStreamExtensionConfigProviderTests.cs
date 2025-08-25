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
            var context = Substitute.For<ExtensionConfigContext>(
                Substitute.For<IConfiguration>(),
                Substitute.For<INameResolver>(),
                Substitute.For<IConverterManager>(),
                Substitute.For<IWebHookProvider>(),
                Substitute.For<IExtensionRegistry>());

            // Act
            sut.Initialize(context);

            // Assert
            context.Received(1).AddBindingRule<EventStreamAttribute>();
        }
    }
}
