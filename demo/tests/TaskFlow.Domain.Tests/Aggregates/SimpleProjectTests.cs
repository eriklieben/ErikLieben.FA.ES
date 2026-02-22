using Xunit;
using FluentAssertions;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.Project;
using ErikLieben.FA.ES.Testing;
using TaskFlow.Domain;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.Domain.Tests.Aggregates;

/// <summary>
/// Simple unit tests for Project aggregate demonstrating event sourcing testing patterns
/// </summary>
public class SimpleProjectTests
{
    public SimpleProjectTests()
    {
        // Configure the PublishProjectionUpdateAction with a no-op publisher for tests
        PublishProjectionUpdateAction.Configure(new NoOpProjectionEventPublisher());
    }

    private TestContext GetTestContext()
    {
        var services = new ServiceCollection();
        TaskFlowDomainFactory.Register(services);
        var serviceProvider = services.BuildServiceProvider();

        return TestSetup.GetContext(serviceProvider, TaskFlowDomainFactory.Get);
    }

    [Fact]
    public async System.Threading.Tasks.Task InitiateProject_ShouldCreateProjectInitiatedEvent()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("Project", "11111111-1111-1111-1111-111111111111");
        var project = new Project(stream);

        // Act
        await project.InitiateProject("Test Project", "A test project", UserProfileId.From("owner-123"));

        // Assert - Verify event was created
        project.Name.Should().Be("Test Project");
        project.Description.Should().Be("A test project");
        project.OwnerId.Should().Be(UserProfileId.From("owner-123"));
        project.IsCompleted.Should().BeFalse();

        // Verify event in store
        context.Assert.ShouldHaveObject("Project", "11111111-1111-1111-1111-111111111111")
            .WithEventCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task RebrandProject_ShouldUpdateProjectName()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("Project", "22222222-2222-2222-2222-222222222222");
        var project = new Project(stream);
        await project.InitiateProject("Original Name", "Description", UserProfileId.From("owner-123"));

        // Act
        await project.RebrandProject("New Name", UserProfileId.From("user-1"));

        // Assert
        project.Name.Should().Be("New Name");

        // Verify two events in store
        context.Assert.ShouldHaveObject("Project", "22222222-2222-2222-2222-222222222222")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task CompleteProject_ShouldMarkAsCompleted()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("Project", "33333333-3333-3333-3333-333333333333");
        var project = new Project(stream);
        await project.InitiateProject("Test Project", "Description", UserProfileId.From("owner-123"));

        // Act
        await project.CompleteProjectSuccessfully("All done!", UserProfileId.From("user-1"));

        // Assert
        project.IsCompleted.Should().BeTrue();
        project.Outcome.Should().Be(ProjectOutcome.Successful);

        context.Assert.ShouldHaveObject("Project", "33333333-3333-3333-3333-333333333333")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddTeamMember_ShouldAddMemberToTeam()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("Project", "44444444-4444-4444-4444-444444444444");
        var project = new Project(stream);
        await project.InitiateProject("Test Project", "Description", UserProfileId.From("owner-123"));

        // Act
        await project.AddTeamMember(UserProfileId.From("dev-456"), "Developer", UserProfileId.From("owner-123"));

        // Assert
        project.TeamMembers.Should().ContainKey(UserProfileId.From("dev-456"));
        project.TeamMembers[UserProfileId.From("dev-456")].Should().Be("Developer");

        context.Assert.ShouldHaveObject("Project", "44444444-4444-4444-4444-444444444444")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProjectLifecycle_ShouldTrackAllChanges()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("Project", "55555555-5555-5555-5555-555555555555");
        var project = new Project(stream);

        // Act - Full lifecycle
        await project.InitiateProject("E-Commerce", "Build store", UserProfileId.From("owner-123"));
        await project.AddTeamMember(UserProfileId.From("dev-1"), "Developer", UserProfileId.From("owner-123"));
        await project.AddTeamMember(UserProfileId.From("dev-2"), "Designer", UserProfileId.From("owner-123"));
        await project.RebrandProject("E-Commerce Suite", UserProfileId.From("owner-123"));
        await project.DeliverProject("Delivered to production", UserProfileId.From("owner-123"));

        // Assert
        project.Name.Should().Be("E-Commerce Suite");
        project.IsCompleted.Should().BeTrue();
        project.Outcome.Should().Be(ProjectOutcome.Delivered);
        project.TeamMembers.Should().HaveCount(2);

        // Verify all 5 events were stored
        context.Assert.ShouldHaveObject("Project", "55555555-5555-5555-5555-555555555555")
            .WithEventCount(5);
    }
}
