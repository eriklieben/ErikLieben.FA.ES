namespace ErikLieben.FA.ES.Actions;

/// <summary>
/// Composite interface that combines all stream action interfaces for comprehensive event stream processing.
/// </summary>
public interface IAllStreamActions : IPostAppendAction, IPostReadAction, IPreAppendAction, IPreReadAction, IAsyncPostCommitAction
{

}
