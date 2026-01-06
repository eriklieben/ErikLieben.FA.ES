# ErikLieben.FA.ES Production Readiness Plan

This document tracks the work needed to make the library fully production-ready for v2.0.0.

## Overview

| Category | Items | Status |
|----------|-------|--------|
| Stream Tags | 2 | **Complete** |
| Event Stream Management | 5 | **Complete** |
| Backup/Restore | 1 | **Complete** |
| Session Management | 2 | **Complete** |
| Bulk Operations | 1 | **Complete** |
| **Total** | **11** | **All Complete** |

---

## 1. Stream Tags Implementation

### 1.1 Stream Tag SetTagAsync/RemoveTagAsync
- **File**: `src/ErikLieben.FA.ES/Documents/ObjectDocumentWithTags.cs`
- **Lines**: 45-47, 64-66
- **Issue**: `NotImplementedException` thrown for `TagTypes.StreamTag`
- **Impact**: Cannot set/remove tags at stream level (only document level works)
- **Solution**: Implement stream-scoped tag storage logic
- **Status**: [x] **Complete** - Added optional `streamTagStore` parameter to constructor, implemented SetTagAsync/RemoveTagAsync for StreamTag type

### 1.2 BlobStreamTagStore.GetAsync Query
- **File**: `src/ErikLieben.FA.ES.AzureStorage/Blob/BlobStreamTagStore.cs`
- **Line**: ~90-92
- **Issue**: `NotImplementedException` - cannot query streams by tag
- **Impact**: Cannot find all streams with a specific tag
- **Solution**: Implement blob tag query or index-based lookup
- **Status**: [x] **Complete** - Refactored storage to `tags/stream-by-tag/{tag}.json` structure, implemented GetAsync to return stream identifiers for a tag

---

## 2. Event Stream Management Execution

### 2.1 Dry-Run Mode
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/MigrationExecutor.cs`
- **Issue**: Dry-run mode for migrations
- **Impact**: Cannot preview migrations before executing
- **Solution**: Add simulation mode that validates without persisting
- **Status**: [x] **Complete** - Already implemented in `MigrationExecutor.ExecuteDryRunAsync()`

### 2.2 Backup Execution
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/MigrationExecutor.cs`
- **Issue**: Backup before migration
- **Impact**: No automated backup before migrations
- **Solution**: Call backup provider before migration starts
- **Status**: [x] **Complete** - Already implemented in `MigrationExecutor.CreateBackupAsync()`

### 2.3 Verification Execution
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/MigrationExecutor.cs`
- **Issue**: Post-migration verification
- **Impact**: Cannot validate migration success automatically
- **Solution**: Execute verification builder after migration
- **Status**: [x] **Complete** - Already implemented in `MigrationExecutor.VerifyMigrationAsync()`

### 2.4 Book Closing (Archival)
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/MigrationExecutor.cs`
- **Issue**: Stream archival after migration
- **Impact**: Cannot archive old streams after migration
- **Solution**: Implement stream archival to cold storage
- **Status**: [x] **Complete** - Already implemented in `MigrationExecutor.CloseBookAsync()`

