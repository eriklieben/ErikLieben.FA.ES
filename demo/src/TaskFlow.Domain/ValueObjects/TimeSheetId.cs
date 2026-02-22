namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for TimeSheets
/// </summary>
public record TimeSheetId(string Value)
{
    public static TimeSheetId From(string value) => new(value);
    public static TimeSheetId New() => new(Guid.NewGuid().ToString());
    public override string ToString() => Value;
}
