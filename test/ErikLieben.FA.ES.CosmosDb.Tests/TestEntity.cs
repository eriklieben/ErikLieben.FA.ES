using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.CosmosDb.Tests;

public class TestEntity : IBase
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }

    public Task Fold()
    {
        return Task.CompletedTask;
    }

    public void Fold(IEvent @event)
    {
    }

    public void ProcessSnapshot(object snapshot)
    {
        if (snapshot is TestEntity entity)
        {
            Name = entity.Name;
            Value = entity.Value;
        }
    }
}
