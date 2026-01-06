using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.ValueObjects.Project;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project completed successfully
/// </summary>
public sealed class SuccessfulProjectSpecification()
    : DelegateSpecification<Aggregates.Project>(project =>
        project.IsCompleted && project.Outcome == ProjectOutcome.Successful);
