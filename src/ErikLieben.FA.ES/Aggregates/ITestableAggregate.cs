using System.Diagnostics.CodeAnalysis;
using ErikLieben.FA.ES.Processors;

namespace ErikLieben.FA.ES.Aggregates;

/// <summary>
/// Marker interface for aggregates that support AOT-friendly testing.
/// Provides static abstract members to retrieve object metadata without reflection.
/// </summary>
/// <typeparam name="TSelf">The aggregate type itself (for static abstract support).</typeparam>
/// <remarks>
/// This interface uses C# 11 static abstract members, not static fields.
/// Each implementing type provides its own implementation - this is by design.
/// </remarks>
[SuppressMessage("SonarCloud", "S2743:Static fields should not be used in generic types",
    Justification = "These are static abstract interface members (C# 11), not static fields. Each implementing type provides its own value.")]
public interface ITestableAggregate<TSelf> : IBase
    where TSelf : ITestableAggregate<TSelf>
{
    /// <summary>
    /// Gets the logical object name for this aggregate type.
    /// This is used to identify the aggregate's event stream.
    /// </summary>
    static abstract string ObjectName { get; }

    /// <summary>
    /// Creates a new instance of the aggregate from an event stream.
    /// This enables AOT-friendly aggregate creation without reflection.
    /// </summary>
    /// <param name="stream">The event stream for the aggregate.</param>
    /// <returns>A new instance of the aggregate.</returns>
    static abstract TSelf Create(IEventStream stream);
}

/// <summary>
/// Marker interface for aggregates with strongly-typed identifiers that support AOT-friendly testing.
/// </summary>
/// <typeparam name="TSelf">The aggregate type itself.</typeparam>
/// <typeparam name="TId">The identifier type for the aggregate.</typeparam>
/// <remarks>
/// This interface uses C# 11 static abstract members, not static fields.
/// Each implementing type provides its own implementation - this is by design.
/// </remarks>
[SuppressMessage("SonarCloud", "S2743:Static fields should not be used in generic types",
    Justification = "These are static abstract interface members (C# 11), not static fields. Each implementing type provides its own value.")]
public interface ITestableAggregate<TSelf, TId> : ITestableAggregate<TSelf>
    where TSelf : ITestableAggregate<TSelf, TId>
{
    /// <summary>
    /// Converts a string identifier to the strongly-typed identifier.
    /// </summary>
    /// <param name="id">The string representation of the identifier.</param>
    /// <returns>The strongly-typed identifier.</returns>
    static abstract TId ParseId(string id);

    /// <summary>
    /// Converts the strongly-typed identifier to a string.
    /// </summary>
    /// <param name="id">The strongly-typed identifier.</param>
    /// <returns>The string representation of the identifier.</returns>
    static abstract string FormatId(TId id);
}
