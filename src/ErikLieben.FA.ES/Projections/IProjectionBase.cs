using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.VersionTokenParts;

namespace ErikLieben.FA.ES.Projections;

public interface IProjectionBase
{
    Task Fold<T>(IEvent @event, IObjectDocument document, T? data = null, IExecutionContext? context = null)
        where T: class;

    Task Fold(IEvent @event, IObjectDocument document);

    Task UpdateToVersion<T>(VersionToken token, IExecutionContextWithData<T>? context = null, T? data = null)
        where T : class;

    Task UpdateToLatestVersion(IExecutionContext? context = null);

    Checkpoint Checkpoint { get; }

    string? CheckpointFingerprint { get; set; }
}
