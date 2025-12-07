namespace ErikLieben.FA.ES.AspNetCore.MinimalApis.Exceptions;

/// <summary>
/// Exception thrown when parameter binding fails for event stream or projection parameters.
/// </summary>
[Serializable]
public sealed class BindingException : Exception
{
    /// <summary>
    /// Gets the name of the parameter that failed to bind.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Gets the type of the parameter that failed to bind.
    /// </summary>
    public Type? ParameterType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingException"/> class.
    /// </summary>
    public BindingException()
        : base("Parameter binding failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BindingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BindingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingException"/> class
    /// with parameter details.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed to bind.</param>
    /// <param name="parameterType">The type of the parameter that failed to bind.</param>
    /// <param name="reason">The reason for the binding failure.</param>
    public BindingException(string parameterName, Type parameterType, string reason)
        : base($"Failed to bind parameter '{parameterName}' of type '{parameterType.Name}': {reason}")
    {
        ParameterName = parameterName;
        ParameterType = parameterType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingException"/> class
    /// with parameter details and an inner exception.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that failed to bind.</param>
    /// <param name="parameterType">The type of the parameter that failed to bind.</param>
    /// <param name="reason">The reason for the binding failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BindingException(string parameterName, Type parameterType, string reason, Exception innerException)
        : base($"Failed to bind parameter '{parameterName}' of type '{parameterType.Name}': {reason}", innerException)
    {
        ParameterName = parameterName;
        ParameterType = parameterType;
    }
}
