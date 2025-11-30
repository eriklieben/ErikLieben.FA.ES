using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErikLieben.FA.ES;
using ErikLieben.FA.ES.Aggregates;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.Documents;
using ErikLieben.FA.ES.Processors;
using ErikLieben.FA.ES.Projections;
using ErikLieben.FA.ES.Testing;
using ErikLieben.FA.ES.Testing.Builders;
using Xunit;

namespace ErikLieben.FA.ES.Testing.Tests.Builders;

/// <summary>
/// Tests demonstrating both patterns for using ProjectionTestBuilder:
/// 1. ITestableProjection pattern - Simpler syntax when projection implements the interface
/// 2. Explicit factory pattern - More flexible, works with any projection
/// </summary>
public partial class ProjectionTestBuilderPatternsTests
{
    #region Events

    [EventName("ProjectCreated")]
    private record ProjectCreated(string Name, string OwnerId);

    [EventName("TaskAdded")]
    private record TaskAdded(string TaskId, string Title);

    [EventName("TaskCompleted")]
    private record TaskCompleted(string TaskId);

    [JsonSerializable(typeof(ProjectCreated))]
    [JsonSerializable(typeof(TaskAdded))]
    [JsonSerializable(typeof(TaskCompleted))]
    private partial class ProjectEventsJsonContext : JsonSerializerContext { }

    #endregion

    #region Test Aggregate (for Given<TAggregate> support)

    private class Project : Aggregate, ITestableAggregate<Project>
    {
        public static string ObjectName => "project";
        public static Project Create(IEventStream stream) => new Project(stream);

        public Project(IEventStream stream) : base(stream)
        {
            stream.EventTypeRegistry.Add(
                typeof(ProjectCreated),
                "ProjectCreated",
                ProjectEventsJsonContext.Default.ProjectCreated);
            stream.EventTypeRegistry.Add(
                typeof(TaskAdded),
                "TaskAdded",
                ProjectEventsJsonContext.Default.TaskAdded);
            stream.EventTypeRegistry.Add(
                typeof(TaskCompleted),
                "TaskCompleted",
                ProjectEventsJsonContext.Default.TaskCompleted);
        }

        public override void Fold(IEvent @event) { }
    }

    #endregion

    #region Pattern 1: ITestableProjection - Projection implementing the interface

