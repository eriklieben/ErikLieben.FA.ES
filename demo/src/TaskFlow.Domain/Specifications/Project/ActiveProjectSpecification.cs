using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project is in an active (not completed) state
/// </summary>
public sealed class ActiveProjectSpecification()
    : DelegateSpecification<Aggregates.Project>(project => !project.IsCompleted);
