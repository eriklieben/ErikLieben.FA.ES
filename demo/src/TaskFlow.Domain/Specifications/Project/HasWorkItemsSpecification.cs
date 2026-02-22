using ErikLieben.FA.Specifications;
using TaskFlow.Domain.Aggregates;

namespace TaskFlow.Domain.Specifications.Project;

/// <summary>
/// Specification: Project has work items (checks ordering lists)
/// </summary>
public sealed class HasWorkItemsSpecification()
    : DelegateSpecification<Aggregates.Project>(project =>
        project != null &&
        (project.PlannedItemsOrder.Count > 0 ||
         project.InProgressItemsOrder.Count > 0 ||
         project.CompletedItemsOrder.Count > 0));
