using ErikLieben.FA.ES.S3.Configuration;
using ErikLieben.FA.ES.S3.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ErikLieben.FA.ES.S3.Tests.HealthChecks;

public class S3HealthCheckBuilderExtensionsTests
{
    [Fact]
    public void Should_register_health_check_with_default_name()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IS3ClientFactory>());
        services.AddSingleton(new EventStreamS3Settings("s3"));

        services.AddHealthChecks().AddS3StorageHealthCheck();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>();

        Assert.Contains(options.Value.Registrations, r => r.Name == "s3-storage");
    }

    [Fact]
    public void Should_register_health_check_with_custom_name()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IS3ClientFactory>());
        services.AddSingleton(new EventStreamS3Settings("s3"));

        services.AddHealthChecks().AddS3StorageHealthCheck(name: "custom-s3");

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>();

        Assert.Contains(options.Value.Registrations, r => r.Name == "custom-s3");
    }
}