    /// <summary>
    /// This projection implements ITestableProjection{TSelf} which provides:
    /// - static TSelf Create(IObjectDocumentFactory, IEventStreamFactory) => Factory method
    ///
    /// This enables the simpler test syntax:
    ///   ProjectionTestBuilder.For{ProjectStats}(context)
    /// </summary>
    private class ProjectStats : Projection, ITestableProjection<ProjectStats>
    {
        // ITestableProjection implementation
        public static ProjectStats Create(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            => new ProjectStats(documentFactory, eventStreamFactory);

        public ProjectStats(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory)
        {
            Checkpoint = new Checkpoint();
        }

        public int TotalProjects { get; private set; }
        public int TotalTasks { get; private set; }
        public int CompletedTasks { get; private set; }
        public List<string> ProjectNames { get; } = new();

        public override async Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "ProjectCreated":
                        var created = JsonSerializer.Deserialize(
                            jsonEvent.Payload!,
                            ProjectEventsJsonContext.Default.ProjectCreated);
                        if (created != null)
                        {
                            TotalProjects++;
                            ProjectNames.Add(created.Name);
                        }
                        break;
                    case "TaskAdded":
                        TotalTasks++;
                        break;
                    case "TaskCompleted":
                        CompletedTasks++;
                        break;
                }
            }
            await Task.CompletedTask;
        }

        public override string ToJson() => JsonSerializer.Serialize(this);
        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();
        public override Checkpoint Checkpoint { get; set; }
    }

    #endregion

    #region Pattern 2: Explicit Factory - Legacy projection without interface

    /// <summary>
    /// This projection does NOT implement ITestableProjection.
    /// It requires the explicit factory pattern:
    ///   ProjectionTestBuilder{TaskSummary}.Create(context, (docFactory, streamFactory) => new TaskSummary(...))
    /// </summary>
    private class TaskSummary : Projection
    {
        public TaskSummary(IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)
            : base(documentFactory, eventStreamFactory)
        {
            Checkpoint = new Checkpoint();
        }

        public int TaskCount { get; private set; }
        public int CompletedCount { get; private set; }

        public override async Task Fold<T>(IEvent @event, VersionToken versionToken, T? data = null, IExecutionContext? parentContext = null)
            where T : class
        {
            if (@event is JsonEvent jsonEvent)
            {
                switch (jsonEvent.EventType)
                {
                    case "TaskAdded":
                        TaskCount++;
                        break;
                    case "TaskCompleted":
                        CompletedCount++;
                        break;
                }
            }
            await Task.CompletedTask;
        }

        public override string ToJson() => JsonSerializer.Serialize(this);
        protected override Task PostWhenAll(IObjectDocument document) => Task.CompletedTask;
        protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories => new();
        public override Checkpoint Checkpoint { get; set; }
    }

    #endregion

    #region Test Helpers

    private static TestContext CreateTestContext()
    {
        var provider = new SimpleServiceProvider();
        return TestSetup.GetContext(provider, _ => typeof(DummyFactory));
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private class DummyFactory : IAggregateCovarianceFactory<IBase>
    {
        public string GetObjectName() => "dummy";
        public IBase Create(IEventStream eventStream) => new Project(eventStream);
        public IBase Create(IObjectDocument document) => throw new NotImplementedException();
    }

    #endregion

    #region Pattern 1 Tests: ITestableProjection Pattern

    /// <summary>
    /// Demonstrates the simplest syntax using ITestableProjection.
    /// The factory is provided by the projection's static Create method.
    /// </summary>
    [Fact]
    public async Task Pattern1_ITestableProjection_SimplestSyntax()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Note: Only context needed!
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .GivenEvents("project", "proj-1", new ProjectCreated("My Project", "owner-1"))
            .UpdateToLatest();

        // Assert
        var assertion = builder.Then();
        assertion.ShouldHaveProperty(p => p.TotalProjects, 1);
        assertion.ShouldHaveProperty(p => p.ProjectNames.Count, 1);
    }

    [Fact]
    public async Task Pattern1_ITestableProjection_WithTypedAggregate()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Using Given<TAggregate> for type-safe aggregate reference
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1", new ProjectCreated("Project Alpha", "owner-1"))
            .Given<Project>("proj-2", new ProjectCreated("Project Beta", "owner-2"))
            .UpdateToLatest();

        // Assert
        var assertion = builder.Then();
        assertion.ShouldHaveProperty(p => p.TotalProjects, 2);
        assertion.ShouldHaveState(p => p.ProjectNames.Contains("Project Alpha"));
        assertion.ShouldHaveState(p => p.ProjectNames.Contains("Project Beta"));
    }

    [Fact]
    public async Task Pattern1_ITestableProjection_MultipleEventTypes()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1",
                new ProjectCreated("Project One", "owner-1"),
                new TaskAdded("task-1", "First Task"),
                new TaskAdded("task-2", "Second Task"),
                new TaskCompleted("task-1"))
            .UpdateToLatest();

        // Assert
        var assertion = builder.Then();
        assertion.ShouldHaveProperty(p => p.TotalProjects, 1);
        assertion.ShouldHaveProperty(p => p.TotalTasks, 2);
        assertion.ShouldHaveProperty(p => p.CompletedTasks, 1);
    }

    [Fact]
    public async Task Pattern1_ITestableProjection_MultipleAggregates()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Events from multiple aggregate instances
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1",
                new ProjectCreated("Project A", "owner-1"),
                new TaskAdded("task-1", "Task A1"))
            .Given<Project>("proj-2",
                new ProjectCreated("Project B", "owner-2"),
                new TaskAdded("task-2", "Task B1"),
                new TaskAdded("task-3", "Task B2"))
            .UpdateToLatest();

        // Assert
        var assertion = builder.Then();
        assertion.ShouldHaveProperty(p => p.TotalProjects, 2);
        assertion.ShouldHaveProperty(p => p.TotalTasks, 3);
        assertion.ShouldHaveCheckpointCount(2);
    }

    #endregion

    #region Pattern 2 Tests: Explicit Factory Pattern

    /// <summary>
    /// Demonstrates the explicit factory pattern for projections that don't implement ITestableProjection.
    /// You must provide: context and a factory function.
    /// </summary>
    [Fact]
    public async Task Pattern2_ExplicitFactory_FullSyntax()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - Note: Factory function must be explicitly provided
        var builder = await ProjectionTestBuilder<TaskSummary>.Create(
            context,
            (docFactory, streamFactory) => new TaskSummary(docFactory, streamFactory))
            .GivenEvents("project", "proj-1",
                new TaskAdded("task-1", "Task One"),
                new TaskAdded("task-2", "Task Two"))
            .UpdateToLatest();

        // Assert
        builder.Then()
            .ShouldHaveProperty(p => p.TaskCount, 2)
            .ShouldHaveProperty(p => p.CompletedCount, 0);
    }

    [Fact]
    public async Task Pattern2_ExplicitFactory_WithGivenEventsFrom()
    {
        // Arrange
        var context = CreateTestContext();

        // First wrap domain events as IEvent[]
        var events1 = new IEvent[] { WrapEvent(new TaskAdded("task-1", "Task 1"), 0) };
        var events2 = new IEvent[] { WrapEvent(new TaskAdded("task-2", "Task 2"), 0), WrapEvent(new TaskCompleted("task-2"), 1) };

        // Act - Using GivenEventsFrom for multiple streams
        var builder = await ProjectionTestBuilder<TaskSummary>.Create(
            context,
            (docFactory, streamFactory) => new TaskSummary(docFactory, streamFactory))
            .GivenEventsFrom(
                ("project", "proj-1", events1),
                ("project", "proj-2", events2))
            .UpdateToLatest();

        // Assert
        builder.Then()
            .ShouldHaveProperty(p => p.TaskCount, 2)
            .ShouldHaveProperty(p => p.CompletedCount, 1);
    }

    [Fact]
    public async Task Pattern2_ExplicitFactory_WithExistingProjectionInstance()
    {
        // Arrange
        var context = CreateTestContext();
        var existingProjection = new TaskSummary(context.DocumentFactory, context.EventStreamFactory);

        // Act - Using Create with existing instance
        var builder = await ProjectionTestBuilder<TaskSummary>.Create(context, existingProjection)
            .GivenEvents("project", "proj-1", new TaskAdded("task-1", "Task"))
            .UpdateToLatest();

        // Assert
        builder.Then()
            .ShouldHaveProperty(p => p.TaskCount, 1);
    }

    /// <summary>
    /// Helper to wrap a domain event as a JsonEvent for IEvent[] parameters.
    /// </summary>
    private static JsonEvent WrapEvent(object domainEvent, int version)
    {
        var eventType = domainEvent.GetType();
        var eventNameAttribute = eventType.GetCustomAttributes(typeof(EventNameAttribute), false)
            .FirstOrDefault() as EventNameAttribute;

        return new JsonEvent
        {
            EventType = eventNameAttribute?.Name ?? eventType.Name,
            EventVersion = version,
            Payload = JsonSerializer.Serialize(domainEvent, eventType)
        };
    }

    #endregion

    #region Assertion Method Tests

    [Fact]
    public async Task Assertions_ShouldHaveState_WithPredicate()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1",
                new ProjectCreated("Test Project", "owner-1"),
                new TaskAdded("task-1", "First Task"),
                new TaskCompleted("task-1"))
            .UpdateToLatest();

        // Assert - Using ShouldHaveState with a predicate
        builder.Then()
            .ShouldHaveState(p =>
            {
                Assert.True(p.CompletedTasks <= p.TotalTasks, "Completed tasks should not exceed total tasks");
            });
    }

    [Fact]
    public async Task Assertions_ShouldHaveCheckpoint()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1", new ProjectCreated("Project", "owner"))
            .UpdateToLatest();

        // Assert
        builder.Then()
            .ShouldHaveCheckpoint("project", "proj-1", 0)
            .ShouldHaveNonEmptyCheckpoint();
    }

    [Fact]
    public async Task Assertions_ShouldHaveCheckpointCount()
    {
        // Arrange
        var context = CreateTestContext();

        // Act
        var builder = await ProjectionTestBuilder.For<ProjectStats>(context)
            .Given<Project>("proj-1", new ProjectCreated("Project A", "owner"))
            .Given<Project>("proj-2", new ProjectCreated("Project B", "owner"))
            .Given<Project>("proj-3", new ProjectCreated("Project C", "owner"))
            .UpdateToLatest();

        // Assert
        builder.Then()
            .ShouldHaveCheckpointCount(3);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void EdgeCase_EmptyEventStream()
    {
        // Arrange
        var context = CreateTestContext();

        // Act - No events given, just create the builder
        var builder = ProjectionTestBuilder.For<ProjectStats>(context);
        var projection = builder.Projection;

        // Assert
        Assert.Equal(0, projection.TotalProjects);
        Assert.Equal(0, projection.TotalTasks);
    }

    [Fact]
    public void EdgeCase_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ProjectionTestBuilder.For<ProjectStats>(null!));
    }

    [Fact]
    public void EdgeCase_ExplicitFactory_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ProjectionTestBuilder<TaskSummary>.Create(
                null!,
                (docFactory, streamFactory) => new TaskSummary(docFactory, streamFactory)));
    }

    [Fact]
    public void EdgeCase_ExplicitFactory_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateTestContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ProjectionTestBuilder<TaskSummary>.Create(
                context,
                (Func<IObjectDocumentFactory, IEventStreamFactory, TaskSummary>)null!));
    }

    [Fact]
    public void EdgeCase_ExplicitFactory_NullProjection_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateTestContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ProjectionTestBuilder<TaskSummary>.Create(context, (TaskSummary)null!));
    }

    #endregion

    #region Comparison Tests

    /// <summary>
    /// Shows both patterns side by side for the same projection type.
    /// When the projection implements ITestableProjection, prefer Pattern 1 for cleaner syntax.
    /// </summary>
    [Fact]
    public async Task Comparison_BothPatternsProduceSameResult()
    {
        var context = CreateTestContext();

        // Pattern 1: ITestableProjection (simpler)
        var builder1 = await ProjectionTestBuilder.For<ProjectStats>(context)
            .GivenEvents("project", "proj-1", new ProjectCreated("Same Project", "owner"))
            .UpdateToLatest();

        // Pattern 2: Explicit Factory (more verbose, but same projection type works too)
        var context2 = CreateTestContext();
        var builder2 = await ProjectionTestBuilder<ProjectStats>.Create(
            context2,
            (docFactory, streamFactory) => new ProjectStats(docFactory, streamFactory))
            .GivenEvents("project", "proj-1", new ProjectCreated("Same Project", "owner"))
            .UpdateToLatest();

        // Both should produce equivalent results
        Assert.Equal(builder1.Projection.TotalProjects, builder2.Projection.TotalProjects);
        Assert.Equal(builder1.Projection.ProjectNames[0], builder2.Projection.ProjectNames[0]);
    }

    #endregion
}
