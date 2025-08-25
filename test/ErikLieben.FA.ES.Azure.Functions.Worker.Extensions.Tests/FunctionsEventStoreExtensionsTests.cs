using System;
using System.Collections.Generic;
using ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;
using ErikLieben.FA.ES.Configuration;
using ErikLieben.FA.ES.Documents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Tests;

public class FunctionsEventStoreExtensionsTests
{
    [Fact]
    public void ConfigureEventStore_registers_expected_services_and_keyed_dictionary()
    {
        var services = new ServiceCollection();
        var settings = new EventStreamDefaultTypeSettings();

        FunctionsEventStoreExtensions.ConfigureEventStore(services, settings);

        var provider = services.BuildServiceProvider();

        // singleton settings
        var resolvedSettings = provider.GetService<EventStreamDefaultTypeSettings>();
        Assert.Same(settings, resolvedSettings);

        // Factories
        Assert.NotNull(provider.GetService<IObjectDocumentFactory>());
        Assert.NotNull(provider.GetService<IDocumentTagDocumentFactory>());
        Assert.NotNull(provider.GetService<IEventStreamFactory>());

        // Keyed dictionaries should resolve even if empty
        var dict1 = provider.GetService<IDictionary<string, IObjectDocumentFactory>>();
        var dict2 = provider.GetService<IDictionary<string, IDocumentTagDocumentFactory>>();
        var dict3 = provider.GetService<IDictionary<string, IEventStreamFactory>>();

        Assert.NotNull(dict1);
        Assert.NotNull(dict2);
        Assert.NotNull(dict3);
        Assert.Empty(dict1!);
        Assert.Empty(dict2!);
        Assert.Empty(dict3!);
    }
}
