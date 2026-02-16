# Security Review: ErikLieben.FA.ES Library and TaskFlow Demo

**Date:** 2026-02-16
**Scope:** `src/` (library) and `demo/src/TaskFlow.Api/` (demo application)
**Reviewer:** AI Security Review (Claude Opus 4.6)

---

## Executive Summary

This review examines the ErikLieben.FA.ES event sourcing library and the TaskFlow demo application for security vulnerabilities. The library itself follows generally sound security practices (ETag-based concurrency, typed serialization via source-generated JSON contexts, and proper null-checks). However, several issues were identified -- primarily in the demo application's admin endpoints, input validation gaps in storage providers, and credential handling concerns.

**Finding Counts:**
- CRITICAL: 2
- MAJOR: 7
- MINOR: 5

---

## CRITICAL Findings

### SEC-01: Admin endpoints expose connection strings and storage credentials without authentication

**Severity:** CRITICAL
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:85-91` (GetStorageConnection)
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:89-91` (GetStorageDebugInfo)
- `demo/src/TaskFlow.Api/Program.cs:542` (MapAdminEndpoints with no auth)

**Description:**
The admin endpoint group at `/api/admin` has no authentication or authorization middleware. The `GetStorageConnection` endpoint (line 1838) returns fully-formed Azure Storage connection strings including the well-known Azurite account key. The `GetStorageDebugInfo` endpoint (line 1912) dumps all configured connection strings including `Store`, `userdataStore`, `events`, `project`, `workitem`, `projections`, `userProfile`, etc.

While the demo uses Azurite (development emulator), the pattern is dangerous because:
1. No `RequireAuthorization()` is applied to the admin group (verified via grep - zero matches for auth-related middleware on Program.cs).
2. The `GetStorageConnection` endpoint hardcodes the well-known Azurite account key (`Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==`) at line 1898.
3. The `ClearAllStorage` endpoint (line 2821) **deletes all blob containers** with zero confirmation, zero auth.
4. If someone deploys this demo against a real Azure Storage account without changing the connection string construction, credentials leak.

**Exploit scenario:**
An attacker accesses `GET /api/admin/storage/connection` or `GET /api/admin/storage/debug` to obtain connection strings. With these credentials, they gain full read/write/delete access to the storage account. Alternatively, calling `DELETE /api/admin/storage/clear` wipes all event data.

**Suggested fix:**
```csharp
var group = routes.MapGroup("/api/admin")
    .WithTags("Admin & Diagnostics")
    .RequireAuthorization("AdminPolicy"); // Add authorization

// In Program.cs - even for demo, gate behind a development-only check:
if (app.Environment.IsDevelopment())
{
    app.MapAdminEndpoints();
}
```
At minimum, restrict the storage connection and debug endpoints to development mode, and add a confirmation mechanism to the `ClearAllStorage` endpoint.

---

### SEC-02: S3 credentials hardcoded in source code

**Severity:** CRITICAL
**Files:**
- `demo/src/TaskFlow.Api/Program.cs:263-271`

**Description:**
MinIO/S3 credentials are hardcoded directly in the application source code:
```csharp
var s3Settings = new EventStreamS3Settings("s3")
{
    ServiceUrl = "http://localhost:9000",
    AccessKey = "minioadmin",    // Line 266
    SecretKey = "minioadmin",    // Line 267
    ...
};
```

While these are default MinIO development credentials, this pattern is problematic because:
1. Credentials are committed to version control
2. The `EventStreamS3Settings` class stores `AccessKey` and `SecretKey` as plain string properties (file `src/ErikLieben.FA.ES.S3/Configuration/EventStreamS3Settings.cs:48-53`)
3. This pattern encourages production deployments to follow the same approach

**Exploit scenario:**
If this demo is deployed to any non-local environment or if real credentials are substituted and committed, they become exposed in the git history.

**Suggested fix:**
Load S3 credentials from configuration/environment variables:
```csharp
var s3Settings = new EventStreamS3Settings(
    builder.Configuration["S3:DataStore"] ?? "s3",
    serviceUrl: builder.Configuration["S3:ServiceUrl"],
    accessKey: builder.Configuration["S3:AccessKey"],
    secretKey: builder.Configuration["S3:SecretKey"],
    ...
);
```
Consider marking `AccessKey` and `SecretKey` in `EventStreamS3Settings` with `[JsonIgnore]` or redacting them in `ToString()` to prevent accidental logging.

---

## MAJOR Findings

### SEC-03: No input validation/sanitization on object IDs used in storage paths

