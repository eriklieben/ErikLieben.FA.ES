using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class FunctionsEventStoreExtensionsTests
{
    [Fact]
    public void ConfigureEventStoreBindings_registers_converters()
    {
        var services = new ServiceCollection();

        services.ConfigureEventStoreBindings();

        // Verify converters are registered (they are registered as IInputConverter with implementation types)
        Assert.Contains(services, sd => sd.ImplementationType?.Name == "EventStreamConverter");
        Assert.Contains(services, sd => sd.ImplementationType?.Name == "ProjectionConverter");
    }
}
