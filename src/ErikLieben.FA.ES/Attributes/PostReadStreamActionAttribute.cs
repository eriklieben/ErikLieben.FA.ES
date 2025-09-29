using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies a post-read stream action of type <typeparamref name="T"/> to execute after events are read.
/// </summary>
/// <typeparam name="T">The action type implementing <see cref="IPostReadAction"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class PostReadStreamActionAttribute<T> : Attribute where T: IPostReadAction
{
}