**Severity:** MAJOR
**Files:**
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDocumentStore.cs:67` (`CreateAsync`)
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDocumentStore.cs:164` (`GetAsync`)
- `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobDataStore.cs:257` (`SetAsync`)
- `src/ErikLieben.FA.ES.S3/S3DocumentStore.cs:61` (`CreateAsync`)
- `src/ErikLieben.FA.ES.S3/S3DocumentStore.cs:156` (`GetAsync`)
- `src/ErikLieben.FA.ES.AzureStorage/Migration/BlobMigrationRoutingTable.cs:215` (`GetBlobName`)

**Description:**
Object IDs and object names are used directly in storage paths without sanitization:
```csharp
// BlobDocumentStore.cs:67
var documentPath = $"{name}/{objectId}.json";

// S3DocumentStore.cs:61
var documentPath = $"{name}/{objectId}.json";

// BlobMigrationRoutingTable.cs:215
return $"{objectId}.routing.json";
```

There is no validation that `name` or `objectId` do not contain path traversal characters (e.g., `../`, `/`, `\`). While Azure Blob Storage and S3 have their own path resolution rules that typically prevent escaping the container/bucket, the behavior is provider-specific and may not be safe in all cases.

In the `BlobDataStore.CreateBlobClient` (line 381-402), the blob path is constructed from `objectDocument.ObjectName` and `documentPath` without validation. In Table Storage, the `TableDocumentTagStore` applies `SanitizeForTableKey` (line 169-175) but this is not applied consistently across all providers.

**Exploit scenario:**
A malicious object ID like `../../other-container/secret` could potentially be used to read or write blobs outside the intended path scope (depending on the storage provider's path normalization). In S3, object keys with `../` are treated literally, but could cause confusion in path-based access policies.

**Suggested fix:**
Add a centralized input validation method and apply it at the boundary:
```csharp
public static class ObjectIdValidator
{
    private static readonly Regex ValidObjectId = new(@"^[a-zA-Z0-9\-_]+$");

    public static void Validate(string objectId)
    {
        if (!ValidObjectId.IsMatch(objectId))
            throw new ArgumentException($"Invalid object ID: '{objectId}'");
    }
}
```
Apply this validation in `IObjectDocumentFactory.GetAsync/CreateAsync`, `EventStreamBinder.BindAsync`, and all storage provider document stores.

---

### SEC-04: OData filter injection in Azure Table Storage queries

**Severity:** MAJOR
**Files:**
- `src/ErikLieben.FA.ES.AzureStorage/Table/TableDocumentStore.cs:432`
- `src/ErikLieben.FA.ES.AzureStorage/Table/TableDataStore.cs:82-86`
- `src/ErikLieben.FA.ES.AzureStorage/Table/TableDocumentTagStore.cs:93`
- `src/ErikLieben.FA.ES.AzureStorage/Table/TableObjectIdProvider.cs:62`

**Description:**
OData filter expressions are constructed via string interpolation with user-controlled values:
```csharp
// TableDocumentStore.cs:432
filter: $"PartitionKey eq '{objectId}'"

// TableDataStore.cs:82
filter = $"PartitionKey eq '{partitionKey}' and RowKey ge '{startRowKey}' and RowKey le '{endRowKey}'"

// TableDocumentTagStore.cs:93
var filter = $"PartitionKey eq '{partitionKey}'"
```

While `objectId` and `partitionKey` often come from internal code paths, if any user-controlled input reaches these queries (e.g., via the `{id}` route parameter in admin endpoints flowing to document lookups), it could inject additional OData filter clauses.

An `objectId` containing `' or PartitionKey eq '` could modify the query semantics.

**Exploit scenario:**
An attacker crafting a request with `id=test' or PartitionKey ne 'x` could modify the OData filter to return all entities in the table, potentially leaking data across object boundaries.

**Suggested fix:**
Use the `TableClient.CreateQueryFilter` method or escape single quotes in values:
```csharp
// Use the Azure SDK's built-in filter builder:
var filter = TableClient.CreateQueryFilter($"PartitionKey eq {objectId}");
```
The `CreateQueryFilter` method properly escapes values via `FormattableString`.

---

### SEC-05: Exception message leakage in GlobalExceptionHandler

**Severity:** MAJOR
**Files:**
- `demo/src/TaskFlow.Api/Middleware/GlobalExceptionHandler.cs:33`

**Description:**
The `GlobalExceptionHandler` always includes `exception.Message` in the response `Detail` field:
```csharp
var problemDetails = new ProblemDetails
{
    Status = (int)HttpStatusCode.InternalServerError,
    Title = "An error occurred while processing your request",
    Detail = exception.Message,  // <-- Always exposed
    Instance = httpContext.Request.Path
};
```

While the stack trace and exception type are gated behind `IsDevelopment()` (line 39), the `exception.Message` is returned in all environments. Exception messages from storage providers often contain:
- Container/bucket names
- Connection string fragments
- Internal file paths
- Object IDs and document names

For example, `BlobDocumentNotFoundException` (line 186-189 of BlobDocumentStore.cs) includes the store name and object details.

**Exploit scenario:**
An attacker sends malformed requests to trigger storage errors. The error messages reveal internal storage structure (container names, blob paths, store names), which assists in planning further attacks.

**Suggested fix:**
```csharp
var problemDetails = new ProblemDetails
{
    Status = (int)HttpStatusCode.InternalServerError,
    Title = "An error occurred while processing your request",
    Detail = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
        ? exception.Message
        : "An internal error occurred. Please try again later.",
    Instance = httpContext.Request.Path
};
```

---

### SEC-06: Benchmark file endpoint allows filename manipulation

**Severity:** MAJOR
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:3852-3888`

**Description:**
The `GetBenchmarkFile` endpoint validates against `..`, `/`, and `\` in the filename, then also checks that the resolved path starts with the expected directory:
```csharp
if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
{
    return Results.BadRequest("Invalid filename");
}
// ...
if (!Path.GetFullPath(filePath).StartsWith(benchmarkResultsPath))
{
    return Results.BadRequest("Invalid filename");
}
```

The path traversal check has two issues:
1. `Path.GetFullPath` on Windows resolves various path manipulation tricks (e.g., null bytes, alternate data streams like `file.json::$DATA`, or `file.json...`).
2. `StartsWith` on Windows is case-sensitive but the filesystem is case-insensitive, so `benchmarkResultsPath` in different casing could bypass the check.
3. The error at line 3884 exposes `ex.Message` which could reveal the file system structure.

**Exploit scenario:**
On Windows, a filename like `file.json::$DATA` bypasses the `..` check but may cause unexpected behavior. While the `StartsWith` check provides defense-in-depth, using `OrdinalIgnoreCase` comparison would be more robust on Windows.

**Suggested fix:**
```csharp
if (!Path.GetFullPath(filePath).StartsWith(benchmarkResultsPath, StringComparison.OrdinalIgnoreCase))
{
    return Results.BadRequest("Invalid filename");
}
```
Additionally, restrict filenames to a strict allowlist pattern:
```csharp
if (!Regex.IsMatch(filename, @"^[\w\-\.]+\.json$"))
{
    return Results.BadRequest("Invalid filename");
}
```

---

### SEC-07: No authentication or authorization on any API endpoints

**Severity:** MAJOR
**Files:**
- `demo/src/TaskFlow.Api/Program.cs:538-549` (all endpoint mappings)
- `demo/src/TaskFlow.Api/Middleware/CurrentUserMiddleware.cs:23-34`

**Description:**
The entire TaskFlow API has zero authentication. The `CurrentUserMiddleware` extracts user identity from the `X-Current-User` header with no validation:
```csharp
if (context.Request.Headers.TryGetValue(CURRENT_USER_HEADER, out var headerValue))
{
    userId = headerValue.FirstOrDefault();
}
if (string.IsNullOrWhiteSpace(userId))
{
    userId = UserProfileEndpoints.ADMIN_USER_ID; // Default to admin!
}
currentUserService.SetCurrentUserId(userId);
```

This means:
1. Any unauthenticated request defaults to the admin user
2. User identity can be spoofed by setting an `X-Current-User` header
3. There is no `RequireAuthorization()` on any endpoint group

While this is a demo application, the pattern establishes a dangerous precedent. The code comments acknowledge this ("In production, this would be replaced with proper authentication middleware") but there is no mechanism to enforce this.

**Exploit scenario:**
Any client can impersonate any user by sending `X-Current-User: victim-user-id`. They can then modify that user's data, view their work items, and perform admin operations.

**Suggested fix:**
Add clear warnings and environment checks:
```csharp
if (!app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Demo authentication middleware is only allowed in Development. " +
        "Configure proper authentication for non-development environments.");
}
```

---

### SEC-08: CosmosDB query with user-controlled containerName parameter

**Severity:** MAJOR
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:1987-2038`

