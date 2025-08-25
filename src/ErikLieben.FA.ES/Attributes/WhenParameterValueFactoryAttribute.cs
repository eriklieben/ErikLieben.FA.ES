using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Attributes;


[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class WhenParameterValueFactoryAttribute<T>() 
    : Attribute where T : IProjectionWhenParameterValueFactory 
{
    
}