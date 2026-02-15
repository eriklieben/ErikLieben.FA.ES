namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Releases
/// </summary>
public record ReleaseId(string Value)
{
    public static ReleaseId From(string value) => new(value);
    public static ReleaseId New() => new(Guid.NewGuid().ToString());
    public override string ToString() => Value;
}
