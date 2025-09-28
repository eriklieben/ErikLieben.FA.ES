using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ErikLieben.FA.ES.Exceptions;

namespace ErikLieben.FA.ES.AzureStorage.Exceptions;

/// <summary>
/// Exception thrown when document-related configuration is invalid or missing.
/// Error Code: ELFAES-CFG-0006
/// </summary>
/// <remarks>
/// This exception is thrown in the following scenarios:
/// - Required configuration values for Azure Storage documents are missing or invalid.
///
/// Common causes:
/// - Misconfigured connection strings, container names, or paths.
/// - Typographical errors in configuration keys.
///
/// Recommended actions:
/// - Validate configuration values during startup.
/// - Use the provided helper guards to validate inputs.
///
/// Documentation: https://github.com/eriklieben/ErikLieben.FA.ES/blob/main/docs/exceptions/elfaes-cfg-0006.md
/// </remarks>
public class DocumentConfigurationException : EsException
{
    private const string Code = "ELFAES-CFG-0006";

    public DocumentConfigurationException(string message) : base(Code, message)
    {
    }

    public DocumentConfigurationException(string message, Exception innerException) : base(Code, message, innerException)
    {
    }

    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            Throw(paramName);
        }
    }

    public static void ThrowIfIsNullOrWhiteSpace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Throw(paramName);
        }
    }

    [DoesNotReturn]
    internal static void Throw(string? paramName) =>
        throw new ArgumentNullException(paramName);
}
