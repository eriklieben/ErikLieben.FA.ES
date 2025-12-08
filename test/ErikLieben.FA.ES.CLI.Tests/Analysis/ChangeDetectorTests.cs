using ErikLieben.FA.ES.CLI.Abstractions;
using ErikLieben.FA.ES.CLI.Analysis;
using ErikLieben.FA.ES.CLI.Model;

namespace ErikLieben.FA.ES.CLI.Tests.Analysis;

public class ChangeDetectorTests
{
    private readonly ChangeDetector _sut = new();

    private static SolutionDefinition CreateEmptySolution(string name = "TestSolution") =>
        new() { SolutionName = name, Projects = [] };

    private static ProjectDefinition CreateEmptyProject(string name = "TestProject") =>
        new() { Name = name, FileLocation = "/test", Namespace = "Test" };

    private static AggregateDefinition CreateAggregate(
        string name,
        int eventCount = 0,
        int propertyCount = 0,
        List<CommandDefinition>? commands = null,
        List<ConstructorDefinition>? constructors = null,
        PostWhenDeclaration? postWhen = null,
        List<StreamActionDefinition>? streamActions = null,
        bool isPartial = false,
        bool hasFactoryPartial = false,
        bool hasRepositoryPartial = false,
        EventStreamTypeAttributeData? eventStreamType = null,
        EventStreamBlobSettingsAttributeData? blobSettings = null) =>
        new()
        {
            IdentifierName = name,
            ObjectName = name,
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = Enumerable.Range(0, eventCount)
                .Select(i => new EventDefinition
                {
                    EventName = $"Event{i}",
                    TypeName = $"Event{i}",
                    Namespace = "Test",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    SchemaVersion = 1,
                    Parameters = []
                }).ToList(),
            Properties = Enumerable.Range(0, propertyCount)
                .Select(i => new PropertyDefinition
                {
                    Name = $"Property{i}",
                    Type = "string",
                    Namespace = "System",
                    IsNullable = false,
                    GenericTypes = []
                }).ToList(),
            Commands = commands ?? [],
            Constructors = constructors ?? [],
            PostWhen = postWhen,
            StreamActions = streamActions ?? [],
            IsPartialClass = isPartial,
            HasUserDefinedFactoryPartial = hasFactoryPartial,
            HasUserDefinedRepositoryPartial = hasRepositoryPartial,
            EventStreamTypeAttribute = eventStreamType,
            EventStreamBlobSettingsAttribute = blobSettings
        };

    private static ProjectionDefinition CreateProjection(
        string name,
        int eventCount = 0,
        int propertyCount = 0,
        bool externalCheckpoint = false,
        bool hasPostWhenAll = false,
        BlobProjectionDefinition? blobProjection = null) =>
        new()
        {
            Name = name,
            Namespace = "Test",
            Events = Enumerable.Range(0, eventCount)
                .Select(i => new ProjectionEventDefinition
                {
                    EventName = $"Event{i}",
                    TypeName = $"Event{i}",
                    Namespace = "Test",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Parameters = []
                }).ToList(),
            Properties = Enumerable.Range(0, propertyCount)
                .Select(i => new PropertyDefinition
                {
                    Name = $"Property{i}",
                    Type = "string",
                    Namespace = "System",
                    IsNullable = false,
                    GenericTypes = []
                }).ToList(),
            ExternalCheckpoint = externalCheckpoint,
            HasPostWhenAllMethod = hasPostWhenAll,
            BlobProjection = blobProjection,
            Constructors = []
        };

    public class InitialAnalysis : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_aggregates_when_previous_is_null()
        {
            // Arrange
            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.Aggregates.Add(CreateAggregate("TestAggregate", eventCount: 2, propertyCount: 3));
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(null, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Added, changes[0].Type);
            Assert.Equal(ChangeCategory.Aggregate, changes[0].Category);
            Assert.Contains("TestAggregate", changes[0].Description);
            Assert.Contains("2 events", changes[0].Details);
        }

        [Fact]
        public void Should_detect_projections_when_previous_is_null()
        {
            // Arrange
            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.Projections.Add(CreateProjection("TestProjection", eventCount: 3));
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(null, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Added, changes[0].Type);
            Assert.Equal(ChangeCategory.Projection, changes[0].Category);
            Assert.Contains("TestProjection", changes[0].Description);
        }

