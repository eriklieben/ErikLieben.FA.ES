using ErikLieben.FA.ES.Actions;

namespace ErikLieben.FA.ES.Attributes;

public class PreAppendStreamActionAttribute<T> : Attribute where T: IPreAppendAction
{
}
