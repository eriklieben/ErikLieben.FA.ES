using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ErikLieben.FA.ES.AzureStorage.Configuration;

/// <summary>
/// Source-generated validator for <see cref="EventStreamBlobSettings"/>.
/// </summary>
/// <remarks>
/// Use this validator when binding settings from IConfiguration:
/// <code>
/// services.AddOptions&lt;EventStreamBlobSettings&gt;()
///     .BindConfiguration("BlobStorage")
///     .ValidateDataAnnotations()
///     .ValidateOnStart();
/// </code>
/// </remarks>
[OptionsValidator]
public partial class EventStreamBlobSettingsValidator : IValidateOptions<EventStreamBlobSettings>
{
}

/// <summary>
/// Extension methods for registering blob settings validation.
/// </summary>
public static class EventStreamBlobSettingsValidatorExtensions
{
    /// <summary>
    /// Adds validation for <see cref="EventStreamBlobSettings"/> using the source-generated validator.
    /// </summary>
    /// <param name="builder">The options builder.</param>
    /// <returns>The options builder for chaining.</returns>
    public static OptionsBuilder<EventStreamBlobSettings> ValidateWithValidator(
        this OptionsBuilder<EventStreamBlobSettings> builder)
    {
        builder.Services.AddSingleton<IValidateOptions<EventStreamBlobSettings>, EventStreamBlobSettingsValidator>();
        return builder;
    }
}