        [Fact]
        public void Should_detect_routed_projection_type()
        {
            // Arrange
            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.Projections.Add(new RoutedProjectionDefinition
            {
                Name = "RoutedProj",
                Namespace = "Test",
                Events = [],
                Properties = [],
                Constructors = [],
                IsRoutedProjection = true
            });
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(null, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("routed projection", changes[0].Description);
        }

        [Fact]
        public void Should_detect_inherited_aggregates_when_previous_is_null()
        {
            // Arrange
            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.InheritedAggregates.Add(new InheritedAggregateDefinition
            {
                IdentifierName = "ChildAggregate",
                InheritedIdentifierName = "ParentAggregate",
                ObjectName = "ChildAggregate",
                IdentifierType = "Guid",
                IdentifierTypeNamespace = "System",
                Namespace = "Test",
                InheritedNamespace = "Test",
                ParentInterface = "IParent",
                ParentInterfaceNamespace = "Test"
            });
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(null, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.InheritedAggregate, changes[0].Category);
            Assert.Contains("ChildAggregate", changes[0].Description);
        }

        [Fact]
        public void Should_detect_version_tokens_when_previous_is_null()
        {
            // Arrange
            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.VersionTokens.Add(new VersionTokenDefinition
            {
                Name = "MyToken",
                GenericType = "int",
                Namespace = "Test",
                NamespaceOfType = "System",
                FileLocations = ["/test.cs"]
            });
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(null, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.VersionToken, changes[0].Category);
            Assert.Contains("MyToken", changes[0].Description);
            Assert.Contains("int", changes[0].Details);
        }
    }

