#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

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
            Projections = [projection]
        };

        var solution = new SolutionDefinition
        {
            SolutionName = "Demo",
            Generator = new GeneratorInformation { Version = "1.0.0-test" },
            Projects = [project]
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
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        },
                    ]
                }
            ],
            Properties =
            [
                new() { Name = "IsEnabled", Type = "bool", Namespace = "System", IsNullable = false }
            ],
            Events =
            [
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    EventName = "FeatureFlag.Enabled",
                    Namespace = "Demo.App.Events",
                    TypeName = "FeatureFlagEnabled",
                    Properties = [],
                    Parameters =
                    [
                        new() { Name = "e", Type = "FeatureFlagEnabled", Namespace = "Demo.App.Events" },
                        new() { Name = "document", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" }
                    ],
                    WhenParameterValueFactories = [],
                    WhenParameterDeclarations = []
                }
            ],
            FileLocations = ["Demo\\FeatureFlags.cs"]
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
        // - Unused usings should be removed by CodeFormattingHelper
        // - No pragma warning should be present as we use Roslyn-based unused using removal
        Assert.DoesNotContain("#pragma warning disable IDE0005", code);
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
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "x", Type = "int", Namespace = "System", IsNullable = false }
                    ]
                },
                // Best match: includes required factories and matches property by name
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        },
                        new ConstructorParameter
                            { Name = "isActive", Type = "bool", Namespace = "System", IsNullable = false },
                    ]
                }
            ],
            Properties =
            [
                new() { Name = "IsActive", Type = "bool", Namespace = "System", IsNullable = false }
            ],
            Events =
            [
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = true,
                    EventName = "Account.Created",
                    Namespace = "Demo.App.Events",
                    TypeName = "AccountCreated",
                    Properties = [],
                    Parameters =
                    [
                        new() { Name = "ev", Type = "IEvent", Namespace = "ErikLieben.FA.ES" },
                        new() { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" }
                    ],
                    WhenParameterValueFactories =
                    [
                        new()
                        {
                            Type = new WhenParameterValueItem { Type = "SomeFactory", Namespace = "" },
                            ForType = new WhenParameterValueItem
                                { Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                        }
                    ],
                    WhenParameterDeclarations = []
                }
            ],
            PostWhen = new PostWhenDeclaration
            {
                Parameters =
                {
                    new PostWhenParameterDeclaration { Name = "doc", Type = "IObjectDocument", Namespace = "ErikLieben.FA.ES.Documents" },
                    new PostWhenParameterDeclaration { Name = "evt", Type = "IEvent", Namespace = "ErikLieben.FA.ES" },
                }
            },
            FileLocations = ["Demo\\Accounts.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Accounts.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Constructor selection -> LoadFromJson uses custom deserialization with proper constructor
        Assert.Contains("var instance = new Accounts(documentFactory, eventStreamFactory", code);
        Assert.Contains("isActive", code);  // isActive variable should be deserialized
        // PostWhen mapping should call PostWhen with event after switch (using version token variant)
        Assert.Contains("PostWhen(", code);
        Assert.Contains("JsonEvent.ToEvent(@event, @event.EventType)", code);
        // Since ActivationAwaitRequired = true, Fold method should be async and use VersionToken signature
        Assert.Contains("public override async Task Fold<T>(IEvent @event, VersionToken versionToken", code);
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
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        },
                    ]
                }
            ],
            Properties =
            [
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
                            GenericTypes: [],
                            SubTypes: []),
                        new PropertyGenericTypeDefinition(
                            Name: "List",
                            Namespace: "System.Collections.Generic",
                            GenericTypes:
                            [
                                new PropertyGenericTypeDefinition(
                                    Name: "Guid",
                                    Namespace: "System",
                                    GenericTypes: [],
                                    SubTypes: [])
                            ],
                            SubTypes: [])
                    ]
                }
            ],
            Events =
            [
                new()
                {
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    EventName = "Account.Created",
                    Namespace = "Demo.App.Events",
                    TypeName = "AccountCreated",
                    // Add a property ending with Identifier to trigger Guid serializable path building
                    Properties =
                    [
                        new()
                        {
                            Name = "UserIdentifier", Type = "SomethingIdentifier", Namespace = "Demo.App.Shared",
                            IsNullable = false
                        }
                    ],
                    Parameters =
                    [
                        new() { Name = "e", Type = "AccountCreated", Namespace = "Demo.App.Events" },
                        // extra parameters that will be provided by whenLookups
                        new() { Name = "ctx", Type = "IExecutionContext", Namespace = "ErikLieben.FA.ES.Projections" },
                        new()
                        {
                            Name = "ctxEvt", Type = "IExecutionContextWithEvent",
                            Namespace = "ErikLieben.FA.ES.Projections"
                        },

                        new()
                        {
                            Name = "ctxData", Type = "IExecutionContextWithData",
                            Namespace = "ErikLieben.FA.ES.Projections"
                        },

                        new() { Name = "custom", Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                    ],
                    WhenParameterValueFactories =
                    [
                        new()
                        {
                            Type = new WhenParameterValueItem { Type = "SomeFactory", Namespace = "" },
                            ForType = new WhenParameterValueItem
                                { Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events" }
                        }
                    ],
                    WhenParameterDeclarations =
                    [
                        new()
                        {
                            Name = "ctx", Type = "IExecutionContext", Namespace = "ErikLieben.FA.ES.Projections",
                            GenericArguments = []
                        },

                        new()
                        {
                            Name = "ctxEvt", Type = "IExecutionContextWithEvent",
                            Namespace = "ErikLieben.FA.ES.Projections", GenericArguments = []
                        },

                        new()
                        {
                            Name = "ctxData", Type = "IExecutionContextWithData",
                            Namespace = "ErikLieben.FA.ES.Projections",
                            GenericArguments = [new GenericArgument { Type = "System.String", Namespace = "System" }]
                        },

                        new()
                        {
                            Name = "custom", Type = "Demo.App.Events.SomeType", Namespace = "Demo.App.Events",
                            GenericArguments = []
                        }
                    ]
                }
            ],
            // Signal that a user-implemented PostWhenAll exists -> generator must NOT add dummy override
            HasPostWhenAllMethod = true,
            FileLocations = ["Demo\\Advanced.cs"]
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
        // Version token based fold uses parentContext for execution context parameters
        Assert.Contains("parentContext", code);
        // Default lookup for custom parameter uses versionToken
        Assert.Contains("GetWhenParameterValue<Demo.App.Events.SomeType, AccountCreated>(", code);
        Assert.Contains("versionToken", code);

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
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        },
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "cont", Connection = "conn" },
            FileLocations = ["Demo\\Blobbed.cs"]
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
        Assert.Contains("public partial class BlobbedFactory(", code);
        Assert.Contains(": BlobProjectionFactory<Blobbed>(", code);
        Assert.Contains("return new Blobbed(objectDocumentFactory, eventStreamFactory);", code);
        // External checkpoint flag used in HasExternalCheckpoint override
        Assert.Contains("protected override bool HasExternalCheckpoint => false;", code);
    }

    [Fact]
    public async Task Generate_blob_projection_factory_with_custom_dependencies_injects_via_service_provider()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "ProjectionWithDependencies",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                    ]
                },
                // Constructor with custom dependencies - should be selected

                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "taskItemFactory", Type = "ITaskItemFactory", Namespace = "Demo.App.Factories",
                            IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "projectFactory", Type = "IProjectFactory", Namespace = "Demo.App.Factories",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\ProjectionWithDependencies.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "ProjectionWithDependencies.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory constructor should include IServiceProvider parameter
        Assert.Contains("IServiceProvider serviceProvider", code);
        Assert.Contains("public partial class ProjectionWithDependenciesFactory(", code);

        // New() method should resolve custom dependencies via DI
        Assert.Contains("var taskItemFactory = serviceProvider.GetService(typeof(ITaskItemFactory)) as ITaskItemFactory;", code);
        Assert.Contains("var projectFactory = serviceProvider.GetService(typeof(IProjectFactory)) as IProjectFactory;", code);

        // New() method should pass resolved dependencies to projection constructor
        Assert.Contains("return new ProjectionWithDependencies(objectDocumentFactory, eventStreamFactory, taskItemFactory!, projectFactory!);", code);
    }

    [Fact]
    public async Task Generate_blob_projection_factory_without_custom_dependencies_does_not_inject_service_provider()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "SimpleProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\SimpleProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "SimpleProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory constructor should NOT include IServiceProvider when there are no custom dependencies
        Assert.DoesNotContain("IServiceProvider serviceProvider", code);

        // New() method should NOT have any DI resolution code (no var statements)
        Assert.DoesNotContain("var ", code.Split("protected override SimpleProjection New()")[1].Split("return new SimpleProjection")[0]);

        // New() method should only pass objectDocumentFactory and eventStreamFactory
        Assert.Contains("return new SimpleProjection(objectDocumentFactory, eventStreamFactory);", code);
    }

    [Fact]
    public async Task Generate_includes_json_serializable_attributes_for_projection_properties()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "LibraryProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors = [],
            Properties =
            [
                new PropertyDefinition
                {
                    Name = "QuestionnaireId",
                    Type = "Int64",
                    Namespace = "System",
                    IsNullable = false
                },
                // Generic collection property with complex type

                new PropertyDefinition
                {
                    Name = "Questions",
                    Type = "List",
                    Namespace = "System.Collections.Generic",
                    IsNullable = true,
                    GenericTypes =
                    [
                        new PropertyGenericTypeDefinition(
                            Name: "QuestionItem",
                            Namespace: "Demo.App.Model",
                            GenericTypes: [],
                            SubTypes: []
                        )
                    ]
                }
            ],
            Events = [],
            FileLocations = ["Demo\\LibraryProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "LibraryProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should include serializable attributes for the projection itself
        Assert.Contains("[JsonSerializable(typeof(LibraryProjection))]", code);

        // Should include serializable attributes for simple property type
        Assert.Contains("[JsonSerializable(typeof(System.Int64))]", code);

        // Should include serializable attributes for collection type
        Assert.Contains("[JsonSerializable(typeof(System.Collections.Generic.List<Demo.App.Model.QuestionItem>))]", code);

        // Should include serializable attributes for the item type within the collection
        Assert.Contains("[JsonSerializable(typeof(Demo.App.Model.QuestionItem))]", code);
    }

    [Fact]
    public async Task Generate_includes_json_serializable_attributes_for_nested_types()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "ComplexProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors = [],
            Properties =
            [
                new PropertyDefinition
                {
                    Name = "Items",
                    Type = "List",
                    Namespace = "System.Collections.Generic",
                    IsNullable = true,
                    GenericTypes =
                    [
                        new PropertyGenericTypeDefinition(
                            Name: "ItemWithText",
                            Namespace: "Demo.App.Model",
                            GenericTypes: [],
                            SubTypes:
                            [
                                new PropertyGenericTypeDefinition(
                                    Name: "TextItem",
                                    Namespace: "Demo.App.Model",
                                    GenericTypes: [],
                                    SubTypes: []
                                )
                            ]
                        )
                    ]
                }
            ],
            Events = [],
            FileLocations = ["Demo\\ComplexProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "ComplexProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should include serializable attributes for the collection
        Assert.Contains("[JsonSerializable(typeof(System.Collections.Generic.List<Demo.App.Model.ItemWithText>))]", code);

        // Should include serializable attributes for the main item type
        Assert.Contains("[JsonSerializable(typeof(Demo.App.Model.ItemWithText))]", code);

        // Should include serializable attributes for nested TextItem type
        Assert.Contains("[JsonSerializable(typeof(Demo.App.Model.TextItem))]", code);
    }

    [Fact]
    public async Task Generate_blob_projection_factory_includes_load_from_json_override()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "TestProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\TestProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "TestProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory should include LoadFromJson override method
        Assert.Contains("protected override TestProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)", code);

        // Override should call the static LoadFromJson method on the projection
        Assert.Contains("return TestProjection.LoadFromJson(json, documentFactory, eventStreamFactory);", code);
    }

    [Fact]
    public async Task Generate_blob_projection_factory_load_from_json_override_has_correct_signature()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "MyCustomProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = true,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "my-container", Connection = "MyConnection" },
            FileLocations = ["Demo\\MyCustomProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "MyCustomProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Verify the method signature is correct (protected override, nullable return, correct parameters)
        Assert.Contains("protected override MyCustomProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)", code);

        // Verify the override calls the static method
        Assert.Contains("return MyCustomProjection.LoadFromJson(json, documentFactory, eventStreamFactory);", code);
    }

    [Fact]
    public async Task Generate_projection_without_blob_factory_does_not_include_load_from_json_override()
    {
        // Arrange - projection without BlobProjection
        var projection = new ProjectionDefinition
        {
            Name = "NonBlobProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = null, // No blob projection
            FileLocations = ["Demo\\NonBlobProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "NonBlobProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should not have a factory class at all
        Assert.DoesNotContain("NonBlobProjectionFactory", code);

        // Should still have the static LoadFromJson method on the projection itself
        Assert.Contains("public static NonBlobProjection? LoadFromJson(string json, IObjectDocumentFactory documentFactory, IEventStreamFactory eventStreamFactory)", code);
    }

    [Fact]
    public async Task Generate_checkpoint_deserialization_uses_null_coalescing_for_non_nullable_checkpoint()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "CheckpointProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties =
            [
                new()
                {
                    Name = "Checkpoint",
                    Type = "Checkpoint",
                    Namespace = "ErikLieben.FA.ES",
                    IsNullable = false
                }
            ],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\CheckpointProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "CheckpointProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Checkpoint deserialization should use null-coalescing operator to default to empty array
        Assert.Contains("checkpoint = JsonSerializer.Deserialize<ErikLieben.FA.ES.Checkpoint>(ref reader, CheckpointProjectionJsonSerializerContext.Default.Options) ?? [];", code);
    }

    [Fact]
    public async Task Generate_deserialization_uses_dollar_prefix_for_checkpoint_fingerprint_json_property()
    {
        // Arrange - projection with external checkpoint to verify CheckpointFingerprint deserialization
        var projection = new ProjectionDefinition
        {
            Name = "ExternalCheckpointProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = true,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties =
            [
                new()
                {
                    Name = "Items",
                    Type = "List",
                    Namespace = "System.Collections.Generic",
                    IsNullable = true,
                    GenericTypes =
                    [
                        new PropertyGenericTypeDefinition(
                            Name: "String",
                            Namespace: "System",
                            GenericTypes: [],
                            SubTypes: [])
                    ]
                },
                // Include Checkpoint property to verify it uses $checkpoint JSON name
                new()
                {
                    Name = "Checkpoint",
                    Type = "Checkpoint",
                    Namespace = "ErikLieben.FA.ES",
                    IsNullable = false
                },
                new()
                {
                    Name = "CheckpointFingerprint",
                    Type = "String",
                    Namespace = "System",
                    IsNullable = true
                }
            ],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\ExternalCheckpointProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "ExternalCheckpointProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // CheckpointFingerprint should be deserialized using $checkpointFingerprint JSON property name
        // (matching the [JsonPropertyName("$checkpointFingerprint")] attribute in base Projection class)
        Assert.Contains("case \"$checkpointFingerprint\":", code);

        // Checkpoint should be deserialized using $checkpoint JSON property name
        Assert.Contains("case \"$checkpoint\":", code);

        // CheckpointFingerprint should be assigned to instance after deserialization
        Assert.Contains("instance.CheckpointFingerprint = checkpointFingerprint;", code);

        // Checkpoint should also be assigned
        Assert.Contains("instance.Checkpoint = checkpoint;", code);
    }

    [Fact]
    public async Task Generate_blob_factory_is_partial_class()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "PartialFactoryProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = false,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            BlobProjection = new BlobProjectionDefinition { Container = "projections", Connection = "BlobStorage" },
            FileLocations = ["Demo\\PartialFactoryProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "PartialFactoryProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory class should be partial to allow user-defined extensions
        Assert.Contains("public partial class PartialFactoryProjectionFactory(", code);
    }

    [Fact]
    public async Task Generate_cosmosdb_factory_is_partial_class()
    {
        // Arrange
        var projection = new ProjectionDefinition
        {
            Name = "CosmosPartialProjection",
            Namespace = "Demo.App.Projections",
            ExternalCheckpoint = true,
            Constructors =
            [
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter
                        {
                            Name = "documentFactory", Type = "IObjectDocumentFactory",
                            Namespace = "ErikLieben.FA.ES.Documents", IsNullable = false
                        },
                        new ConstructorParameter
                        {
                            Name = "eventStreamFactory", Type = "IEventStreamFactory", Namespace = "ErikLieben.FA.ES",
                            IsNullable = false
                        }
                    ]
                }
            ],
            Properties = [],
            Events = [],
            CosmosDbProjection = new CosmosDbProjectionDefinition
            {
                Container = "projections",
                PartitionKeyPath = "/projectionName",
                Connection = "cosmosdb"
            },
            FileLocations = ["Demo\\CosmosPartialProjection.cs"]
        };

        var (solution, outDir) = BuildSolution(projection);
        var sut = new GenerateProjectionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "CosmosPartialProjection.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // CosmosDB factory class should be partial to allow user-defined extensions
        Assert.Contains("public partial class CosmosPartialProjectionFactory(", code);
    }
}
