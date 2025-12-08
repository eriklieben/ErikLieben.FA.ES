namespace ErikLieben.FA.ES.AspNetCore.MinimalApis;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging methods for AspNetCore.MinimalApis.
/// Using LoggerMessage source generators for zero-allocation logging.
/// </summary>
public static partial class LogMessages
{
    // ===== Projection Output Filter =====

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Updating projection {ProjectionType} with blob name {BlobName}")]
    public static partial void UpdatingProjection(this ILogger logger, string projectionType, string blobName);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Successfully updated projection {ProjectionType}")]
    public static partial void ProjectionUpdated(this ILogger logger, string projectionType);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Saved projection {ProjectionType} using generic factory")]
    public static partial void ProjectionSavedGeneric(this ILogger logger, string projectionType);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Debug,
        Message = "Saved projection {ProjectionType} using non-generic factory")]
    public static partial void ProjectionSavedNonGeneric(this ILogger logger, string projectionType);
}
