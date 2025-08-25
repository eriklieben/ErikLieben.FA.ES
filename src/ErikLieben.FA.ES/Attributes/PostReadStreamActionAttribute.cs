using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Attributes;

public class PostReadStreamActionAttribute<T> : Attribute where T: IPostReadAction
{
}
