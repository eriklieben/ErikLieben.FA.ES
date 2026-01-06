using Xunit;
using FluentAssertions;
using TaskFlow.Domain.Aggregates;
using TaskFlow.Domain.Events;
using ErikLieben.FA.ES.Testing;
using TaskFlow.Domain;
using TaskFlow.Domain.Actions;
using TaskFlow.Domain.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Domain.ValueObjects.WorkItem;

namespace TaskFlow.Domain.Tests.Aggregates;

/// <summary>
/// Simple unit tests for WorkItem aggregate demonstrating event sourcing testing patterns
/// </summary>
public class SimpleWorkItemTests
{
    public SimpleWorkItemTests()
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
    public async System.Threading.Tasks.Task PlanTask_ShouldCreateWorkItem()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workItem = new WorkItem(stream);

        // Act
        await workItem.PlanTask(
            "project-123",
            "Implement authentication",
            "Add OAuth2 login",
            WorkItemPriority.High,
            UserProfileId.From("planner"));

        // Assert
        workItem.ProjectId.Should().Be("project-123");
        workItem.Title.Should().Be("Implement authentication");
        workItem.Priority.Should().Be(WorkItemPriority.High);
        workItem.Status.Should().Be(WorkItemStatus.Planned);

        context.Assert.ShouldHaveObject("WorkItem", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            .WithEventCount(1);
    }

    [Fact]
    public async System.Threading.Tasks.Task AssignResponsibility_ShouldAssignMember()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Fix bug", "Description", WorkItemPriority.Medium, UserProfileId.From("planner"));

        // Act
        await workItem.AssignResponsibility("dev-456", UserProfileId.From("manager"));

        // Assert
        workItem.AssignedTo.Should().Be("dev-456");

        context.Assert.ShouldHaveObject("WorkItem", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task CommenceWork_ShouldChangeStatusToInProgress()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "cccccccc-cccc-cccc-cccc-cccccccccccc");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Build feature", "Description", WorkItemPriority.High, UserProfileId.From("planner"));
        await workItem.AssignResponsibility("dev-456", UserProfileId.From("manager"));

        // Act
        await workItem.CommenceWork(UserProfileId.From("dev-456"));

        // Assert
        workItem.Status.Should().Be(WorkItemStatus.InProgress);

        context.Assert.ShouldHaveObject("WorkItem", "cccccccc-cccc-cccc-cccc-cccccccccccc")
            .WithEventCount(3);
    }

    [Fact]
    public async System.Threading.Tasks.Task CompleteWork_ShouldMarkAsCompleted()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "dddddddd-dddd-dddd-dddd-dddddddddddd");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Build feature", "Description", WorkItemPriority.High, UserProfileId.From("planner"));
        await workItem.AssignResponsibility("dev-456", UserProfileId.From("manager"));
        await workItem.CommenceWork(UserProfileId.From("dev-456"));

        // Act
        await workItem.CompleteWork("Feature complete", UserProfileId.From("dev-456"));

        // Assert
        workItem.Status.Should().Be(WorkItemStatus.Completed);

        context.Assert.ShouldHaveObject("WorkItem", "dddddddd-dddd-dddd-dddd-dddddddddddd")
            .WithEventCount(4);
    }

    [Fact]
    public async System.Threading.Tasks.Task Reprioritize_ShouldChangePriority()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Fix issue", "Description", WorkItemPriority.Low, UserProfileId.From("planner"));

        // Act
        await workItem.Reprioritize(WorkItemPriority.Critical, "Customer escalation", UserProfileId.From("manager"));

        // Assert
        workItem.Priority.Should().Be(WorkItemPriority.Critical);

        context.Assert.ShouldHaveObject("WorkItem", "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task EstablishDeadline_ShouldSetDeadline()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "ffffffff-ffff-ffff-ffff-ffffffffffff");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Feature", "Description", WorkItemPriority.High, UserProfileId.From("planner"));
        var deadline = DateTime.UtcNow.AddDays(7);

        // Act
        await workItem.EstablishDeadline(deadline, UserProfileId.From("manager"));

        // Assert
        workItem.Deadline.Should().Be(deadline);

        context.Assert.ShouldHaveObject("WorkItem", "ffffffff-ffff-ffff-ffff-ffffffffffff")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task Retag_ShouldUpdateTags()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "aaaabbbb-cccc-dddd-eeee-ffffffff1111");
        var workItem = new WorkItem(stream);
        await workItem.PlanTask("project-123", "Feature", "Description", WorkItemPriority.Medium, UserProfileId.From("planner"));

        // Act
        await workItem.Retag(["backend", "api", "security"], UserProfileId.From("tagger"));

        // Assert
        workItem.Tags.Should().BeEquivalentTo("backend", "api", "security");

        context.Assert.ShouldHaveObject("WorkItem", "aaaabbbb-cccc-dddd-eeee-ffffffff1111")
            .WithEventCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task WorkItemLifecycle_ShouldTrackAllEvents()
    {
        // Arrange
        var context = GetTestContext();
        var stream = await context.GetEventStreamFor("WorkItem", "aaaabbbb-cccc-dddd-eeee-ffffffff2222");
        var workItem = new WorkItem(stream);

        // Act - Complete workflow
        await workItem.PlanTask("project-123", "OAuth2 auth", "Implement OAuth2", WorkItemPriority.High, UserProfileId.From("product-owner"));
        await workItem.ReestimateEffort(16, UserProfileId.From("tech-lead"));
        await workItem.EstablishDeadline(DateTime.UtcNow.AddDays(14), UserProfileId.From("manager"));
        await workItem.Retag(new[] { "backend", "security" }, UserProfileId.From("dev"));
        await workItem.AssignResponsibility("dev-john", UserProfileId.From("tech-lead"));
        await workItem.CommenceWork(UserProfileId.From("dev-john"));
        await workItem.Reprioritize(WorkItemPriority.Critical, "Escalation", UserProfileId.From("product-owner"));
        await workItem.CompleteWork("OAuth2 complete", UserProfileId.From("dev-john"));

        // Assert
        workItem.Priority.Should().Be(WorkItemPriority.Critical);
        workItem.Status.Should().Be(WorkItemStatus.Completed);
        workItem.AssignedTo.Should().Be("dev-john");
        workItem.EstimatedHours.Should().Be(16);
        workItem.Tags.Should().HaveCount(2);

        // Verify all 8 events were stored
        context.Assert.ShouldHaveObject("WorkItem", "aaaabbbb-cccc-dddd-eeee-ffffffff2222")
            .WithEventCount(8);
    }
}
