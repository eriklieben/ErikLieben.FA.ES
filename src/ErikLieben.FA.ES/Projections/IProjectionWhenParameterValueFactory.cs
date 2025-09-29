using ErikLieben.FA.ES.Documents;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Defines a factory that creates parameter values for Projection When-methods using a strongly typed event payload.
/// </summary>
/// <typeparam name="TValue">The type of the parameter value to create.</typeparam>
/// <typeparam name="TEventType">The type of the event payload used by the factory.</typeparam>
public interface IProjectionWhenParameterValueFactory<out TValue, in TEventType>
    : IProjectionWhenParameterValueFactory where TEventType : class
{
    /// <summary>
    /// Creates a parameter value based on the provided document and event with a typed payload.
    /// </summary>
    /// <param name="document">The current projection document.</param>
    /// <param name="event">The event being folded that carries a payload of type <typeparamref name="TEventType"/>.</param>
    /// <returns>The created parameter value.</returns>
    public TValue Create(IObjectDocument document, IEvent<TEventType> @event);
}

/// <summary>
/// Defines a factory that creates parameter values for Projection When-methods using an untyped event.
/// </summary>
/// <typeparam name="TValue">The type of the parameter value to create.</typeparam>
public interface IProjectionWhenParameterValueFactory<out TValue>
    : IProjectionWhenParameterValueFactory
{
    /// <summary>
    /// Creates a parameter value based on the provided document and event.
    /// </summary>
    /// <param name="document">The current projection document.</param>
    /// <param name="event">The event being folded.</param>
    /// <returns>The created parameter value.</returns>
    public TValue Create(IObjectDocument document, IEvent @event);
}

/// <summary>
/// Marker interface for factories that supply parameter values for Projection When-methods.
/// </summary>
public interface IProjectionWhenParameterValueFactory
{
}
