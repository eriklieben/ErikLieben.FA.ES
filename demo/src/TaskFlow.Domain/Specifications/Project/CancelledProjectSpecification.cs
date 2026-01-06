using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects.Project;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project was cancelled
/// </summary>
public sealed class CancelledProjectSpecification()
    : DelegateSpecification<Aggregates.Project>(project =>
        project.IsCompleted && project.Outcome == ProjectOutcome.Cancelled);
