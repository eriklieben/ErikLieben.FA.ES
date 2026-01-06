using ErikLieben.FA.Results;
using ErikLieben.FA.Results.Validations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TaskFlow.Domain.Extensions;

/// <summary>
/// Error response for validation failures
/// </summary>
/// <param name="Errors">Collection of validation errors</param>
public record ValidationErrorResponse(IEnumerable<ValidationErrorDetail> Errors);

/// <summary>
/// Individual validation error detail
/// </summary>
/// <param name="Property">The property name that failed validation</param>
/// <param name="Message">The error message</param>
public record ValidationErrorDetail(string? Property, string Message);

/// <summary>
/// Extension methods to convert ErikLieben.FA.Results to strongly-typed Microsoft.AspNetCore.Http.IResult
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result to a strongly-typed ASP.NET Core IResult
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <returns>Ok (200) if successful, BadRequest (400) with validation errors if failed</returns>
    public static Results<Ok, BadRequest<ValidationErrorResponse>> ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok();

        return TypedResults.BadRequest(new ValidationErrorResponse(
            result.Errors.ToArray().Select(e => new ValidationErrorDetail(e.PropertyName, e.Message))
        ));
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to a strongly-typed ASP.NET Core IResult
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="result">The result to convert</param>
    /// <returns>Ok (200) with value if successful, BadRequest (400) with validation errors if failed</returns>
    public static Results<Ok<T>, BadRequest<ValidationErrorResponse>> ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return TypedResults.Ok(result.Value);

        return TypedResults.BadRequest(new ValidationErrorResponse(
            result.Errors.ToArray().Select(e => new ValidationErrorDetail(e.PropertyName, e.Message))
        ));
    }

    /// <summary>
    /// Converts a Result to a strongly-typed ASP.NET Core IResult with a custom success status code
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <param name="successStatusCode">The HTTP status code to return on success</param>
    /// <returns>The specified status code if successful, BadRequest (400) with validation errors if failed</returns>
    public static Results<StatusCodeHttpResult, BadRequest<ValidationErrorResponse>> ToHttpResult(
        this Result result,
        int successStatusCode)
    {
        if (result.IsSuccess)
            return TypedResults.StatusCode(successStatusCode);

        return TypedResults.BadRequest(new ValidationErrorResponse(
            result.Errors.ToArray().Select(e => new ValidationErrorDetail(e.PropertyName, e.Message))
        ));
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to a strongly-typed ASP.NET Core IResult with Created (201) response
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="uri">The URI of the created resource</param>
    /// <returns>Created (201) with value and location if successful, BadRequest (400) with validation errors if failed</returns>
    public static Results<Created<T>, BadRequest<ValidationErrorResponse>> ToCreatedResult<T>(
        this Result<T> result,
        string uri)
    {
        if (result.IsSuccess)
            return TypedResults.Created(uri, result.Value);

        return TypedResults.BadRequest(new ValidationErrorResponse(
            result.Errors.ToArray().Select(e => new ValidationErrorDetail(e.PropertyName, e.Message))
        ));
    }

    /// <summary>
    /// Converts a Result to a strongly-typed ASP.NET Core IResult with NoContent (204) response on success
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <returns>NoContent (204) if successful, BadRequest (400) with validation errors if failed</returns>
    public static Results<NoContent, BadRequest<ValidationErrorResponse>> ToNoContentResult(this Result result)
    {
        if (result.IsSuccess)
            return TypedResults.NoContent();

        return TypedResults.BadRequest(new ValidationErrorResponse(
            result.Errors.ToArray().Select(e => new ValidationErrorDetail(e.PropertyName, e.Message))
        ));
    }
}
