# v2 Readiness Assessment — Summary (from previous review session)

## Verdict: **Stay in Preview** (ship `2.0.0-preview.7`)

4 blockers, 8 critical/high issues, and 25+ major items across performance, security, and completeness.

---

## Blockers (must fix before any v2 stable)

| # | Area | Issue |
|---|------|-------|
| B1 | Completeness | **CosmosDB targets only net9.0** — all other packages target net9.0+net10.0. Also has hardcoded version `1.0.0` instead of centralized `PackageVersion` |
| B2 | Completeness | **WebJobs.Isolated.Extensions targets net8.0** — core library is net9.0+. Binary incompatible. Needs update or deprecation |
| B3 | Completeness | **No public API tracking** — `PublicAPI.Shipped.txt` is empty, RS0016 suppressed. Impossible to detect breaking changes between previews |
| B4 | Completeness | **Newtonsoft.Json in CosmosDB conflicts with AOT goals** — library claims AOT-compatible but CosmosDB depends on Newtonsoft |

## Critical (should fix for preview.7)

| # | Area | Issue |
|---|------|-------|
| C1 | Performance | **Blob/S3 read-modify-write on every append** — downloads entire blob, deserializes all events, appends, re-uploads. O(n) per append |
| C2 | Performance | **Non-thread-safe HashSet in CosmosDB** — static `HashSet<string>` under concurrent access |
| C3 | Performance | **Double materialization in ReadAsync** — all events loaded into memory twice |
| C4 | Performance | **BlobProjectionStatusCoordinator downloads ALL status blobs** to filter by status |
| C5 | Security | **No input validation on storage paths** — object IDs flow directly into blob/S3/table paths without sanitization |
| C6 | Security | **Exception messages leaked to clients** in GlobalExceptionHandler regardless of environment |
| C7 | Security | **Connection string logged in full** at Program.cs line 79 |
| C8 | Security | **Hardcoded credentials** in demo Program.cs (S3 keys, Azurite account key) |

## Significant Gaps (provider parity)

| # | Issue |
|---|-------|
| G1 | **S3 missing projection infrastructure** — no S3ProjectionAttribute, no S3ProjectionStatusCoordinator, no routed projection factory |
| G2 | **Table/CosmosDB missing IStreamMetadataProvider** — retention discovery won't work |
| G3 | **Migration/backup only for Azure Blob** — no backup provider for CosmosDB, S3, or Table |
| G4 | **CosmosDB has no EventStreamManagement integration** — no migration, backup, or stream repair |
| G5 | **No explicit event replay API** — only implicit via ReadAsync + rehydration |
| G6 | **Routed projections only for Blob** — no equivalent for other providers |

## Major highlights

- Synchronous `CreateIfNotExists()` blocking thread pool in hot paths
- Sequential I/O that could be parallelized (`Task.WhenAll`)
- `Activator.CreateInstance` in ProjectionLoader not AOT-compatible
- Admin endpoints expose connection strings with no auth
- `ClearAllStorage` endpoint has no safety net
- S3 credentials stored as plain strings without `[JsonIgnore]`

## Recommended Path

### For preview.7 (next release):
1. Fix target framework alignment (CosmosDB -> net9.0+net10.0, drop or update WebJobs)
2. Add input validation for object IDs/names at the library boundary
3. Fix thread-safety in CosmosDB `ClosedStreamCache`
4. Fix exception message leakage + connection string logging
5. Publish new NuGet packages from current source (fix binary incompatibility)

### For v2.0.0 stable:
1. Enable public API tracking (RS0016)
2. Document provider parity gaps clearly (S3/CosmosDB/Table limitations)
3. Address the read-modify-write pattern (append blobs or event-per-blob)
4. Add S3 projection status coordinator
5. Resolve Newtonsoft/AOT conflict in CosmosDB provider
6. Add security documentation for consumers

The **core library and Azure Blob provider are mature**. S3 is close. CosmosDB and Table have the most gaps.
