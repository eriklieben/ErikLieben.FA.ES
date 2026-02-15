using TaskFlow.Domain.ValueObjects.Release;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record CreateReleaseRequest(
    string Name,
    string Version,
    string ProjectId);

public record AddReleaseNoteRequest(
    string Note);

public record RollbackReleaseRequest(
    string Reason);

// Response DTOs

public record ReleaseDto(
    string ReleaseId,
    string Name,
    string Version,
    string ProjectId,
    ReleaseStatus Status,
    string CreatedBy,
    DateTime CreatedAt,
    string? StagedBy,
    DateTime? StagedAt,
    string? DeployedBy,
    DateTime? DeployedAt,
    string? CompletedBy,
    DateTime? CompletedAt,
    string? RolledBackBy,
    DateTime? RolledBackAt,
    string? RollbackReason);

public record ReleaseListDto(
    string ReleaseId,
    string Name,
    string Version,
    string ProjectId,
    ReleaseStatus Status,
    DateTime CreatedAt);

public record ReleaseStatisticsDto(
    int TotalReleases,
    int DraftCount,
    int StagedCount,
    int DeployedCount,
    int CompletedCount,
    int RolledBackCount,
    double CompletionRate,
    double RollbackRate);
