using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.AzureStorage.Tests.Blob;

public class TestEntity : IBase
{
    public Task Fold()
    {
        return Task.CompletedTask;
    }

    public void Fold(IEvent @event)
    {
    }

    public void ProcessSnapshot(object snapshot)
    {
    }
}