### 2.5 Rollback Execution
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/MigrationExecutor.cs`
- **Issue**: Migration rollback
- **Impact**: Cannot undo failed migrations
- **Solution**: Restore from backup or reverse transformations
- **Status**: [x] **Complete** - Already implemented in `MigrationExecutor.RollbackAsync()`

---

## 3. Backup/Restore Service

### 3.1 Standalone Backup/Restore Service
- **Files**: Multiple new files
- **Issue**: Need standalone backup/restore API independent of migrations
- **Impact**: Cannot backup/restore streams without migration context
- **Solution**: Full backup/restore service with single-stream and bulk operations
- **Status**: [x] **Complete** (2026-01-06)

**Implementation:**
- `src/ErikLieben.FA.ES.EventStreamManagement/Backup/IBackupRestoreService.cs`
- `src/ErikLieben.FA.ES.EventStreamManagement/Backup/BackupRestoreService.cs`
- `src/ErikLieben.FA.ES.EventStreamManagement/Backup/BackupOptions.cs`
- `src/ErikLieben.FA.ES.EventStreamManagement/Backup/IBackupRegistry.cs`
- `src/ErikLieben.FA.ES.AzureStorage/Migration/AzureBlobBackupRegistry.cs`

**Features:**
- Single stream backup/restore
- Bulk backup/restore with parallel processing
- Backup listing, validation, deletion
- Retention and cleanup
- **79 unit tests** (51 BackupRestoreService + 28 AzureBlobBackup)

---

## 4. Session Management

### 4.1 LeasedSession Commit Failure Handling
- **File**: `src/ErikLieben.FA.ES/EventStream/LeasedSession.cs`
- **Issue**: Proper exception handling and state tracking during commit
- **Impact**: Could not determine recovery strategy after commit failure
- **Solution**: Implemented `CommitFailedException` with context for recovery
- **Status**: [x] **Complete** (2026-01-06)

**Implementation:**
- Created `CommitFailedException` with recovery context (original version, attempted version, events may be written flag)
- Commit order: document metadata first (for ETag concurrency control), then events
- Version restoration on failure enables retry with same buffer
- Clear distinction between safe-to-retry (document failed) vs potentially-inconsistent (events may have partially written)

**Key Changes:**
- `src/ErikLieben.FA.ES/Exceptions/CommitFailedException.cs` - New exception with recovery context
- `src/ErikLieben.FA.ES/EventStream/LeasedSession.cs` - CommitState tracking, proper exception wrapping
- **54 unit tests** covering all failure scenarios (8 new commit failure tests)

### 4.2 Chunk Settings Default
- **File**: `src/ErikLieben.FA.ES/EventStream/LeasedSession.cs`
- **Line**: 300
- **Issue**: TODO - what should default chunk size be?
- **Impact**: Unclear optimal configuration
- **Solution**: Research, benchmark, document recommendation
- **Status**: [x] **Complete** (2026-01-06)

**Decision: Default chunk size = 1000 events**

Rationale:
- Keeps blob chunks at ~1-5MB (assuming ~1-5KB per event with JSON payload)
- Fast read-modify-write operations for append-heavy workloads
- Most streams have <1000 events and don't need chunking at all
- Balances chunk management overhead vs individual chunk size
- Consistent with defaults in `EventStreamBlobSettings` and `EventStreamTableSettings`

**Documentation added:**
- `StreamChunkSettings.cs` - Added XML docs explaining when to use chunking and recommended sizes
- `LeasedSession.cs` - Added inline comments explaining the default rationale

---

## 5. Bulk Operations

### 5.1 Bulk Migration Support
- **File**: `src/ErikLieben.FA.ES.EventStreamManagement/Core/EventStreamMigrationService.cs`
- **Issue**: TODO - proper bulk migration
- **Impact**: Large migrations may be slow
- **Solution**: Implement batch processing with parallelization
- **Status**: [x] **Complete** (2026-01-06)

**Implementation:**
- `src/ErikLieben.FA.ES.EventStreamManagement/Core/BulkMigrationBuilder.cs`
- Updated `EventStreamMigrationService.ForDocuments()` to use `BulkMigrationBuilder`

**Features:**
- Parallel migration with configurable concurrency (`WithMaxConcurrency()`)
- Continue-on-error support (`WithContinueOnError()`)
- Progress callbacks (`WithBulkProgress()`)
- Custom stream identifier factory (`CopyToNewStreams()`)

---

## Execution Order

Recommended order based on dependencies and impact:

### Phase 1: Foundation (Stream Tags) - **COMPLETE**
1. [x] 1.1 Stream Tag SetTagAsync/RemoveTagAsync
2. [x] 1.2 BlobStreamTagStore.GetAsync Query

### Phase 2: Migration Core (Event Stream Management) - **COMPLETE**
3. [x] 2.1 Dry-Run Mode (already implemented)
4. [x] 2.2 Backup Execution (already implemented)
5. [x] 3.1 Standalone Backup/Restore Service
6. [x] 2.3 Verification Execution (already implemented)
7. [x] 2.5 Rollback Execution (already implemented)
8. [x] 2.4 Book Closing (already implemented)

### Phase 3: Bulk Operations - **COMPLETE**
9. [x] 5.1 Bulk Migration Support

### Phase 4: Session Management - **COMPLETE**
10. [x] 4.1 LeasedSession Commit Failure Handling
11. [x] 4.2 Chunk Settings Default (documentation)

---

## Progress Tracking

| Date | Item | Status | Notes |
|------|------|--------|-------|
| 2026-01-05 | Plan created | Done | Initial assessment complete |
| 2026-01-05 | 1.1 Stream Tag SetTagAsync/RemoveTagAsync | Done | Added streamTagStore to ObjectDocumentWithTags, updated all event stream factories |
| 2026-01-05 | 1.2 BlobStreamTagStore.GetAsync Query | Done | Refactored to store by tag name, implemented GetAsync and RemoveAsync |
| 2026-01-05 | Phase 1 tests | Done | 42 tests for stream tag functionality (29 BlobStreamTagStore + 13 ObjectDocumentWithTags) |
| 2026-01-06 | Live Migration - preview.6 verification | Done | Verified 2.0.0-preview.6 published on NuGet |
| 2026-01-06 | Live Migration - Hash conflict fix | Done | Reload document before close event append, re-check for tailing events |
| 2026-01-06 | Live Migration - UI phase indicator | Done | Fixed complete phase showing green instead of blue/pulsing |
| 2026-01-06 | Live Migration - Target event data | Done | Load transformed data from storage during migration via new API endpoint |
| 2026-01-06 | Live Migration - Post-migration events | Done | Live events after completion now correctly route to target stream |
| 2026-01-06 | Live Migration - Code cleanup | Done | Removed simulated mode dead code (~400 lines removed) |
| 2026-01-06 | 4.1 Backup/Restore Service | Done | Full implementation with 79 tests |
| 2026-01-06 | 6.1 Bulk Migration Support | Done | BulkMigrationBuilder with parallel processing |
| 2026-01-06 | 4.1 LeasedSession Commit Failure Handling | Done | CommitFailedException, document-first order, 54 tests |
| 2026-01-06 | 4.2 Chunk Settings Default | Done | Documented 1000 as default, added XML docs to StreamChunkSettings |

---

## Notes

- Each item should include unit tests
- Update documentation after implementation
- Consider backward compatibility
- Add to CHANGELOG.md when complete

---

# Live Migration Demo - Complete (2026-01-06)

## Summary

The live migration demo is now fully functional and production-ready.

## Library Changes (ErikLieben.FA.ES.EventStreamManagement)

**Published in 2.0.0-preview.5:**
- Added `OnEventCopied(Func<LiveMigrationEventProgress, Task>)` - async callback for per-event progress
- Added `OnBeforeAppend(Func<LiveMigrationEventProgress, Task>)` - async callback before each append (enables demo delays)
- Added `LiveMigrationEventProgress` record with transformation metadata

**Published in 2.0.0-preview.6:**
- Fixed infinite loop bug when source stream is already closed
- Added check in `AttemptCloseAsync` to detect existing `StreamClosedEvent`

**Fixed (local, using project reference):**
- Hash conflict bug: Reload document before appending close event to get current hash
- Re-check for tailing events after reload to handle race conditions

## Demo Changes (TaskFlow)

### Backend (TaskFlow.Api)
- Updated to 2.0.0-preview.6 NuGet packages (with local project reference for hash fix)
- Added `GetTargetStreamEvents` API endpoint to read events from target stream during migration
- SignalR broadcasting for per-event progress
- Demo delay support via `OnBeforeAppend` callback

### Frontend (taskflow-web)
- **Target event data loading**: Events now show actual transformed data during migration (debounced API calls)
- **Phase indicator fix**: Complete phase shows green (not blue/pulsing)
- **Post-migration live events**: Events added after migration correctly route to target stream
- **Code cleanup**: Removed ~400 lines of dead simulated mode code

## Key Files Modified

| File | Changes |
|------|---------|
| `src/.../LiveMigration/LiveMigrationExecutor.cs` | Hash conflict fix: reload document, re-check tailing events |
| `demo/.../StreamMigrationDemoEndpoints.cs` | Added GetTargetStreamEvents endpoint |
| `demo/.../stream-migration.component.ts` | Target event loading, post-migration routing, cleanup |
| `demo/.../stream-migration.component.css` | Complete phase styling (green, no pulse) |
| `demo/.../stream-migration-api.service.ts` | Added getTargetStreamEvents, removed unused methods |

## Issues Fixed

1. **Hash conflict causing infinite loop** - Background processes update document hash; now reload before close
2. **Target events showing empty `{}`** - Now loads actual data from storage during migration
3. **Post-migration events going to source** - Now correctly routes to target stream
4. **Complete phase staying blue** - CSS fix for green complete indicator
5. **Dead simulated mode code** - Removed all unused simulated migration code

## Verified Working

- Per-event progress shows in UI with actual data
- Demo delay works (slow mode toggle)
- Migration completes successfully (no infinite loop)
- "Add Live Event" works during migration (goes to source, caught up to target)
- "Add Live Event" after migration goes to target stream
- Phase indicator shows green when complete
