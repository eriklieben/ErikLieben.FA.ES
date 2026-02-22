using Microsoft.AspNetCore.Mvc;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.EventStreamManagement.Backup;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Api.Endpoints;

/// <summary>
/// Admin endpoints for backup and restore operations.
/// These endpoints demonstrate the standalone backup/restore service capabilities.
/// </summary>
public static class BackupRestoreEndpoints
{
    public static RouteGroupBuilder MapBackupRestoreEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/backup")
            .WithTags("Backup & Restore")
            .WithDescription("Endpoints for backing up and restoring event streams");

        // Single stream backup
        group.MapPost("/workitems/{id}", BackupWorkItem)
            .WithName("BackupWorkItem")
            .WithSummary("Create a backup of a work item's event stream")
            .WithDescription("Creates a point-in-time backup of all events in the work item's stream. " +
                           "Backups can be used for disaster recovery, debugging, or stream migration.");

        group.MapPost("/projects/{id}", BackupProject)
            .WithName("BackupProject")
            .WithSummary("Create a backup of a project's event stream")
            .WithDescription("Creates a complete backup of the project aggregate including all events.");

        // Bulk backup
        group.MapPost("/workitems/bulk", BulkBackupWorkItems)
            .WithName("BulkBackupWorkItems")
            .WithSummary("Backup multiple work items")
            .WithDescription("Creates backups for multiple work item streams in parallel.");

        // List and query backups
        group.MapGet("/list", ListBackups)
            .WithName("ListBackups")
            .WithSummary("List available backups")
            .WithDescription("Returns a list of all available backups with metadata.");

        group.MapGet("/list/{objectName}", ListBackupsByObjectName)
            .WithName("ListBackupsByObjectName")
            .WithSummary("List backups for a specific object type")
            .WithDescription("Returns backups filtered by object name (e.g., 'workItem', 'project').");

        // Get specific backup
        group.MapGet("/{backupId:guid}", GetBackup)
            .WithName("GetBackup")
            .WithSummary("Get backup details")
            .WithDescription("Returns detailed information about a specific backup.");

        // Validate backup
        group.MapGet("/{backupId:guid}/validate", ValidateBackup)
            .WithName("ValidateBackup")
            .WithSummary("Validate a backup")
            .WithDescription("Verifies that a backup is intact and can be restored.");

        // Restore operations
        group.MapPost("/{backupId:guid}/restore", RestoreBackup)
            .WithName("RestoreBackup")
            .WithSummary("Restore from a backup")
            .WithDescription("Restores an event stream from a backup. Use with caution as this may overwrite existing data.");

        group.MapPost("/{backupId:guid}/restore-to/{newId}", RestoreToNewStream)
            .WithName("RestoreToNewStream")
            .WithSummary("Restore to a new stream ID")
            .WithDescription("Restores a backup to a new stream with a different ID, useful for cloning aggregates.");

        // Delete backup
        group.MapDelete("/{backupId:guid}", DeleteBackup)
            .WithName("DeleteBackup")
            .WithSummary("Delete a backup")
            .WithDescription("Permanently deletes a backup from storage.");

        // Cleanup
        group.MapPost("/cleanup", CleanupExpiredBackups)
            .WithName("CleanupExpiredBackups")
            .WithSummary("Clean up expired backups")
            .WithDescription("Removes backups that have exceeded their retention period.");

