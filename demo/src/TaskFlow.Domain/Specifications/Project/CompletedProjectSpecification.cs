using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project has been completed
/// </summary>
public sealed class CompletedProjectSpecification()
    : DelegateSpecification<Aggregates.Project>(project => project.IsCompleted);
