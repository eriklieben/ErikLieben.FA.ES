using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Specifies a pre-append stream action of type <typeparamref name="T"/> to execute before events are appended.
/// </summary>
/// <typeparam name="T">The action type implementing <see cref="IPreAppendAction"/>.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class PreAppendStreamActionAttribute<T> : Attribute where T: IPreAppendAction
{
}
