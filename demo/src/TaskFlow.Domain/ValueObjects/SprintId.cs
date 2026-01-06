namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Sprints
/// </summary>
public record SprintId(string Value)
{
    public static SprintId From(string value) => new(value);
    public static SprintId New() => new(Guid.NewGuid().ToString());
    public override string ToString() => Value;
}
