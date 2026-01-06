using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events.Project;
using TaskFlow.Domain.Tests.TestHelpers;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;
using Xunit;

namespace TaskFlow.Domain.Tests.Aggregates;

/// <summary>
/// Fluent builder tests for Project aggregate demonstrating Given-When-Then patterns.
/// These tests mirror the documentation examples.
/// </summary>
public class ProjectFluentTests
{
    public ProjectFluentTests()
    {
        PublishProjectionUpdateAction.Configure(new NoOpProjectionEventPublisher());
    }

    private TestContext GetTestContext()
    {
        var services = new ServiceCollection();
        TaskFlowDomainFactory.Register(services);
        var serviceProvider = services.BuildServiceProvider();
        return TestSetup.GetContext(serviceProvider, TaskFlowDomainFactory.Get);
    }

    private static MemberPermissions ReadWritePermissions => new(CanEdit: true, CanDelete: false, CanInvite: false, CanManageWorkItems: true);
    private static MemberPermissions ReadOnlyPermissions => new(CanEdit: false, CanDelete: false, CanInvite: false, CanManageWorkItems: false);

    [Fact]
    public async Task Can_add_member_to_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.AddTeamMemberWithPermissions(
                    UserProfileId.From("member-1"),
                    "Developer",
                    ReadWritePermissions,
                    UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<MemberJoinedProject>()
                .ShouldHaveState(p => p.TeamMembers.Count == 1));
    }

    [Fact]
    public async Task Can_complete_project_successfully()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.CompleteProjectSuccessfully("All objectives met", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectCompletedSuccessfully>()
                .ShouldHaveState(p => p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.Successful));
    }

    [Fact]
    public async Task Can_deliver_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow),
                new MemberJoinedProject("member-1", "Developer", ReadWritePermissions, "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.DeliverProject("Delivered to production", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectDelivered>()
                .ShouldHaveState(p => p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.Delivered));
    }

    [Fact]
    public async Task Cannot_rebrand_completed_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow),
                new ProjectCompletedSuccessfully("Done", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                var result = await project.RebrandProject("New Name", UserProfileId.From("owner-1"));
                result.IsFailure.Should().BeTrue();
            })
            .Then(result => result
                .ShouldNotHaveAppended<ProjectRebranded>()
                .ShouldHaveState(p => p.Name == "My Project"));
    }

    [Fact]
    public async Task Cannot_add_member_to_completed_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow),
                new ProjectDelivered("Delivered", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                var result = await project.AddTeamMemberWithPermissions(
                    UserProfileId.From("new-member"),
                    "Developer",
                    ReadOnlyPermissions,
                    UserProfileId.From("owner-1"));
                result.IsFailure.Should().BeTrue();
            })
            .Then(result => result
                .ShouldNotHaveAppended<MemberJoinedProject>()
                .ShouldHaveState(p => p.TeamMembers.Count == 0));
    }

    [Fact]
    public async Task Can_reactivate_completed_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow),
                new ProjectCancelled("Budget cuts", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.ReactivateProject("New funding approved", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectReactivated>()
                .ShouldHaveState(p => !p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.None));
    }

    [Fact]
    public async Task Can_remove_team_member()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Description", "owner-1", DateTime.UtcNow),
                new MemberJoinedProject("member-1", "Developer", ReadWritePermissions, "owner-1", DateTime.UtcNow),
                new MemberJoinedProject("member-2", "Designer", ReadOnlyPermissions, "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.RemoveTeamMember(UserProfileId.From("member-1"), UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<MemberLeftProject>()
                .ShouldHaveState(p => p.TeamMembers.Count == 1)
                .ShouldHaveState(p => !p.TeamMembers.ContainsKey(UserProfileId.From("member-1"))));
    }

    [Fact]
    public async Task Project_lifecycle_through_multiple_states()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("E-Commerce", "Build store", "owner-1", now),
                new MemberJoinedProject("dev-1", "Developer", ReadWritePermissions, "owner-1", now.AddMinutes(1)),
                new MemberJoinedProject("dev-2", "Designer", ReadOnlyPermissions, "owner-1", now.AddMinutes(2)),
                new ProjectRebranded("E-Commerce", "E-Commerce Suite", "owner-1", now.AddMinutes(3)))
            .When(async project =>
            {
                await project.DeliverProject("Deployed to production", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectDelivered>()
                .ShouldHaveState(p => p.Name == "E-Commerce Suite")
                .ShouldHaveState(p => p.TeamMembers.Count == 2)
                .ShouldHaveState(p => p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.Delivered));
    }

    [Fact]
    public async Task Can_cancel_project_with_reason()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("Experimental Feature", "R&D project", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.CancelProject("Market conditions changed", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectCancelled>()
                .ShouldHaveState(p => p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.Cancelled));
    }

    [Fact]
    public async Task Can_fail_project()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("Risky Project", "High-risk initiative", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.FailProject("Technical limitations discovered", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectFailed>()
                .ShouldHaveState(p => p.IsCompleted)
                .ShouldHaveState(p => p.Outcome == ProjectOutcome.Failed));
    }

    [Fact]
    public async Task Can_refine_project_scope()
    {
        var context = GetTestContext();
        var projectId = Guid.NewGuid().ToString();

        await AggregateTestBuilder<Project>
            .For("Project", projectId, context, s => new Project(s))
            .Given(
                new ProjectInitiated("My Project", "Initial description", "owner-1", DateTime.UtcNow))
            .When(async project =>
            {
                await project.RefineScope("Updated description with more details", UserProfileId.From("owner-1"));
            })
            .Then(result => result
                .ShouldHaveAppended<ProjectScopeRefined>()
                .ShouldHaveState(p => p.Description == "Updated description with more details"));
    }
}