    public class AggregateChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_aggregate()
        {
            // Arrange
            var previous = CreateEmptySolution();
            previous.Projects.Add(CreateEmptyProject());

            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.Aggregates.Add(CreateAggregate("NewAggregate"));
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Added, changes[0].Type);
            Assert.Equal(ChangeCategory.Aggregate, changes[0].Category);
            Assert.Contains("Added aggregate NewAggregate", changes[0].Description);
        }

        [Fact]
        public void Should_detect_removed_aggregate()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("OldAggregate"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            current.Projects.Add(CreateEmptyProject());

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Removed, changes[0].Type);
            Assert.Equal(ChangeCategory.Aggregate, changes[0].Category);
            Assert.Contains("Removed aggregate OldAggregate", changes[0].Description);
        }

        [Fact]
        public void Should_detect_partial_class_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", isPartial: false));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", isPartial: true));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Modified, changes[0].Type);
            Assert.Contains("is now partial", changes[0].Description);
        }

        [Fact]
        public void Should_detect_factory_partial_added()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", hasFactoryPartial: false));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", hasFactoryPartial: true));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Custom factory partial added", changes[0].Description);
        }

        [Fact]
        public void Should_detect_repository_partial_added()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", hasRepositoryPartial: false));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", hasRepositoryPartial: true));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Custom repository partial added", changes[0].Description);
        }
    }

    public class EventChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_event()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", eventCount: 1));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var aggregate = CreateAggregate("TestAgg", eventCount: 1);
            aggregate.Events.Add(new EventDefinition
            {
                EventName = "NewEvent",
                TypeName = "NewEvent",
                Namespace = "Test",
                ActivationType = "Command",
                ActivationAwaitRequired = false,
                SchemaVersion = 1,
                Parameters = []
            });
            currProject.Aggregates.Add(aggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Added, changes[0].Type);
            Assert.Equal(ChangeCategory.Event, changes[0].Category);
            Assert.Contains("Added NewEvent event", changes[0].Description);
            Assert.Contains("command", changes[0].Details);
        }

        [Fact]
        public void Should_detect_removed_event()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            var prevAggregate = CreateAggregate("TestAgg");
            prevAggregate.Events.Add(new EventDefinition
            {
                EventName = "OldEvent",
                TypeName = "OldEvent",
                Namespace = "Test",
                ActivationType = "When",
                ActivationAwaitRequired = false,
                SchemaVersion = 1,
                Parameters = []
            });
            prevProject.Aggregates.Add(prevAggregate);
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg"));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Removed, changes[0].Type);
            Assert.Contains("Removed OldEvent event", changes[0].Description);
        }

        [Fact]
        public void Should_detect_activation_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            var prevAggregate = CreateAggregate("TestAgg");
            prevAggregate.Events.Add(new EventDefinition
            {
                EventName = "TestEvent",
                TypeName = "TestEvent",
                Namespace = "Test",
                ActivationType = "When",
                ActivationAwaitRequired = false,
                SchemaVersion = 1,
                Parameters = []
            });
            prevProject.Aggregates.Add(prevAggregate);
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var currAggregate = CreateAggregate("TestAgg");
            currAggregate.Events.Add(new EventDefinition
            {
                EventName = "TestEvent",
                TypeName = "TestEvent",
                Namespace = "Test",
                ActivationType = "Command",
                ActivationAwaitRequired = false,
                SchemaVersion = 1,
                Parameters = []
            });
            currProject.Aggregates.Add(currAggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.WhenMethod, changes[0].Category);
            Assert.Contains("When → Command", changes[0].Details);
        }

        [Fact]
        public void Should_detect_async_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            var prevAggregate = CreateAggregate("TestAgg");
            prevAggregate.Events.Add(new EventDefinition
            {
                EventName = "TestEvent",
                TypeName = "TestEvent",
                Namespace = "Test",
                ActivationType = "When",
                ActivationAwaitRequired = false,
                SchemaVersion = 1,
                Parameters = []
            });
            prevProject.Aggregates.Add(prevAggregate);
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var currAggregate = CreateAggregate("TestAgg");
            currAggregate.Events.Add(new EventDefinition
            {
                EventName = "TestEvent",
                TypeName = "TestEvent",
                Namespace = "Test",
                ActivationType = "When",
                ActivationAwaitRequired = true,
                SchemaVersion = 1,
                Parameters = []
            });
            currProject.Aggregates.Add(currAggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("now async", changes[0].Description);
        }
    }

    public class PropertyChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_property()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", propertyCount: 1));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var aggregate = CreateAggregate("TestAgg", propertyCount: 1);
            aggregate.Properties.Add(new PropertyDefinition
            {
                Name = "NewProperty",
                Type = "int",
                Namespace = "System",
                IsNullable = true,
                GenericTypes = []
            });
            currProject.Aggregates.Add(aggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.Property, changes[0].Category);
            Assert.Contains("Added property NewProperty", changes[0].Description);
            Assert.Contains("int?", changes[0].Details);
        }

        [Fact]
        public void Should_detect_property_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            var prevAggregate = CreateAggregate("TestAgg");
            prevAggregate.Properties.Add(new PropertyDefinition
            {
                Name = "TestProp",
                Type = "string",
                Namespace = "System",
                IsNullable = false,
                GenericTypes = []
            });
            prevProject.Aggregates.Add(prevAggregate);
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var currAggregate = CreateAggregate("TestAgg");
            currAggregate.Properties.Add(new PropertyDefinition
            {
                Name = "TestProp",
                Type = "int",
                Namespace = "System",
                IsNullable = true,
                GenericTypes = []
            });
            currProject.Aggregates.Add(currAggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("string → int?", changes[0].Details);
        }

        [Fact]
        public void Should_detect_removed_property()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            var prevAggregate = CreateAggregate("TestAgg");
            prevAggregate.Properties.Add(new PropertyDefinition
            {
                Name = "OldProp",
                Type = "string",
                Namespace = "System",
                IsNullable = false,
                GenericTypes = []
            });
            prevProject.Aggregates.Add(prevAggregate);
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg"));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Removed property OldProp", changes[0].Description);
        }
    }

    public class CommandChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_command()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            var aggregate = CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]);
            currProject.Aggregates.Add(aggregate);
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.Command, changes[0].Category);
            Assert.Contains("Added command DoSomething", changes[0].Description);
        }

        [Fact]
        public void Should_detect_command_async_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = true,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("is now async", changes[0].Description);
        }

        [Fact]
        public void Should_detect_command_return_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "bool", Namespace = "System" }
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed return type", changes[0].Description);
            Assert.Contains("void → bool", changes[0].Details);
        }

        [Fact]
        public void Should_detect_command_produces_new_event()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents = [],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", commands:
            [
                new CommandDefinition
                {
                    CommandName = "DoSomething",
                    RequiresAwait = false,
                    Parameters = [],
                    ProducesEvents =
                    [
                        new CommandEventDefinition { TypeName = "SomethingDone", Namespace = "Test", File = "/test.cs", EventName = "SomethingDone" }
                    ],
                    ReturnType = new CommandReturnType { Type = "void", Namespace = "System" }
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("now produces SomethingDone", changes[0].Description);
        }
    }

    public class ConstructorChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_constructor()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", constructors:
            [
                new ConstructorDefinition
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "id", Type = "Guid", Namespace = "System", IsNullable = false }
                    ]
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.Constructor, changes[0].Category);
            Assert.Contains("Added constructor", changes[0].Description);
            Assert.Contains("1 parameters", changes[0].Details);
        }

        [Fact]
        public void Should_detect_removed_constructor()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", constructors:
            [
                new ConstructorDefinition
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "id", Type = "Guid", Namespace = "System", IsNullable = false }
                    ]
                }
            ]));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg"));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Removed constructor", changes[0].Description);
        }
    }

    public class PostWhenChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_postwhen()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", postWhen: new PostWhenDeclaration
            {
                Parameters =
                [
                    new PostWhenParameterDeclaration { Name = "service", Type = "IService", Namespace = "Test" }
                ]
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.PostWhen, changes[0].Category);
            Assert.Contains("Added PostWhen handler", changes[0].Description);
            Assert.Contains("1 parameters", changes[0].Details);
        }

        [Fact]
        public void Should_detect_removed_postwhen()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", postWhen: new PostWhenDeclaration
            {
                Parameters = []
            }));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg"));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Removed PostWhen handler", changes[0].Description);
        }
    }

    public class StreamActionChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_stream_action()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", streamActions:
            [
                new StreamActionDefinition
                {
                    Type = "CustomAction",
                    Namespace = "Test",
                    StreamActionInterfaces = ["IStreamAction"],
                    RegistrationType = "Auto"
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.StreamAction, changes[0].Category);
            Assert.Contains("Added stream action CustomAction", changes[0].Description);
        }

        [Fact]
        public void Should_detect_stream_action_registration_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", streamActions:
            [
                new StreamActionDefinition
                {
                    Type = "CustomAction",
                    Namespace = "Test",
                    StreamActionInterfaces = ["IStreamAction"],
                    RegistrationType = "Auto"
                }
            ]));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", streamActions:
            [
                new StreamActionDefinition
                {
                    Type = "CustomAction",
                    Namespace = "Test",
                    StreamActionInterfaces = ["IStreamAction"],
                    RegistrationType = "Manual"
                }
            ]));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed registration type", changes[0].Description);
            Assert.Contains("Auto → Manual", changes[0].Details);
        }
    }

    public class EventStreamTypeChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_event_stream_type_attribute()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", eventStreamType: new EventStreamTypeAttributeData
            {
                StreamType = "blob"
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.EventStreamType, changes[0].Category);
            Assert.Contains("Added [EventStreamType]", changes[0].Description);
        }

        [Fact]
        public void Should_detect_stream_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", eventStreamType: new EventStreamTypeAttributeData
            {
                StreamType = "blob"
            }));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", eventStreamType: new EventStreamTypeAttributeData
            {
                StreamType = "table"
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed StreamType", changes[0].Description);
            Assert.Contains("blob → table", changes[0].Details);
        }
    }

    public class BlobSettingsChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_blob_settings()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg"));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", blobSettings: new EventStreamBlobSettingsAttributeData
            {
                DataStore = "mystore"
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.BlobSettings, changes[0].Category);
            Assert.Contains("Added [EventStreamBlobSettings]", changes[0].Description);
            Assert.Contains("DataStore=mystore", changes[0].Details);
        }

        [Fact]
        public void Should_detect_data_store_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", blobSettings: new EventStreamBlobSettingsAttributeData
            {
                DataStore = "store1"
            }));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", blobSettings: new EventStreamBlobSettingsAttributeData
            {
                DataStore = "store2"
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed DataStore", changes[0].Description);
        }
    }

    public class ProjectionChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_projection()
        {
            // Arrange
            var previous = CreateEmptySolution();
            previous.Projects.Add(CreateEmptyProject());

            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.Projections.Add(CreateProjection("NewProjection", eventCount: 2));
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeType.Added, changes[0].Type);
            Assert.Equal(ChangeCategory.Projection, changes[0].Category);
        }

        [Fact]
        public void Should_detect_external_checkpoint_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Projections.Add(CreateProjection("TestProj", externalCheckpoint: false));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Projections.Add(CreateProjection("TestProj", externalCheckpoint: true));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Enabled external checkpoint", changes[0].Description);
        }

        [Fact]
        public void Should_detect_blob_projection_container_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Projections.Add(CreateProjection("TestProj", blobProjection: new BlobProjectionDefinition
            {
                Container = "container1",
                Connection = "conn"
            }));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Projections.Add(CreateProjection("TestProj", blobProjection: new BlobProjectionDefinition
            {
                Container = "container2",
                Connection = "conn"
            }));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed blob container", changes[0].Description);
            Assert.Contains("container1 → container2", changes[0].Details);
        }
    }

    public class VersionTokenChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_version_token()
        {
            // Arrange
            var previous = CreateEmptySolution();
            previous.Projects.Add(CreateEmptyProject());

            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.VersionTokens.Add(new VersionTokenDefinition
            {
                Name = "NewToken",
                GenericType = "long",
                Namespace = "Test",
                NamespaceOfType = "System",
                FileLocations = ["/test.cs"]
            });
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.VersionToken, changes[0].Category);
            Assert.Contains("Added version token NewToken", changes[0].Description);
        }

        [Fact]
        public void Should_detect_version_token_generic_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.VersionTokens.Add(new VersionTokenDefinition
            {
                Name = "TestToken",
                GenericType = "int",
                Namespace = "Test",
                NamespaceOfType = "System",
                FileLocations = ["/test.cs"]
            });
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.VersionTokens.Add(new VersionTokenDefinition
            {
                Name = "TestToken",
                GenericType = "long",
                Namespace = "Test",
                NamespaceOfType = "System",
                FileLocations = ["/test.cs"]
            });
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed generic type", changes[0].Description);
            Assert.Contains("int → long", changes[0].Details);
        }

        [Fact]
        public void Should_detect_removed_version_token()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.VersionTokens.Add(new VersionTokenDefinition
            {
                Name = "OldToken",
                GenericType = "int",
                Namespace = "Test",
                NamespaceOfType = "System",
                FileLocations = ["/test.cs"]
            });
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            current.Projects.Add(CreateEmptyProject());

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Removed version token OldToken", changes[0].Description);
        }
    }

    public class InheritedAggregateChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_detect_added_inherited_aggregate()
        {
            // Arrange
            var previous = CreateEmptySolution();
            previous.Projects.Add(CreateEmptyProject());

            var current = CreateEmptySolution();
            var project = CreateEmptyProject();
            project.InheritedAggregates.Add(new InheritedAggregateDefinition
            {
                IdentifierName = "Child",
                InheritedIdentifierName = "Parent",
                ObjectName = "Child",
                IdentifierType = "Guid",
                IdentifierTypeNamespace = "System",
                Namespace = "Test",
                InheritedNamespace = "Test",
                ParentInterface = "IParent",
                ParentInterfaceNamespace = "Test"
            });
            current.Projects.Add(project);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Equal(ChangeCategory.InheritedAggregate, changes[0].Category);
            Assert.Contains("Added inherited aggregate Child", changes[0].Description);
            Assert.Contains("inherits from Parent", changes[0].Details);
        }

        [Fact]
        public void Should_detect_base_type_change()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.InheritedAggregates.Add(new InheritedAggregateDefinition
            {
                IdentifierName = "Child",
                InheritedIdentifierName = "Parent1",
                ObjectName = "Child",
                IdentifierType = "Guid",
                IdentifierTypeNamespace = "System",
                Namespace = "Test",
                InheritedNamespace = "Test",
                ParentInterface = "IParent",
                ParentInterfaceNamespace = "Test"
            });
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.InheritedAggregates.Add(new InheritedAggregateDefinition
            {
                IdentifierName = "Child",
                InheritedIdentifierName = "Parent2",
                ObjectName = "Child",
                IdentifierType = "Guid",
                IdentifierTypeNamespace = "System",
                Namespace = "Test",
                InheritedNamespace = "Test",
                ParentInterface = "IParent",
                ParentInterfaceNamespace = "Test"
            });
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Single(changes);
            Assert.Contains("Changed base type", changes[0].Description);
            Assert.Contains("Parent1 → Parent2", changes[0].Details);
        }
    }

    public class NoChanges : ChangeDetectorTests
    {
        [Fact]
        public void Should_return_empty_when_no_changes()
        {
            // Arrange
            var previous = CreateEmptySolution();
            var prevProject = CreateEmptyProject();
            prevProject.Aggregates.Add(CreateAggregate("TestAgg", eventCount: 2, propertyCount: 1));
            previous.Projects.Add(prevProject);

            var current = CreateEmptySolution();
            var currProject = CreateEmptyProject();
            currProject.Aggregates.Add(CreateAggregate("TestAgg", eventCount: 2, propertyCount: 1));
            current.Projects.Add(currProject);

            // Act
            var changes = _sut.DetectChanges(previous, current);

            // Assert
            Assert.Empty(changes);
        }
    }
}
