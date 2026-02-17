using TaskFlow.Domain.ValueObjects.TimeSheet;

namespace TaskFlow.Api.DTOs;

// Command DTOs (Requests)

public record CreateTimeSheetRequest(
    string UserId,
    string ProjectId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public record LogTimeRequest(
    string WorkItemId,
    decimal Hours,
    string Description,
    DateTime Date);

public record AdjustTimeRequest(
    string EntryId,
    decimal NewHours,
    string NewDescription,
    string Reason);

public record RemoveTimeEntryRequest(
    string EntryId,
    string Reason);

public record RejectTimeSheetRequest(
    string Reason);

// Response DTOs

public record TimeSheetDto(
    string TimeSheetId,
    string UserId,
    string ProjectId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    TimeSheetStatus Status,
    int EntryCount,
    decimal TotalHours,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason);

public record TimeSheetListDto(
    string TimeSheetId,
    string UserId,
    string ProjectId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    TimeSheetStatus Status,
    decimal TotalHours,
    DateTime CreatedAt);

public record TimeSheetStatisticsDto(
    int TotalTimeSheets,
    int OpenCount,
    int SubmittedCount,
    int ApprovedCount,
    int RejectedCount,
    decimal TotalHoursLogged,
    decimal TotalApprovedHours);