**Description:**
The `GetCosmosDbDocuments` endpoint accepts `containerName` as a query parameter and uses it directly to access a CosmosDB container:
```csharp
private static async Task<IResult> GetCosmosDbDocuments(
    [FromServices] IServiceProvider serviceProvider,
    [FromQuery] string? objectName = null,
    [FromQuery] string? containerName = null)
{
    var targetContainer = containerName ?? "documents";
    var container = db.GetContainer(targetContainer);  // User-controlled!
    ...
}
```

This allows an attacker to enumerate and read data from any container in the `eventstore` database, not just the intended `documents` container. The error handling at lines 2033-2037 also exposes the full exception message and inner exception.

**Exploit scenario:**
An attacker calls `GET /api/admin/cosmosdb/documents?containerName=tags` to access tag data, or `containerName=events` to dump all raw events. Combined with `objectName` parameter, they can craft precise queries to extract sensitive data.

**Suggested fix:**
Validate `containerName` against an allowlist:
```csharp
var allowedContainers = new[] { "documents", "events", "tags", "projections" };
if (containerName != null && !allowedContainers.Contains(containerName))
{
    return Results.BadRequest($"Invalid container name. Allowed: {string.Join(", ", allowedContainers)}");
}
```
And add authentication to the endpoint.

---

