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
