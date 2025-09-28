using System;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.Exceptions;

/// <summary>
/// Exception thrown when an invalid binding source is provided during Azure Functions Worker model binding.
/// Error Code: ELFAES-VAL-0002
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - The provided <c>ModelBindingData.Source</c> is not supported by the binding.
///
/// Common causes:
/// - Binding configuration expects a different source.
/// - Incorrect attribute usage or trigger configuration.
///
/// Recommended actions:
/// - Ensure the binding source matches the expected value(s).
/// - Update configuration or attributes to supply a supported source.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-val-0002.md
/// </remarks>
internal class InvalidBindingSourceException : EsException
{
    private const string Code = "ELFAES-VAL-0002";

    /// <summary>
    /// Initializes a new instance of the exception with a formatted message including the actual and expected binding sources.
    /// </summary>
    /// <param name="actualSource">The source that is being provided by ModelBindingData.</param>
    /// <param name="expectedSource">The source(s) that is supported.</param>
    public InvalidBindingSourceException(string actualSource, string expectedSource)
        : base(Code, $"Unexpected binding source '{actualSource}'. Only '{expectedSource}' is supported.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the exception with a formatted message and an inner exception.
    /// </summary>
    /// <param name="actualSource">The source that is being provided by ModelBindingData.</param>
    /// <param name="expectedSource">The source(s) that is supported.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public InvalidBindingSourceException(string actualSource, string expectedSource, Exception innerException)
        : base(Code, $"Unexpected binding source '{actualSource}'. Only '{expectedSource}' is supported.", innerException)
    {
    }
}