## MINOR Findings

### SEC-09: Connection string fragments logged at startup

**Severity:** MINOR
**Files:**
- `demo/src/TaskFlow.Api/Program.cs:52` (logging "Found")
- `demo/src/TaskFlow.Api/Program.cs:62` (logging first 50 chars of connection string)
- `demo/src/TaskFlow.Api/Program.cs:79` (logging full UserDataStore connection string)
- `demo/src/TaskFlow.Api/Program.cs:119` (logging first 100 chars of table connection string)

**Description:**
At startup, connection strings are partially logged:
```csharp
logger.LogInformation($"Store connection string: {storeConnectionString?.Substring(0, Math.Min(50, storeConnectionString?.Length ?? 0))}...");
logger.LogInformation($"UserDataStore connection string: {userDataConnectionString}"); // Full string!
```

Line 79 logs the **full** `userDataConnectionString` without truncation.

**Exploit scenario:**
If logs are accessible (e.g., Application Insights, stdout in containers), credentials could be extracted.

**Suggested fix:**
Log only whether connection strings are present, not their content:
```csharp
logger.LogInformation("UserDataStore connection string: {Status}",
    string.IsNullOrEmpty(userDataConnectionString) ? "NOT FOUND" : "Configured");
```

---

### SEC-10: Debug Console.WriteLine statements in production code

**Severity:** MINOR
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:364-369` (GetProjectAtVersion)
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:377-383` (GetProjectAtVersion)

**Description:**
Debug output using `Console.WriteLine` dumps project state details to stdout:
```csharp
Console.WriteLine($"[DEBUG] GetProjectAtVersion - ProjectId: {projectId}, Version: {version}");
Console.WriteLine($"[DEBUG] IsCompleted: {project.IsCompleted}, Outcome: {project.Outcome}");
```

These statements expose internal state information and pollute logs in production.

**Suggested fix:**
Replace with conditional logging:
```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.LogDebug("GetProjectAtVersion - ProjectId: {ProjectId}, Version: {Version}", projectId, version);
}
```
Or remove the debug statements entirely.

---

### SEC-11: Optimistic concurrency bypass in S3 DataStore

**Severity:** MINOR
**Files:**
- `src/ErikLieben.FA.ES.S3/S3DataStore.cs:230-266`
- `src/ErikLieben.FA.ES.S3/S3DocumentStore.cs:274-292`

**Description:**
The S3 data store's append operation has a TOCTOU (time-of-check-time-of-use) race condition. The `ObjectExistsAsync` check (line 230) and the subsequent `PutObjectAsEntityAsync` (line 244) are not atomic:
```csharp
var exists = await s3Client.ObjectExistsAsync(bucketName, key);
if (!exists)
{
    // ... create new
    await s3Client.PutObjectAsEntityAsync(bucketName, key, newDoc, ...);
}
```

If two concurrent requests both see `exists == false`, both will attempt to create the document, and the second write silently overwrites the first, losing events.

Similarly, the ETag-based concurrency for existing objects relies on S3's conditional writes via `If-Match`, but the S3 protocol's `PutObject` does not natively support `If-Match` preconditions (unlike Azure Blob Storage). The `PutObjectAsEntityAsync` method may not actually enforce the ETag check depending on the S3 provider.

**Exploit scenario:**
Under high concurrency, two clients could simultaneously append events to a new stream, and one client's events would be silently lost.

