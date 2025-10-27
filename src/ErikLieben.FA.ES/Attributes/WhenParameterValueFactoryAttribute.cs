using ErikLieben.FA.ES.Projections;

namespace ErikLieben.FA.ES.Attributes;

/// <summary>
/// Marks a projection When-method to use a specific parameter value factory for resolving method parameters during event processing.
/// </summary>
/// <typeparam name="T">The type of the parameter value factory that implements <see cref="IProjectionWhenParameterValueFactory"/>.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class WhenParameterValueFactoryAttribute<T>()
    : Attribute where T : IProjectionWhenParameterValueFactory
{

}