        return group;
    }

    private static async Task<IResult> BackupWorkItem(
        string id,
        [FromServices] IBackupRestoreService? backupService,
        [FromServices] IWorkItemFactory workItemFactory,
        [FromQuery] bool includeSnapshots = false,
        [FromQuery] bool compress = false)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                detail: "The IBackupRestoreService is not registered. Ensure backup providers are configured.",
                statusCode: 501);
        }

        try
        {
            // Verify work item exists
            var workItem = await workItemFactory.GetAsync(WorkItemId.From(id));
            if (workItem.Metadata?.Id == null)
            {
                return Results.NotFound(new { error = "Work item not found", id });
            }

            var options = new BackupOptions
            {
                IncludeSnapshots = includeSnapshots,
                EnableCompression = compress,
                Retention = TimeSpan.FromDays(30)
            };

            var handle = await backupService.BackupStreamAsync("workItem", id, options);

            return Results.Ok(new
            {
                success = true,
                message = "Backup created successfully",
                backup = new
                {
                    backupId = handle.BackupId,
                    objectName = handle.ObjectName,
                    objectId = handle.ObjectId,
                    eventCount = handle.EventCount,
                    createdAt = handle.CreatedAt,
                    sizeBytes = handle.SizeBytes,
                    checksum = handle.Metadata.Checksum
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Backup failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> BackupProject(
        string id,
        [FromServices] IBackupRestoreService? backupService,
        [FromServices] IProjectFactory projectFactory,
        [FromQuery] bool includeSnapshots = false,
        [FromQuery] bool compress = false)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                detail: "The IBackupRestoreService is not registered. Ensure backup providers are configured.",
                statusCode: 501);
        }

        try
        {
            // Verify project exists
            var project = await projectFactory.GetAsync(ProjectId.From(id));
            if (project.Metadata?.Id == null)
            {
                return Results.NotFound(new { error = "Project not found", id });
            }

            var options = new BackupOptions
            {
                IncludeSnapshots = includeSnapshots,
                EnableCompression = compress,
                Retention = TimeSpan.FromDays(30)
            };

            var handle = await backupService.BackupStreamAsync("project", id, options);

            return Results.Ok(new
            {
                success = true,
                message = "Backup created successfully",
                backup = new
                {
                    backupId = handle.BackupId,
                    objectName = handle.ObjectName,
                    objectId = handle.ObjectId,
                    projectName = project.Name,
                    eventCount = handle.EventCount,
                    createdAt = handle.CreatedAt,
                    sizeBytes = handle.SizeBytes,
                    checksum = handle.Metadata.Checksum
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Backup failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> BulkBackupWorkItems(
        [FromBody] BulkBackupRequest request,
        [FromServices] IBackupRestoreService? backupService)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                detail: "The IBackupRestoreService is not registered.",
                statusCode: 501);
        }

        if (request.ObjectIds == null || request.ObjectIds.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one object ID is required" });
        }

        try
        {
            var options = new BulkBackupOptions
            {
                IncludeSnapshots = request.IncludeSnapshots,
                EnableCompression = request.Compress,
                MaxConcurrency = Math.Min(request.MaxConcurrency ?? 4, 10),
                ContinueOnError = true
            };

            var result = await backupService.BackupManyAsync(request.ObjectIds, "workItem", options);

            return Results.Ok(new
            {
                success = result.IsFullySuccessful,
                summary = new
                {
                    totalProcessed = result.TotalProcessed,
                    successCount = result.SuccessCount,
                    failureCount = result.FailureCount,
                    elapsedMs = result.ElapsedTime.TotalMilliseconds
                },
                successfulBackups = result.SuccessfulBackups.Select(h => new
                {
                    backupId = h.BackupId,
                    objectId = h.ObjectId,
                    eventCount = h.EventCount
                }),
                failedBackups = result.FailedBackups.Select(f => new
                {
                    objectId = f.ObjectId,
                    error = f.ErrorMessage
                })
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Bulk backup failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> ListBackups(
        [FromServices] IBackupRestoreService? backupService,
        [FromQuery] int? limit = 50)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var query = new BackupQuery { MaxResults = limit ?? 50 };
            var backups = await backupService.ListBackupsAsync(query);

            return Results.Ok(new
            {
                backups = backups.Select(h => new
                {
                    backupId = h.BackupId,
                    objectName = h.ObjectName,
                    objectId = h.ObjectId,
                    eventCount = h.EventCount,
                    createdAt = h.CreatedAt,
                    sizeBytes = h.SizeBytes,
                    location = h.Location
                }),
                count = backups.Count()
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("registry"))
        {
            return Results.Problem(
                title: "Backup registry not configured",
                detail: "Listing backups requires a backup registry to be configured.",
                statusCode: 501);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to list backups",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> ListBackupsByObjectName(
        string objectName,
        [FromServices] IBackupRestoreService? backupService,
        [FromQuery] int? limit = 50)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var query = new BackupQuery
            {
                ObjectName = objectName,
                MaxResults = limit ?? 50
            };
            var backups = await backupService.ListBackupsAsync(query);

            return Results.Ok(new
            {
                objectName,
                backups = backups.Select(h => new
                {
                    backupId = h.BackupId,
                    objectId = h.ObjectId,
                    eventCount = h.EventCount,
                    createdAt = h.CreatedAt,
                    sizeBytes = h.SizeBytes
                }),
                count = backups.Count()
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("registry"))
        {
            return Results.Problem(
                title: "Backup registry not configured",
                statusCode: 501);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to list backups",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetBackup(
        Guid backupId,
        [FromServices] IBackupRestoreService? backupService)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var handle = await backupService.GetBackupAsync(backupId);

            if (handle == null)
            {
                return Results.NotFound(new { error = "Backup not found", backupId });
            }

            return Results.Ok(new
            {
                backupId = handle.BackupId,
                objectName = handle.ObjectName,
                objectId = handle.ObjectId,
                eventCount = handle.EventCount,
                streamVersion = handle.StreamVersion,
                createdAt = handle.CreatedAt,
                sizeBytes = handle.SizeBytes,
                checksum = handle.Metadata.Checksum,
                isCompressed = handle.Metadata.IsCompressed,
                includesSnapshots = handle.Metadata.IncludesSnapshots,
                location = handle.Location,
                providerName = handle.ProviderName
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get backup",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> ValidateBackup(
        Guid backupId,
        [FromServices] IBackupRestoreService? backupService)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var handle = await backupService.GetBackupAsync(backupId);

            if (handle == null)
            {
                return Results.NotFound(new { error = "Backup not found", backupId });
            }

            var isValid = await backupService.ValidateBackupAsync(handle);

            return Results.Ok(new
            {
                backupId,
                isValid,
                validatedAt = DateTimeOffset.UtcNow,
                message = isValid
                    ? "Backup is valid and can be restored"
                    : "Backup validation failed - backup may be corrupt or incomplete"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Validation failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> RestoreBackup(
        Guid backupId,
        [FromServices] IBackupRestoreService? backupService,
        [FromBody] RestoreRequest? request)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var handle = await backupService.GetBackupAsync(backupId);

            if (handle == null)
            {
                return Results.NotFound(new { error = "Backup not found", backupId });
            }

            var options = new RestoreOptions
            {
                Overwrite = request?.Overwrite ?? false,
                ValidateBeforeRestore = request?.ValidateFirst ?? true
            };

            await backupService.RestoreStreamAsync(handle, options);

            return Results.Ok(new
            {
                success = true,
                message = "Restore completed successfully",
                backupId,
                objectName = handle.ObjectName,
                objectId = handle.ObjectId,
                restoredAt = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Results.Conflict(new
            {
                error = "Stream already exists",
                detail = ex.Message,
                hint = "Set 'overwrite' to true to restore over existing data"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Restore failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> RestoreToNewStream(
        Guid backupId,
        string newId,
        [FromServices] IBackupRestoreService? backupService,
        [FromBody] RestoreRequest? request)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var handle = await backupService.GetBackupAsync(backupId);

            if (handle == null)
            {
                return Results.NotFound(new { error = "Backup not found", backupId });
            }

            var options = new RestoreOptions
            {
                Overwrite = request?.Overwrite ?? false,
                ValidateBeforeRestore = request?.ValidateFirst ?? true
            };

            await backupService.RestoreToNewStreamAsync(handle, newId, options);

            return Results.Ok(new
            {
                success = true,
                message = $"Restored to new stream '{newId}' successfully",
                backupId,
                originalObjectId = handle.ObjectId,
                newObjectId = newId,
                objectName = handle.ObjectName,
                restoredAt = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Results.Conflict(new
            {
                error = "Target stream already exists",
                detail = ex.Message,
                hint = "Set 'overwrite' to true or use a different ID"
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Restore to new stream failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteBackup(
        Guid backupId,
        [FromServices] IBackupRestoreService? backupService)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var handle = await backupService.GetBackupAsync(backupId);

            if (handle == null)
            {
                return Results.NotFound(new { error = "Backup not found", backupId });
            }

            await backupService.DeleteBackupAsync(handle);

            return Results.Ok(new
            {
                success = true,
                message = "Backup deleted successfully",
                backupId,
                deletedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to delete backup",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> CleanupExpiredBackups(
        [FromServices] IBackupRestoreService? backupService)
    {
        if (backupService == null)
        {
            return Results.Problem(
                title: "Backup service not configured",
                statusCode: 501);
        }

        try
        {
            var deletedCount = await backupService.CleanupExpiredBackupsAsync();

            return Results.Ok(new
            {
                success = true,
                message = deletedCount > 0
                    ? $"Cleaned up {deletedCount} expired backup(s)"
                    : "No expired backups found",
                deletedCount,
                cleanedAt = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("registry"))
        {
            return Results.Problem(
                title: "Backup registry not configured",
                detail: "Cleanup requires a backup registry to track expiration.",
                statusCode: 501);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Cleanup failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    // Request DTOs
    public record BulkBackupRequest(
        List<string> ObjectIds,
        bool IncludeSnapshots = false,
        bool Compress = false,
        int? MaxConcurrency = 4);

    public record RestoreRequest(
        bool Overwrite = false,
        bool ValidateFirst = true);
}
