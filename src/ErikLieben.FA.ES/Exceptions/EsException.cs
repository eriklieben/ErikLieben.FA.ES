using System;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Base exception for the ErikLieben.FA.ES library that carries a mandatory error code.
/// </summary>
public abstract class EsException : Exception
{
    /// <summary>
    /// Gets the unique error code associated with this exception instance.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EsException"/> class with a specified error code and message.
    /// </summary>
    protected EsException(string errorCode, string message)
        : base($"[{errorCode}] {message}")
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EsException"/> class with a specified error code, message, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    protected EsException(string errorCode, string message, Exception innerException)
        : base($"[{errorCode}] {message}", innerException)
    {
        ErrorCode = errorCode;
    }
}
