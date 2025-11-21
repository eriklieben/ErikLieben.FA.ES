using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateAggregateCodeTests
{
    private static (SolutionDefinition solution, string outDir) BuildSolution(ProjectDefinition project)
    {
        var solution = new SolutionDefinition
        {
            SolutionName = "Demo",
            Generator = new GeneratorInformation { Version = "1.0.0-test" },
            Projects = new List<ProjectDefinition> { project }
        };

        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(outDir);
        return (solution, outDir);
    }

    [Fact]
    public async Task Generate_writes_partial_class_interfaces_factory_and_setup_and_fold_and_json()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Account",
            ObjectName = "Account",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                        new ConstructorParameter { Name = "svc", Type = "IService", Namespace = "Demo.App.Services", IsNullable = false }
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Name", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>
            {
                new()
                {
                    TypeName = "UserCreated",
                    Namespace = "Demo.App.Events",
                    EventName = "User.Created",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Properties = new List<PropertyDefinition>
                    {
                        // Include a subtype to ensure JsonSerializable lines for subtypes exist (though Guid appears due to HACK)
                        new PropertyDefinition
                        {
                            Name = "CustomerId",
                            Type = "CustomerId",
                            Namespace = "Demo.App.Shared",
                            IsNullable = false,
                            SubTypes =
                            [
                                new PropertyGenericTypeDefinition(
                                    Name: "Guid",
                                    Namespace: "System",
                                    GenericTypes: new List<PropertyGenericTypeDefinition>(),
                                    SubTypes: new List<PropertyGenericTypeDefinition>())
                            ]
                        }
                    },
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "e", Type = "UserCreated", Namespace = "Demo.App.Events" },
                        new() { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" }
                    }
                },
                new()
                {
                    TypeName = "FeatureFlagEnabled",
                    Namespace = "Demo.App.Events",
                    EventName = "FeatureFlag.Enabled",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Properties = new List<PropertyDefinition>(),
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "e", Type = "FeatureFlagEnabled", Namespace = "Demo.App.Events" }
                    }
                }
            },
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "document", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" },
                    new PostWhenParameterDeclaration { Name = "evt", Type = "IEvent", Namespace = "ErikLieben.FA.ES" },
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Account.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        // Generator does not create directories -> pre-create
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Account.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Pragma warning to suppress unnecessary using directives
        Assert.Contains("#pragma warning disable IDE0005", code);

        // Class, interfaces
        Assert.Contains("namespace Demo.App.Domain;", code);
        Assert.Contains("public partial class Account : Aggregate, IBase, IAccount", code);

        // Fold switch cases and When invocations
        Assert.Contains("switch (@event.EventType)", code);
        Assert.Contains("case \"User.Created\":", code);
        Assert.Contains("When(JsonEvent.To(@event, UserCreatedJsonSerializerContext.Default.UserCreated),", code);
        Assert.Contains("Stream.Document", code); // extra parameter mapping
        Assert.Contains("case \"FeatureFlag.Enabled\":", code);
        Assert.Contains("When(JsonEvent.To(@event, FeatureFlagEnabledJsonSerializerContext.Default.FeatureFlagEnabled));", code);

        // PostWhen call after switch
        Assert.Contains("PostWhen(Stream.Document, @event);", code);

        // GeneratedSetup registration and context settings
        Assert.Contains("Stream.RegisterEvent<UserCreated>(", code);
        Assert.Contains("\"User.Created\"", code);
        Assert.Contains("UserCreatedJsonSerializerContext.Default.UserCreated", code);
        Assert.Contains("Stream.SetSnapShotType(AccountJsonSerializerContext.Default.AccountSnapshot);", code);
        Assert.Contains("Stream.SetAggregateType(AccountJsonSerializerContext.Default.Account);", code);

        // Interfaces and snapshot record
        Assert.Contains("public interface IAccount", code);
        Assert.Contains("public record AccountSnapshot : IAccount", code);
        Assert.Contains("public required String Name { get; init; }", code);

        // JsonSerializable attributes and context
        Assert.Contains("[JsonSerializable(typeof(AccountSnapshot))]", code);
        Assert.Contains("[JsonSerializable(typeof(Account))]", code);
        Assert.Contains("internal partial class AccountJsonSerializerContext : JsonSerializerContext", code);

        // Factory interface and class
        Assert.Contains("public partial interface IAccountFactory : IAggregateFactory<Account, Guid>", code);
        Assert.Contains("public partial class AccountFactory : IAccountFactory", code);
        Assert.Contains("public static string ObjectName => \"Account\";", code);
        Assert.Contains("public string GetObjectName()", code);

        // DI resolution and constructor invocation
        Assert.Contains("var svc = serviceProvider.GetService(typeof(IService)) as IService;", code);
        Assert.Contains("return new Account(eventStream, svc!);", code);

        // Async factory helpers, including generic CreateAsync<T> with ActionMetadata parameter
        Assert.Contains("public async Task<Account> CreateAsync(Guid id)", code);
        Assert.Contains("protected async Task<Account> CreateAsync<T>(Guid id, T firstEvent, ActionMetadata? metadata = null) where T : class", code);
        Assert.Contains("public async Task<(Account, IObjectDocument)> GetWithDocumentAsync(Guid id)", code);
    }

    [Fact]
    public async Task Generate_skips_when_not_partial()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Temp",
            ObjectName = "Temp",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = false,
            FileLocations = new List<string> { "Demo\\Domain\\Temp.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Temp.Generated.cs");
        Assert.False(File.Exists(generatedPath));
    }

    // Unit tests for helper methods
    [Fact]
    public void BuildUsings_returns_default_usings_and_property_namespaces()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Value", Type = "CustomType", Namespace = "Custom.Namespace", IsNullable = false },
                new() { Name = "Count", Type = "Int32", Namespace = "System", IsNullable = false }
            }
        };

        // Act
        var usings = GenerateAggregateCode.BuildUsings(aggregate);

        // Assert
        Assert.Contains("System.Collections.Generic", usings);
        Assert.Contains("System.Text.Json.Serialization", usings);
        Assert.Contains("System.Threading", usings);
        Assert.Contains("System.Threading.Tasks", usings);
        Assert.Contains("ErikLieben.FA.ES", usings);
        Assert.Contains("ErikLieben.FA.ES.Processors", usings);
        Assert.Contains("ErikLieben.FA.ES.Aggregates", usings);
        Assert.Contains("ErikLieben.FA.ES.Documents", usings);
        Assert.Contains("System.Diagnostics.CodeAnalysis", usings);
        Assert.Contains("Custom.Namespace", usings);
        Assert.Contains("System", usings);
    }

    [Fact]
    public void BuildUsings_avoids_duplicate_namespaces()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Value1", Type = "String", Namespace = "System", IsNullable = false },
                new() { Name = "Value2", Type = "Int32", Namespace = "System", IsNullable = false }
            }
        };

        // Act
        var usings = GenerateAggregateCode.BuildUsings(aggregate);

        // Assert
        var systemCount = usings.Count(u => u == "System");
        Assert.Equal(1, systemCount);
    }

    [Fact]
    public void GeneratePostWhenCode_returns_empty_when_no_postwhen()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            PostWhen = null
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GeneratePostWhenCode(aggregate, usings);

        // Assert
        Assert.Equal(string.Empty, result.ToString());
    }

    [Fact]
    public void GeneratePostWhenCode_generates_call_with_IObjectDocument_parameter()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" }
                }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GeneratePostWhenCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("PostWhen(Stream.Document);", code);
        Assert.Contains("ErikLieben.FA.ES.Documents", usings);
    }

    [Fact]
    public void GeneratePostWhenCode_generates_call_with_IEvent_parameter()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "evt", Type = "IEvent", Namespace = "ErikLieben.FA.ES" }
                }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GeneratePostWhenCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("PostWhen(@event);", code);
    }

    [Fact]
    public void GeneratePostWhenCode_generates_call_with_multiple_parameters()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" },
                    new PostWhenParameterDeclaration { Name = "evt", Type = "IEvent", Namespace = "ErikLieben.FA.ES" }
                }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GeneratePostWhenCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("PostWhen(Stream.Document, @event);", code);
    }

    // Simplified tests - removing granular ones to focus on key coverage
    [Fact]
    public void GenerateFoldCode_generates_case_for_when_events()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>
            {
                new()
                {
                    TypeName = "UserCreated",
                    Namespace = "Test.Events",
                    EventName = "User.Created",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    File = "",
                    Parameters = new List<ParameterDefinition> { new() { Name = "e", Type = "UserCreated", Namespace = "Test.Events" } }
                }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GenerateFoldCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("case \"User.Created\":", code);
        Assert.Contains("UserCreated", code);
        Assert.Contains("Test.Events", usings);
    }

    [Fact]
    public void GenerateJsonSerializableCode_generates_attributes_for_events()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "TestAggregate",
            ObjectName = "TestAggregate",
            IdentifierType = "string",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>
            {
                new() { TypeName = "Event1", Namespace = "Test.Events", EventName = "Event.One", ActivationType = "When", ActivationAwaitRequired = false, File = "" },
                new() { TypeName = "Event2", Namespace = "Test.Events", EventName = "Event.Two", ActivationType = "When", ActivationAwaitRequired = false, File = "" }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GenerateJsonSerializableCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("[JsonSerializable(typeof(Event1))]", code);
        Assert.Contains("[JsonSerializable(typeof(Event2))]", code);
        Assert.Contains("[JsonSerializable(typeof(TestAggregateSnapshot))]", code);
        Assert.Contains("[JsonSerializable(typeof(TestAggregate))]", code);
    }

    [Fact]
    public void GenerateJsonSerializableCode_includes_identifier_type_when_not_string()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "TestAggregate",
            ObjectName = "TestAggregate",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>()
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GenerateJsonSerializableCode(aggregate, usings);

        // Assert
        var code = result.ToString();
        Assert.Contains("[JsonSerializable(typeof(Guid))]", code);
        Assert.Contains("System", usings);
    }

    // Keeping only essential tests for property and constructor code
    [Fact]
    public void BuildPropertyType_builds_generic_type_correctly()
    {
        // Arrange
        var property = new PropertyDefinition
        {
            Name = "Items",
            Type = "Dictionary",
            Namespace = "System.Collections.Generic",
            IsNullable = false,
            GenericTypes = new List<PropertyGenericTypeDefinition>
            {
                new(Name: "String", Namespace: "System", GenericTypes: new List<PropertyGenericTypeDefinition>(), SubTypes: new List<PropertyGenericTypeDefinition>()),
                new(Name: "Int32", Namespace: "System", GenericTypes: new List<PropertyGenericTypeDefinition>(), SubTypes: new List<PropertyGenericTypeDefinition>())
            }
        };

        // Act
        var result = GenerateAggregateCode.BuildPropertyType(property);

        // Assert
        Assert.Equal("Dictionary<System.String,System.Int32>", result);
    }

    [Fact]
    public void GenerateConstructorParameters_extracts_service_parameters()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters = new List<ConstructorParameter>
                    {
                        new() { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                        new() { Name = "logger", Type = "ILogger", Namespace = "Microsoft.Extensions.Logging", IsNullable = false }
                    }
                }
            }
        };

        // Act
        var (get, ctorInput) = GenerateAggregateCode.GenerateConstructorParameters(aggregate);

        // Assert
        Assert.Contains("var logger = serviceProvider.GetService(typeof(ILogger)) as ILogger;", get);
        Assert.Contains(", logger!", ctorInput);
        Assert.DoesNotContain("eventStream", get);
    }

    [Fact]
    public void GenerateSetupCode_registers_all_events()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "TestAggregate",
            ObjectName = "TestAggregate",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>
            {
                new() { TypeName = "Event1", EventName = "Event.One", Namespace = "Test.Events", ActivationType = "When", ActivationAwaitRequired = false, File = "" },
                new() { TypeName = "Event2", EventName = "Event.Two", Namespace = "Test.Events", ActivationType = "When", ActivationAwaitRequired = false, File = "" }
            }
        };

        // Act
        var result = GenerateAggregateCode.GenerateSetupCode(aggregate);

        // Assert
        var code = result.ToString();
        Assert.Contains("Stream.RegisterEvent<Event1>", code);
        Assert.Contains("\"Event.One\"", code);
        Assert.Contains("Event1JsonSerializerContext.Default.Event1", code);
        Assert.Contains("Stream.RegisterEvent<Event2>", code);
        Assert.Contains("\"Event.Two\"", code);
    }

    [Fact]
    public void GenerateSetupCode_sets_snapshot_and_aggregate_types()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "MyAggregate",
            ObjectName = "MyAggregate",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>()
        };

        // Act
        var result = GenerateAggregateCode.GenerateSetupCode(aggregate);

        // Assert
        var code = result.ToString();
        Assert.Contains("Stream.SetSnapShotType(MyAggregateJsonSerializerContext.Default.MyAggregateSnapshot);", code);
        Assert.Contains("Stream.SetAggregateType(MyAggregateJsonSerializerContext.Default.MyAggregate);", code);
    }

    [Fact]
    public void AssembleAggregateCode_includes_all_components()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "TestAggregate",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test.Domain",
            ObjectName = "TestObject"
        };
        var usings = new List<string> { "System", "ErikLieben.FA.ES" };
        var postWhenCode = new System.Text.StringBuilder("PostWhen();");
        var foldCode = new System.Text.StringBuilder("case \"test\": break;");
        var serializableCode = new System.Text.StringBuilder("[JsonSerializable(typeof(TestAggregate))]");
        var propertyCode = new System.Text.StringBuilder("public String Name { get; }");
        var propertySnapshotCode = new System.Text.StringBuilder("public required String Name { get; init; }");
        var get = "var svc = serviceProvider.GetService(typeof(IService)) as IService;\n";
        var ctorInput = ", svc!";
        var setupCode = new System.Text.StringBuilder("Stream.RegisterEvent<TestEvent>(\"Test.Event\", TestEventJsonSerializerContext.Default.TestEvent);");

        // Act
        var result = GenerateAggregateCode.AssembleAggregateCode(
            aggregate, usings, postWhenCode, foldCode, serializableCode,
            propertyCode, propertySnapshotCode, get, ctorInput, setupCode);

        // Assert
        var code = result.ToString();
        Assert.Contains("using System;", code);
        Assert.Contains("using ErikLieben.FA.ES;", code);
        Assert.Contains("namespace Test.Domain;", code);
        Assert.Contains("public partial class TestAggregate : Aggregate, IBase, ITestAggregate", code);
        Assert.Contains("PostWhen();", code);
        Assert.Contains("case \"test\": break;", code);
        Assert.Contains("public interface ITestAggregate", code);
        Assert.Contains("public record TestAggregateSnapshot : ITestAggregate", code);
        Assert.Contains("[JsonSerializable(typeof(TestAggregate))]", code);
        Assert.Contains("public partial class TestAggregateFactory : ITestAggregateFactory", code);
        Assert.Contains("public static string ObjectName => \"TestObject\";", code);
        Assert.Contains("var svc = serviceProvider.GetService(typeof(IService)) as IService;", code);
        Assert.Contains("return new TestAggregate(eventStream, svc!);", code);
    }

    [Fact]
    public async Task Generate_includes_repository_interface()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Product",
            ObjectName = "product",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Product.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Product.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository interface
        Assert.Contains("public partial interface IProductRepository", code);
        Assert.Contains("Task<PagedResult<string>> GetObjectIdsAsync(", code);
        Assert.Contains("string? continuationToken", code);
        Assert.Contains("int pageSize", code);
        Assert.Contains("Task<Product?> GetByIdAsync(", code);
        Assert.Contains("Task<bool> ExistsAsync(", code);
        Assert.Contains("Task<long> CountAsync(", code);
    }

    [Fact]
    public async Task Generate_includes_repository_implementation()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Order",
            ObjectName = "order",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Order.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Order.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository class
        Assert.Contains("public partial class OrderRepository : IOrderRepository", code);
        Assert.Contains("private readonly IOrderFactory orderFactory;", code);
        Assert.Contains("private readonly IObjectDocumentFactory objectDocumentFactory;", code);
        Assert.Contains("private readonly IObjectIdProvider objectIdProvider;", code);

        // Repository constructor
        Assert.Contains("public OrderRepository(", code);
        Assert.Contains("IOrderFactory orderFactory,", code);
        Assert.Contains("IObjectDocumentFactory objectDocumentFactory,", code);
        Assert.Contains("IObjectIdProvider objectIdProvider)", code);

        // GetObjectIdsAsync implementation
        Assert.Contains("public async Task<PagedResult<string>> GetObjectIdsAsync(", code);
        Assert.Contains("ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);", code);
        Assert.Contains("ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);", code);
        Assert.Contains("return await objectIdProvider.GetObjectIdsAsync(", code);
        Assert.Contains("ObjectName,", code);

        // GetByIdAsync implementation
        Assert.Contains("public async Task<Order?> GetByIdAsync(", code);
        Assert.Contains("var obj = orderFactory.Create(document);", code);
        Assert.Contains("await obj.Fold();", code);
        Assert.Contains("return obj;", code);

        // ExistsAsync implementation
        Assert.Contains("public async Task<bool> ExistsAsync(", code);
        Assert.Contains("objectIdProvider.ExistsAsync", code);

        // CountAsync implementation
        Assert.Contains("public async Task<long> CountAsync(", code);
        Assert.Contains("objectIdProvider.CountAsync", code);
    }

    [Fact]
    public async Task Generate_adds_obsolete_attributes_to_factory_query_methods()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Customer",
            ObjectName = "customer",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Customer.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Customer.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // GetAsync with Obsolete
        Assert.Contains("[Obsolete", code);
        Assert.Contains("Use ICustomerRepository.GetByIdAsync instead", code);
        Assert.Contains("GetAsync", code);

        // GetWithDocumentAsync with Obsolete
        Assert.Contains("GetWithDocumentAsync", code);

        // GetFirstByDocumentTag with Obsolete
        Assert.Contains("GetFirstByDocumentTag", code);

        // GetAllByDocumentTag with Obsolete
        Assert.Contains("GetAllByDocumentTag", code);
    }

    [Fact]
    public async Task Generate_repository_validates_page_size_bounds()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Invoice",
            ObjectName = "invoice",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Invoice.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Invoice.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Page size validation
        Assert.Contains("ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);", code);
        Assert.Contains("ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);", code);
    }

    [Fact]
    public async Task Generate_factory_GetAsync_includes_upToVersion_parameter()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "BlogPost",
            ObjectName = "blogpost",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\BlogPost.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "BlogPost.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory GetAsync should have upToVersion parameter
        Assert.Contains("public async Task<BlogPost> GetAsync(Guid id, int? upToVersion = null)", code);
    }

    [Fact]
    public async Task Generate_factory_GetAsync_uses_ReadAsync_with_upToVersion()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Article",
            ObjectName = "article",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Article.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Article.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory GetAsync should use ReadAsync with upToVersion
        Assert.Contains("var events = await eventStream.ReadAsync(0, upToVersion);", code);

        // Should manually fold each event
        Assert.Contains("foreach (var e in events)", code);
        Assert.Contains("obj.Fold(e);", code);

        // Comment should indicate this creates event stream and folds events
        Assert.Contains("// Create event stream", code);
        Assert.Contains("// Read events up to version WITH upcasting applied", code);
        Assert.Contains("// Fold events into the aggregate", code);
    }

    [Fact]
    public async Task Generate_repository_GetByIdAsync_includes_upToVersion_parameter()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Comment",
            ObjectName = "comment",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Comment.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Comment.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository interface should have upToVersion parameter with XML docs
        Assert.Contains("/// <param name=\"upToVersion\">Optional: The maximum event version to fold. If null, loads to current state.</param>", code);
        Assert.Contains("Task<Comment?> GetByIdAsync(", code);
        Assert.Contains("int? upToVersion = null,", code);

        // Repository implementation should have upToVersion parameter
        Assert.Contains("public async Task<Comment?> GetByIdAsync(", code);
        Assert.Contains("int? upToVersion = null,", code);
    }

    [Fact]
    public async Task Generate_repository_GetByIdAsync_delegates_to_factory_with_upToVersion()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Review",
            ObjectName = "review",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Properties = new List<PropertyDefinition>(),
            Events = new List<EventDefinition>(),
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Review.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));
        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Review.Generated.cs");
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository GetByIdAsync should delegate to factory with upToVersion
        Assert.Contains("return await reviewFactory.GetAsync(id, upToVersion);", code);
    }

    [Fact]
    public void GenerateFoldCode_skips_command_events_without_when_handlers()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Test",
            ObjectName = "Test",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>
            {
                new()
                {
                    TypeName = "LegacyEventCompleted",
                    Namespace = "Test.Events",
                    EventName = "Legacy.Completed",
                    ActivationType = "Command", // From command, no When handler
                    ActivationAwaitRequired = false,
                    File = "",
                    Parameters = new List<ParameterDefinition>()
                },
                new()
                {
                    TypeName = "UserCreated",
                    Namespace = "Test.Events",
                    EventName = "User.Created",
                    ActivationType = "When", // Has When handler
                    ActivationAwaitRequired = false,
                    File = "",
                    Parameters = new List<ParameterDefinition> { new() { Name = "e", Type = "UserCreated", Namespace = "Test.Events" } }
                }
            }
        };
        var usings = new List<string>();

        // Act
        var result = GenerateAggregateCode.GenerateFoldCode(aggregate, usings);

        // Assert
        var code = result.ToString();

        // Should include the When event
        Assert.Contains("case \"User.Created\":", code);
        Assert.Contains("UserCreated", code);

        // Should NOT include the Command event
        Assert.DoesNotContain("case \"Legacy.Completed\":", code);
        Assert.DoesNotContain("LegacyEventCompleted", code);
    }

    [Fact]
    public void GenerateSetupCode_registers_command_events_without_when_handlers()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "TestAggregate",
            ObjectName = "TestAggregate",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Test",
            Events = new List<EventDefinition>
            {
                new()
                {
                    TypeName = "ProjectCompleted",
                    EventName = "Project.Completed",
                    Namespace = "Test.Events",
                    ActivationType = "Command", // From obsolete command, no When handler
                    ActivationAwaitRequired = false,
                    File = ""
                },
                new()
                {
                    TypeName = "ProjectCompletedSuccessfully",
                    EventName = "Project.CompletedSuccessfully",
                    Namespace = "Test.Events",
                    ActivationType = "When", // Has When handler
                    ActivationAwaitRequired = false,
                    File = ""
                }
            }
        };

        // Act
        var result = GenerateAggregateCode.GenerateSetupCode(aggregate);

        // Assert
        var code = result.ToString();

        // Both events should be registered, regardless of ActivationType
        Assert.Contains("Stream.RegisterEvent<ProjectCompleted>", code);
        Assert.Contains("\"Project.Completed\"", code);
        Assert.Contains("ProjectCompletedJsonSerializerContext.Default.ProjectCompleted", code);

        Assert.Contains("Stream.RegisterEvent<ProjectCompletedSuccessfully>", code);
        Assert.Contains("\"Project.CompletedSuccessfully\"", code);
        Assert.Contains("ProjectCompletedSuccessfullyJsonSerializerContext.Default.ProjectCompletedSuccessfully", code);
    }

    [Fact]
    public async Task Generate_registers_command_events_but_skips_fold_cases()
    {
        // Arrange - Simulating an aggregate with a legacy event (from command) and a new event (with When)
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Account",
            ObjectName = "Account",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters = [new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Status", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>
            {
                // Legacy event from deprecated command - should be registered but NOT have Fold case
                new()
                {
                    TypeName = "AccountClosed",
                    Namespace = "Demo.App.Events",
                    EventName = "Account.Closed",
                    ActivationType = "Command",
                    ActivationAwaitRequired = false,
                    Properties = new List<PropertyDefinition>()
                },
                // New event with When handler - should be registered AND have Fold case
                new()
                {
                    TypeName = "AccountClosedSuccessfully",
                    Namespace = "Demo.App.Events",
                    EventName = "Account.ClosedSuccessfully",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Properties = new List<PropertyDefinition>(),
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "e", Type = "AccountClosedSuccessfully", Namespace = "Demo.App.Events" }
                    }
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Account.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Account.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // BOTH events should be registered in GeneratedSetup
        Assert.Contains("Stream.RegisterEvent<AccountClosed>(", code);
        Assert.Contains("\"Account.Closed\"", code);
        Assert.Contains("AccountClosedJsonSerializerContext.Default.AccountClosed", code);

        Assert.Contains("Stream.RegisterEvent<AccountClosedSuccessfully>(", code);
        Assert.Contains("\"Account.ClosedSuccessfully\"", code);
        Assert.Contains("AccountClosedSuccessfullyJsonSerializerContext.Default.AccountClosedSuccessfully", code);

        // Only the When event should have a Fold case
        Assert.Contains("case \"Account.ClosedSuccessfully\":", code);
        Assert.Contains("When(JsonEvent.To(@event, AccountClosedSuccessfullyJsonSerializerContext.Default.AccountClosedSuccessfully));", code);

        // The Command event should NOT have a Fold case
        Assert.DoesNotContain("case \"Account.Closed\":", code);
    }

    [Fact]
    public async Task Generate_adds_EditorBrowsable_attribute_when_user_defined_factory_partial_exists()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "WorkItem",
            ObjectName = "workitem",
            IdentifierType = "WorkItemId",
            IdentifierTypeNamespace = "Demo.App.ValueObjects",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            HasUserDefinedFactoryPartial = true, // User has defined their own partial factory
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Title", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>(),
            FileLocations = new List<string> { "Demo\\Domain\\WorkItem.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "WorkItem.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should contain EditorBrowsable attribute before CreateAsync method
        Assert.Contains("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]", code);
        Assert.Contains("public async Task<WorkItem> CreateAsync(WorkItemId id)", code);

        // Verify the attribute appears right before the CreateAsync method
        var editorBrowsableIndex = code.IndexOf("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]");
        var createAsyncIndex = code.IndexOf("public async Task<WorkItem> CreateAsync(WorkItemId id)");
        Assert.True(editorBrowsableIndex < createAsyncIndex, "EditorBrowsable attribute should appear before CreateAsync method");
    }

    [Fact]
    public async Task Generate_does_not_add_EditorBrowsable_attribute_when_no_user_defined_factory_partial()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Project",
            ObjectName = "project",
            IdentifierType = "ProjectId",
            IdentifierTypeNamespace = "Demo.App.ValueObjects",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            HasUserDefinedFactoryPartial = false, // No user-defined factory partial
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Name", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>(),
            FileLocations = new List<string> { "Demo\\Domain\\Project.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Project.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should NOT contain EditorBrowsable attribute
        Assert.DoesNotContain("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]", code);

        // But should still have the public CreateAsync method
        Assert.Contains("public async Task<Project> CreateAsync(ProjectId id)", code);
    }

    [Fact]
    public async Task Generate_adds_EditorBrowsable_attribute_to_repository_methods_when_user_defined_repository_partial_exists()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "UserProfile",
            ObjectName = "userprofile",
            IdentifierType = "UserProfileId",
            IdentifierTypeNamespace = "Demo.App.ValueObjects",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            HasUserDefinedRepositoryPartial = true, // User has defined their own partial repository
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Email", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>(),
            FileLocations = new List<string> { "Demo\\Domain\\UserProfile.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "UserProfile.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should contain EditorBrowsable attribute before repository methods
        Assert.Contains("[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]", code);

        // Verify repository methods are generated
        Assert.Contains("public async Task<UserProfile?> GetByIdAsync(", code);
        Assert.Contains("public async Task<UserProfile?> GetFirstByDocumentTagAsync(", code);

        // Verify EditorBrowsable appears before repository methods
        // Count the occurrences - there should be one for each repository method (7 total)
        var editorBrowsableCount = System.Text.RegularExpressions.Regex.Matches(code,
            "\\[System\\.ComponentModel\\.EditorBrowsable\\(System\\.ComponentModel\\.EditorBrowsableState\\.Never\\)\\]").Count;
        Assert.True(editorBrowsableCount >= 7, $"Should have at least 7 EditorBrowsable attributes for repository methods, found {editorBrowsableCount}");
    }

    [Fact]
    public async Task Generate_does_not_add_EditorBrowsable_attribute_to_repository_when_no_user_defined_repository_partial()
    {
        // Arrange
        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Product",
            ObjectName = "product",
            IdentifierType = "ProductId",
            IdentifierTypeNamespace = "Demo.App.ValueObjects",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            HasUserDefinedRepositoryPartial = false, // No user-defined repository partial
            HasUserDefinedFactoryPartial = false, // No user-defined factory partial either
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false }
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "Name", Type = "String", Namespace = "System", IsNullable = false }
            },
            Events = new List<EventDefinition>(),
            FileLocations = new List<string> { "Demo\\Domain\\Product.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { aggregate }
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Product.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository methods should exist without EditorBrowsable
        Assert.Contains("public async Task<Product?> GetByIdAsync(", code);

        // EditorBrowsable should not appear at all since no partials exist
        var editorBrowsableCount = System.Text.RegularExpressions.Regex.Matches(code,
            "\\[System\\.ComponentModel\\.EditorBrowsable\\(System\\.ComponentModel\\.EditorBrowsableState\\.Never\\)\\]").Count;
        Assert.Equal(0, editorBrowsableCount);
    }
}
