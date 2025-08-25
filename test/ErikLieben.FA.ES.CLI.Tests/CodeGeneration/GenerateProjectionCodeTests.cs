using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateProjectionCodeTests
{
    private static (SolutionDefinition solution, string outDir) BuildSolution(
        ProjectionDefinition projection)
    {
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Projections = new List<ProjectionDefinition> { projection }
        };

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
    public async Task Generate_writes_fold_and_json_context_and_checkpoint_attribute()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "FeatureFlags",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "documentFactory", Type = "IObjectDocumentFactory", Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false },
                        new ConstructorParameter { Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "IsEnabled", Type = "bool", Namespace = "System", IsNullable = false },
            },
            Events = new List<ProjectionEventDefinition>
            {
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    EventName = "FeatureFlag.Enabled",
                    Namespace = "Demo.App.Events",
                    TypeName = "FeatureFlagEnabled",
                    Properties = new List<PropertyDefinition>(),
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "e", Type = "FeatureFlagEnabled", Namespace = "Demo.App.Events" },
                        new() { Name = "document", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" }
                    },
                    WhenParameterValueFactories = new List<WhenParameterValueFactory>(),
                    WhenParameterDeclarations = new List<WhenParameterDeclaration>()
                }
            },
            FileLocations = new List<string> { "Demo\\FeatureFlags.cs" }
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "FeatureFlags.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // AAA Assert blocks verifying key pieces
        // - fold switch with event case and When invocation
        Assert.Contains("switch (@event.EventType)", code);
        Assert.Contains("case \"FeatureFlag.Enabled\":", code);
        Assert.Contains("When(JsonEvent.ToEvent(@event, FeatureFlagEnabledJsonSerializerContext.Default.FeatureFlagEnabled)", code);
        // - PostWhenAll dummy should exist because none was provided
        Assert.Contains("protected override Task PostWhenAll(IObjectDocument document)", code);
        // - Json serializer context for projection exists
        Assert.Contains("internal partial class FeatureFlagsJsonSerializerContext : JsonSerializerContext", code);
        // - Checkpoint has JsonPropertyName attribute when ExternalCheckpoint == false
        Assert.Contains("[JsonPropertyName(\"$checkpoint\")]", code);
    }

    [Fact]
    public async Task Generate_uses_constructor_selection_and_PostWhen_parameters()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "Accounts",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = true,
            Constructors = new List<ConstructorDefinition>
            {
                // Not matching dependencies (filtered out)
                new() { Parameters = [ new ConstructorParameter { Name = "x", Type = "int", Namespace = "System", IsNullable = false } ] },
                // Best match: includes required factories and matches property by name
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "documentFactory", Type = "IObjectDocumentFactory", Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false },
                        new ConstructorParameter { Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                        new ConstructorParameter { Name = "isActive", Type = "bool", Namespace = "System", IsNullable = false },
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                new() { Name = "IsActive", Type = "bool", Namespace = "System", IsNullable = false },
            },
            Events = new List<ProjectionEventDefinition>
            {
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = true,
                    EventName = "Account.Created",
                    Namespace = "Demo.App.Events",
                    TypeName = "AccountCreated",
                    Properties = new List<PropertyDefinition>(),
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "ev", Type = "IEvent", Namespace = "ErikLieben.FA.ES" },
                        new() { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" },
                    },
                    WhenParameterValueFactories = new List<WhenParameterValueFactory>
                    {
                        new()
                        {
                            Type = new WhenParameterValueItem{ Type = "SomeFactory", Namespace = "" },
                            ForType = new WhenParameterValueItem{ Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                        }
                    },
                    WhenParameterDeclarations = new List<WhenParameterDeclaration>()
                }
            },
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" },
                    new PostWhenParameterDeclaration { Name = "evt", Type = "IEvent", Namespace = "ErikLieben.FA.ES" },
                }
            },
            FileLocations = new List<string> { "Demo\\Accounts.cs" }
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Accounts.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Constructor selection -> LoadFromJson creates new Accounts(documentFactory, eventStreamFactory, obj.IsActive)
        Assert.Contains("return new Accounts(documentFactory, eventStreamFactory", code);
        Assert.Contains("obj.IsActive", code);
        // PostWhen mapping should call PostWhen(document, JsonEvent.ToEvent(@event, @event.EventType)); after switch
        Assert.Contains("PostWhen(document, JsonEvent.ToEvent(@event, @event.EventType));", code);
        // Since ActivationAwaitRequired = true, Fold method should be async and not return Task.CompletedTask directly
        Assert.Contains("public override async Task Fold<T>(IEvent @event, IObjectDocument document", code);
        }

    [Fact]
    public async Task Generate_builds_execution_contexts_and_factories_and_skips_dummy_postwhenall_when_present_and_sets_jsonignore()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "Advanced",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = true, // should result in [JsonIgnore]
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "documentFactory", Type = "IObjectDocumentFactory", Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false },
                        new ConstructorParameter { Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                    ]
                }
            },
            Properties = new List<PropertyDefinition>
            {
                // complex generic property to exercise Inner() with nested generics
                new()
                {
                    Name = "Lookup",
                    Type = "Dictionary",
                    Namespace = "System.Collections.Generic",
                    IsNullable = false,
                    // Dictionary<string, List<Guid>>
                    GenericTypes =
                    [
                        new PropertyGenericTypeDefinition(
                            Name: "String",
                            Namespace: "System",
                            GenericTypes: new List<PropertyGenericTypeDefinition>(),
                            SubTypes: new List<PropertyGenericTypeDefinition>()),
                        new PropertyGenericTypeDefinition(
                            Name: "List",
                            Namespace: "System.Collections.Generic",
                            GenericTypes: new List<PropertyGenericTypeDefinition>
                            {
                                new PropertyGenericTypeDefinition(
                                    Name: "Guid",
                                    Namespace: "System",
                                    GenericTypes: new List<PropertyGenericTypeDefinition>(),
                                    SubTypes: new List<PropertyGenericTypeDefinition>())
                            },
                            SubTypes: new List<PropertyGenericTypeDefinition>())
                    ]
                }
            },
            Events = new List<ProjectionEventDefinition>
            {
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    EventName = "Account.Created",
                    Namespace = "Demo.App.Events",
                    TypeName = "AccountCreated",
                    // Add a property ending with Identifier to trigger Guid serializable path building
                    Properties = new List<PropertyDefinition>
                    {
                        new() { Name = "UserIdentifier", Type = "SomethingIdentifier", Namespace = "Demo.App.Shared", IsNullable = false }
                    },
                    Parameters = new List<ParameterDefinition>
                    {
                        new() { Name = "e", Type = "AccountCreated", Namespace = "Demo.App.Events" },
                        // extra parameters that will be provided by whenLookups
                        new() { Name = "ctx", Type = "IExecutionContext", Namespace = "ErikLieben.FA.ES.Projections" },
                        new() { Name = "ctxEvt", Type = "IExecutionContextWithEvent", Namespace = "ErikLieben.FA.ES.Projections" },
                        new() { Name = "ctxData", Type = "IExecutionContextWithData", Namespace = "ErikLieben.FA.ES.Projections" },
                        new() { Name = "custom", Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                    },
                    WhenParameterValueFactories = new List<WhenParameterValueFactory>
                    {
                        new()
                        {
                            Type = new WhenParameterValueItem { Type = "SomeFactory", Namespace = "" },
                            ForType = new WhenParameterValueItem { Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                        }
                    },
                    WhenParameterDeclarations = new List<WhenParameterDeclaration>
                    {
                        new() { Name = "ctx", Type = "IExecutionContext", Namespace = "ErikLieben.FA.ES.Projections", GenericArguments = new List<GenericArgument>() },
                        new() { Name = "ctxEvt", Type = "IExecutionContextWithEvent", Namespace = "ErikLieben.FA.ES.Projections", GenericArguments = new List<GenericArgument>() },
                        new() { Name = "ctxData", Type = "IExecutionContextWithData", Namespace = "ErikLieben.FA.ES.Projections", GenericArguments = new List<GenericArgument>{ new GenericArgument{ Type = "System.String", Namespace = "System" } } },
                        new() { Name = "custom", Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events", GenericArguments = new List<GenericArgument>() }
                    }
                }
            },
            // Signal that a user-implemented PostWhenAll exists -> generator must NOT add dummy override
            HasPostWhenAllMethod = true,
            FileLocations = new List<string> { "Demo\\Advanced.cs" }
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Advanced.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Checkpoint should be JsonIgnore for ExternalCheckpoint=true
        Assert.Contains("[JsonIgnore]", code);
        // No dummy PostWhenAll override
        Assert.DoesNotContain("protected override Task PostWhenAll(IObjectDocument document)", code);

        // WhenParameterValueFactories dictionary entry exists
        Assert.Contains("{\"Demo.App.Events.SomeType\", new SomeFactory()}", code);
        // ExecutionContext creations present
        Assert.Contains("ExecutionContext<AccountCreated", code); // generic context present
        Assert.Contains("IExecutionContextWithData<", code); // WithData context present
        // Default lookup for custom parameter
        Assert.Contains("GetWhenParameterValue<Demo.App.Events.SomeType, AccountCreated>(", code);

        // Complex generic property was rendered in interface
        Assert.Contains("public Dictionary<System.String", code);
    }

    [Fact]
    public async Task Generate_emits_blob_projection_factory_and_azure_usings()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "Blobbed",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors = new List<ConstructorDefinition>
            {
                new() { Parameters = [
                    new ConstructorParameter { Name = "documentFactory", Type = "IObjectDocumentFactory", Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false },
                    new ConstructorParameter { Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                ]}
            },
            Properties = new List<PropertyDefinition>(),
            Events = new List<ProjectionEventDefinition>(),
            BlobProjection = new BlobProjectionDefinition { Container = "cont", Connection = "conn" },
            FileLocations = new List<string> { "Demo\\Blobbed.cs" }
        };
        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Blobbed.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Azure usings
        Assert.Contains("using ErikLieben.FA.ES.AzureStorage.Blob;", code);
        Assert.Contains("using Microsoft.Extensions.Azure;", code);
        Assert.Contains("using Azure.Storage.Blobs;", code);

        // Blob projection factory class with proper base and constructor
        Assert.Contains("public class BlobbedFactory(", code);
        Assert.Contains(": BlobProjectionFactory<Blobbed>(", code);
        Assert.Contains("return new Blobbed(objectDocumentFactory, eventStreamFactory);", code);
        // External checkpoint flag used in HasExternalCheckpoint override
        Assert.Contains("protected override bool HasExternalCheckpoint => false;", code);
    }
}