**Suggested fix:**
For new document creation, use S3's `If-None-Match: *` header (when supported) or implement application-level locking. Document that S3 providers without conditional write support may have weaker concurrency guarantees.

---

### SEC-12: CORS configured for HTTP localhost origins

**Severity:** MINOR
**Files:**
- `demo/src/TaskFlow.Api/Program.cs:460-471`

**Description:**
The CORS policy allows specific origins:
```csharp
policy.WithOrigins(
    "https://localhost:4200",
    "https://taskflow-frontend.dev.localhost")
```

This is well-configured for HTTPS-only origins. However, combined with `AllowCredentials()` and the complete lack of authentication, any origin that can reach the API can simply call it directly without CORS (CORS only restricts browser-based JavaScript). The SignalR hub at `/hub/taskflow` is also unprotected.

**Suggested fix:**
No immediate action needed for the demo, but note that CORS is not a security boundary -- it should be complemented by proper authentication.

---

### SEC-13: Guid.Parse without validation in admin endpoints

**Severity:** MINOR
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:290`
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:355`
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:432`

**Description:**
Route parameters are parsed as GUIDs using `Guid.Parse(id)` which throws `FormatException` on invalid input:
```csharp
var workItemId = Guid.Parse(id);  // Line 290
var projectId = Guid.Parse(id);   // Line 355
```

These exceptions bubble up to the `GlobalExceptionHandler`, which returns the exception message (including the malformed input) to the client.

**Suggested fix:**
Use `Guid.TryParse` and return a 400 Bad Request:
```csharp
if (!Guid.TryParse(id, out var workItemId))
{
    return Results.BadRequest("Invalid work item ID format");
}
```

---

### SEC-14: Exception details in error responses across admin endpoints

**Severity:** MINOR
**Files:**
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:2035-2037` (CosmosDB endpoint)
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:2817` (projection metadata)
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:2878-2881` (user profiles status)
- `demo/src/TaskFlow.Api/Endpoints/AdminEndpoints.cs:3882-3886` (benchmark file)

**Description:**
Multiple admin endpoints return `Results.Problem(detail: ex.Message)` which includes inner exception messages:
```csharp
return Results.Problem(
    title: "CosmosDB Query Failed",
    detail: ex.Message + (ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : ""),
    statusCode: 500
);
```

These error messages can reveal internal infrastructure details (database names, container names, connection issues, specific error codes).

**Suggested fix:**
In non-development environments, return generic error messages. Log the full exception details server-side only.

---

## Positive Observations

The following security practices were noted as positive:

1. **ETag-based optimistic concurrency** is implemented consistently across Blob Storage (`BlobDataStore.cs:327-342`), Table Storage (`TableDocumentStore.cs:267-268`), and CosmosDB (`CosmosDbDocumentStore.cs:234-248`).

2. **Source-generated JSON serialization** (`System.Text.Json` source generators) is used throughout, which avoids the type-confusion risks associated with `Newtonsoft.Json`'s `TypeNameHandling` or `System.Text.Json`'s polymorphic deserialization.

3. **Table Storage key sanitization** is implemented in `TableDocumentTagStore.SanitizeForTableKey()` (line 169-175) using a generated regex that strips invalid characters.

4. **Argument null checks** are consistently applied via `ArgumentNullException.ThrowIfNull()` across all storage providers.

5. **SHA-256 hashing** for document integrity checking uses the modern `SHA256.HashData()` API (not `SHA256.Create()`), which is both performant and does not require `IDisposable` management.

6. **Container name normalization** to lowercase is applied consistently (`ToLowerInvariant()`) preventing case-sensitivity issues in blob/bucket names.

7. **Path traversal defense** in the benchmark file endpoint (line 3854-3873) shows awareness of the risk, even though the implementation could be improved.

8. **Blob Storage request conditions** (`IfMatch`, `IfNoneMatch`) are properly used for concurrency control in `BlobDataStore` operations.

---

## Recommendations Summary

| Priority | Action |
|----------|--------|
| P0 | Add authentication/authorization to all admin endpoints, or restrict them to development mode |
| P0 | Move S3 credentials to configuration/environment variables |
| P1 | Add centralized object ID validation to prevent path traversal |
| P1 | Use parameterized OData filters in Table Storage queries |
| P1 | Remove `exception.Message` from production error responses |
| P2 | Fix benchmark filename validation for Windows case-insensitivity |
| P2 | Remove `Console.WriteLine` debug statements |
| P2 | Stop logging connection string content at startup |
| P3 | Document S3 concurrency limitations |
| P3 | Use `Guid.TryParse` for route parameter validation |
