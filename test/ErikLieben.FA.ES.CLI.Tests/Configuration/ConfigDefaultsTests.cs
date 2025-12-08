#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

using ErikLieben.FA.ES.CLI.Configuration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Configuration;

public class ConfigDefaultsTests
{
    [Fact]
    public void Config_should_have_expected_defaults()
    {
        var config = new Config();

        Assert.NotNull(config.AdditionalJsonSerializables);
        Assert.Empty(config.AdditionalJsonSerializables);
        Assert.NotNull(config.Es);
        Assert.False(config.Es.EnableDiagnostics);
    }
}
