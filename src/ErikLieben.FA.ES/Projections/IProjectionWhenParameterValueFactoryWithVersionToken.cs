using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Factory that creates parameter values from version token for When-method parameters.
/// </summary>
/// <typeparam name="T">The type of parameter value to create.</typeparam>
public interface IProjectionWhenParameterValueFactoryWithVersionToken<out T>
{
    /// <summary>
    /// Creates a parameter value using a version token and event.
    /// </summary>
    /// <param name="versionToken">The version token containing object context.</param>
    /// <param name="event">The event being processed.</param>
    /// <returns>The created parameter value.</returns>
    T Create(VersionToken versionToken, IEvent @event);
}

/// <summary>
/// Factory that creates parameter values from version token with typed event for When-method parameters.
/// </summary>
/// <typeparam name="T">The type of parameter value to create.</typeparam>
/// <typeparam name="Te">The event payload type.</typeparam>
public interface IProjectionWhenParameterValueFactoryWithVersionToken<out T, in Te> : IProjectionWhenParameterValueFactoryWithVersionToken<T>
    where Te : class
{
    /// <summary>
    /// Creates a parameter value using a version token and typed event.
    /// </summary>
    /// <param name="versionToken">The version token containing object context.</param>
    /// <param name="event">The typed event being processed.</param>
    /// <returns>The created parameter value.</returns>
    T Create(VersionToken versionToken, IEvent<Te> @event);
}
