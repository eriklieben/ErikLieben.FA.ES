using System;
using System.Runtime.Serialization;

namespace ErikLieben.FA.ES.Exceptions;

/// <summary>
/// Base exception for the ErikLieben.FA.ES library that carries a mandatory error code.
/// </summary>
[Serializable]
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

    /// <summary>
    /// Initializes a new instance of the <see cref="EsException"/> class with serialized data.
    /// </summary>
    protected EsException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ErrorCode = info.GetString(nameof(ErrorCode))!;
    }

    /// <inheritdoc />
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ErrorCode), ErrorCode);
    }
}
