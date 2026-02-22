using System.Diagnostics.CodeAnalysis;

namespace ErikLieben.FA.ES.Projections;

/// <summary>
/// Marker interface for projections that support AOT-friendly testing.
/// Provides a static factory method to create projection instances without reflection.
/// </summary>
/// <typeparam name="TSelf">The projection type itself (for static abstract support).</typeparam>
/// <remarks>
/// This interface uses C# 11 static abstract members, not static fields.
/// Each implementing type provides its own implementation - this is by design.
/// </remarks>
[SuppressMessage("SonarCloud", "S2743:Static fields should not be used in generic types",
    Justification = "These are static abstract interface members (C# 11), not static fields. Each implementing type provides its own value.")]
public interface ITestableProjection<TSelf>
    where TSelf : ITestableProjection<TSelf>
{
    /// <summary>
    /// Creates a new instance of the projection with the required factories.
    /// This factory method enables AOT-friendly projection instantiation without reflection.
    /// </summary>
    /// <param name="documentFactory">The document factory for creating/retrieving documents.</param>
    /// <param name="eventStreamFactory">The event stream factory for accessing event streams.</param>
    /// <returns>A new instance of the projection.</returns>
    static abstract TSelf Create(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory);
}
