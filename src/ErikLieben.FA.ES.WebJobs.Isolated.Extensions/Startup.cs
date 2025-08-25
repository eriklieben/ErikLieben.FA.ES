using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.WebJobs;

[assembly: WebJobsStartup(typeof(ErikLieben.FA.ES.WebJobs.Isolated.Extensions.Startup))]

namespace ErikLieben.FA.ES.WebJobs.Isolated.Extensions;

public class Startup : IWebJobsStartup
{
    public void Configure(IWebJobsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddExtension<EventStreamExtensionConfigProvider>();
    }
}
