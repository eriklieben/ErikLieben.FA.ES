using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace ErikLieben.FA.ES.Azure.Functions.Worker.Extensions;

/// <summary>
/// Specifies that the function should update one or more projections to their latest state after successful execution.
/// </summary>
/// <remarks>
/// <para>
/// This attribute can be applied multiple times to a function to update multiple projections.
/// All projection updates are executed after the function completes successfully.
/// If any projection update fails, an exception is thrown.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Function(nameof(CreateWorkItem))]
/// [ProjectionOutput&lt;ProjectKanbanBoard&gt;]
/// [ProjectionOutput&lt;ActiveWorkItems&gt;]
/// public async Task&lt;HttpResponseData&gt; CreateWorkItem(...)
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ProjectionOutputAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionOutputAttribute"/> class.
    /// </summary>
    /// <param name="projectionType">The type of projection to update.</param>
    public ProjectionOutputAttribute(Type projectionType)
    {
        ProjectionType = projectionType ?? throw new ArgumentNullException(nameof(projectionType));
    }

    /// <summary>
    /// Gets the type of projection to update after function execution.
    /// </summary>
    public Type ProjectionType { get; }

    /// <summary>
    /// Gets or sets the optional blob name for routed projections.
    /// If not specified, the default projection instance is updated.
    /// </summary>
    public string? BlobName { get; set; }

    /// <summary>
    /// Gets or sets whether to save the projection after updating.
    /// Defaults to true.
    /// </summary>
    public bool SaveAfterUpdate { get; set; } = true;
}

/// <summary>
/// Specifies that the function should update a projection of type <typeparamref name="T"/>
/// to its latest state after successful execution.
/// </summary>
/// <typeparam name="T">The type of projection to update.</typeparam>
/// <remarks>
/// <para>
/// This attribute can be applied multiple times to a function to update multiple projections.
/// All projection updates are executed after the function completes successfully.
/// If any projection update fails, an exception is thrown.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Function(nameof(CreateWorkItem))]
/// [ProjectionOutput&lt;ProjectKanbanBoard&gt;]
/// [ProjectionOutput&lt;ActiveWorkItems&gt;]
/// public async Task&lt;HttpResponseData&gt; CreateWorkItem(...)
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ProjectionOutputAttribute<T> : ProjectionOutputAttribute where T : Projections.Projection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionOutputAttribute{T}"/> class.
    /// </summary>
    public ProjectionOutputAttribute() : base(typeof(T))
    {
    }
}
