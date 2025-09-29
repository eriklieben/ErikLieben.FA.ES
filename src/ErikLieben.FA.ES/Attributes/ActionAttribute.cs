using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Indicates that a stream-level action of type <typeparamref name="T"/> should be applied to the decorated class.
/// </summary>
/// <typeparam name="T">The action type implementing <see cref="IAction"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class StreamActionAttribute<T> : Attribute where T : IAction
{
}
