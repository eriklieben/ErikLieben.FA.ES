namespace ErikLieben.FA.ES.Results;

/// <summary>
/// Represents the result of an operation that may succeed or fail.
/// </summary>
public readonly struct Result
{
    private readonly bool _isSuccess;
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the error if the operation failed; otherwise null.
    /// </summary>
    public Error? Error => _error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a failed result with an error code and message.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    public static Result Failure(string code, string message) => new(false, new Error(code, message));

    /// <summary>
    /// Implicitly converts an Error to a failed Result.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the result of an operation that may succeed with a value or fail.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T>
{
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessing value on a failed result.</exception>
    public T Value => _isSuccess && _value is not null
        ? _value
        : throw new InvalidOperationException("Cannot access Value on a failed result. Check IsSuccess first.");

    /// <summary>
    /// Gets the error if the operation failed; otherwise null.
    /// </summary>
    public Error? Error => _error;

    /// <summary>
    /// Gets the value or the specified default if the result is a failure.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the result is a failure.</param>
    public T? GetValueOrDefault(T? defaultValue = default) => _isSuccess ? _value : defaultValue;

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The success value.</param>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result<T> Failure(Error error) => new(false, default, error);

    /// <summary>
    /// Creates a failed result with an error code and message.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    public static Result<T> Failure(string code, string message) => new(false, default, new Error(code, message));

    /// <summary>
    /// Implicitly converts a value to a successful Result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an Error to a failed Result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Maps the success value to a new value using the specified function.
    /// </summary>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="mapper">The mapping function.</param>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return _isSuccess && _value is not null
            ? Result<TNew>.Success(mapper(_value))
            : Result<TNew>.Failure(_error ?? Error.Unknown);
    }

    /// <summary>
    /// Maps the success value to a new Result using the specified function.
    /// </summary>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="binder">The binding function.</param>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return _isSuccess && _value is not null
            ? binder(_value)
            : Result<TNew>.Failure(_error ?? Error.Unknown);
    }

    /// <summary>
    /// Executes an action on the value if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess && _value is not null)
        {
            action(_value);
        }
        return this;
    }

    /// <summary>
    /// Executes an action on the error if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (!_isSuccess && _error is not null)
        {
            action(_error);
        }
        return this;
    }
}
