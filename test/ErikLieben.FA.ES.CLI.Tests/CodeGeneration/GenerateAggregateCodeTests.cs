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

        // Async factory helpers, including generic CreateAsync<T>
        Assert.Contains("public async Task<Account> CreateAsync(Guid id)", code);
        Assert.Contains("protected async Task<Account> CreateAsync<T>(Guid id, T firstEvent) where T : class", code);
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
}
