namespace ErikLieben.FA.ES.EventStreamManagement.Verification;

/// <summary>
/// Implementation of migration plan.
/// </summary>
public class MigrationPlan : IMigrationPlan
{
    /// <inheritdoc/>
    public Guid PlanId { get; init; }

    /// <inheritdoc/>
    public required StreamAnalysis SourceAnalysis { get; init; }

    /// <inheritdoc/>
    public required TransformationSimulation TransformationSimulation { get; init; }

    /// <inheritdoc/>
    public required ResourceEstimate ResourceEstimate { get; init; }

    /// <inheritdoc/>
    public required IReadOnlyList<Prerequisite> Prerequisites { get; init; }

    /// <inheritdoc/>
    public required IReadOnlyList<MigrationRisk> Risks { get; init; }

    /// <inheritdoc/>
    public required IReadOnlyList<string> RecommendedPhases { get; init; }

    /// <inheritdoc/>
    public bool IsFeasible { get; init; }
}
