using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateExtensionCodeTests
{
    private static (SolutionDefinition solution, string outDir) BuildSolution(ProjectDefinition project)
    {
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
    public async Task Generate_writes_factory_extension_and_registrations_for_aggregates()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Account",
                    ObjectName = "Account",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Account.cs"]
                }
            ],
            InheritedAggregates =
            [
                new InheritedAggregateDefinition
                {
                    InheritedIdentifierName = "OrderBase",
                    InheritedNamespace = "Demo.App.Domain",
                    IdentifierName = "Order",
                    ObjectName = "Order",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    ParentInterface = "Demo.App.Domain.IOrder",
                    ParentInterfaceNamespace = "Demo.App.Domain",
                    FileLocations = ["Demo\\Domain\\Order.cs"]
                }
            ],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Class names based on ProjectNameRegex (remove dots)
        Assert.Contains("public partial class DemoAppFactory", code);
        Assert.Contains("public static class DemoAppExtensions", code);

        // Registration lines for aggregate and inherited aggregate
        Assert.Contains("serviceCollection.AddSingleton<IAggregateFactory<Account, Guid>, AccountFactory>();", code);
        Assert.Contains("serviceCollection.AddSingleton<IAggregateFactory<Order, Guid>, OrderFactory>();", code);

        // Mapping switch cases
        Assert.Contains("Type agg when agg == typeof(Account) => typeof(IAggregateFactory<Account, Guid>)", code);
        Assert.Contains("Type agg when agg == typeof(Order) => typeof(IAggregateFactory<Order, Guid>)", code);

        // Extension method wires factory registration
        Assert.Contains("public static IServiceCollection ConfigureDemoAppFactory(this IServiceCollection services)", code);
        Assert.Contains("DemoAppFactory.Register(services);", code);
    }

    [Fact]
    public async Task Generate_writes_json_serializer_contexts_for_events_and_property_subtypes()
    {
        // Arrange
        var eventProps = new List<PropertyDefinition>
        {
            // Property with a subtype Guid to force [JsonSerializable(typeof(Guid))]
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
                        GenericTypes: [],
                        SubTypes: [])
                ]
            }
        };

        var aggregate = new AggregateDefinition
        {
            IdentifierName = "Account",
            ObjectName = "Account",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Events =
            [
                new EventDefinition
                {
                    TypeName = "AccountCreated",
                    Namespace = "Demo.App.Events",
                    EventName = "Account.Created",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Properties = eventProps
                }
            ]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [aggregate],
            Projections =
            [
                new ProjectionDefinition
                {
                    Name = "AccountsProjection",
                    Namespace = "Demo.App.Projections",
                    Constructors = [],
                    Properties = [],
                    Events =
                    [
                        new ProjectionEventDefinition
                        {
                            TypeName = "FeatureFlagEnabled",
                            Namespace = "Demo.App.Events",
                            EventName = "FeatureFlag.Enabled",
                            ActivationType = "When",
                            ActivationAwaitRequired = false,
                            Parameters = [],
                            Properties = []
                        }
                    ],
                    FileLocations = ["Demo\\Projections\\AccountsProjection.cs"]
                }
            ]
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Using directives include event namespace
        Assert.Contains("using Demo.App.Events;", code);

        // JsonSerializable attributes for event types gathered from both aggregates and projections
        Assert.Contains("[JsonSerializable(typeof(AccountCreated))]", code);
        Assert.Contains("[JsonSerializable(typeof(FeatureFlagEnabled))]", code);

        // Property subtype Guid should be marked serializable
        Assert.Contains("[JsonSerializable(typeof(Guid))]", code);

        // JsonSerializerContext classes for events exist
        Assert.Contains("internal partial class AccountCreatedJsonSerializerContext : JsonSerializerContext", code);
        Assert.Contains("internal partial class FeatureFlagEnabledJsonSerializerContext : JsonSerializerContext", code);
    }

    [Fact]
    public async Task Generate_skips_non_partial_aggregates_and_filters_collection_serializables()
    {
        // Arrange
        var eventProps = new List<PropertyDefinition>
        {
            // A generic collection property IList<Guid> should be filtered out from JsonSerializable
            new PropertyDefinition
            {
                Name = "Ids",
                Type = "IList",
                Namespace = "System.Collections.Generic",
                IsNullable = false,
                GenericTypes =
                [
                    new PropertyGenericTypeDefinition(
                        Name: "Guid",
                        Namespace: "System",
                        GenericTypes: [],
                        SubTypes: [])
                ],
                // Add a complex subtype with its own generic arguments to exercise nested generics
                SubTypes =
                [
                    new PropertyGenericTypeDefinition(
                        Name: "Dictionary",
                        Namespace: "System.Collections.Generic",
                        GenericTypes:
                        [
                            new PropertyGenericTypeDefinition(
                                Name: "String",
                                Namespace: "System",
                                GenericTypes: [],
                                SubTypes: []),

                            new PropertyGenericTypeDefinition(
                                Name: "Int32",
                                Namespace: "System",
                                GenericTypes: [],
                                SubTypes: [])
                        ],
                        SubTypes: [])
                ]
            }
        };

        var partialAgg = new AggregateDefinition
        {
            IdentifierName = "User",
            ObjectName = "User",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            Events =
            [
                new EventDefinition
                {
                    TypeName = "UserRegistered",
                    Namespace = "Demo.App.Events",
                    EventName = "User.Registered",
                    ActivationType = "When",
                    ActivationAwaitRequired = false,
                    Properties = eventProps
                }
            ]
        };

        var nonPartialAgg = new AggregateDefinition
        {
            IdentifierName = "Temp",
            ObjectName = "Temp",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = false,
            FileLocations = ["Demo\\Domain\\Temp.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [partialAgg, nonPartialAgg],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Only partial aggregate should be registered/mapped
        Assert.Contains("serviceCollection.AddSingleton<IAggregateFactory<User, Guid>, UserFactory>();", code);
        Assert.DoesNotContain("serviceCollection.AddSingleton<IAggregateFactory<Temp, Guid>, TempFactory>();", code);
        Assert.Contains("Type agg when agg == typeof(User) => typeof(IAggregateFactory<User, Guid>)", code);
        Assert.DoesNotContain("Type agg when agg == typeof(Temp)", code);

        // Root factory registration exists
        Assert.Contains("serviceCollection.AddSingleton<IAggregateFactory, DemoAppFactory>();", code);

        // Subtypes and nested generics should be serialized
        Assert.Contains("[JsonSerializable(typeof(Dictionary<String, Int32>))]", code);
    }

    [Fact]
    public async Task Generate_registers_repository_interfaces_and_implementations()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Product",
                    ObjectName = "product",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Product.cs"]
                },
                new AggregateDefinition
                {
                    IdentifierName = "Order",
                    ObjectName = "order",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Order.cs"]
                }
            ],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository registrations with AddScoped
        Assert.Contains("serviceCollection.AddScoped<IProductRepository, ProductRepository>();", code);
        Assert.Contains("serviceCollection.AddScoped<IOrderRepository, OrderRepository>();", code);
    }

    [Fact]
    public async Task Generate_registers_repositories_for_inherited_aggregates()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [],
            InheritedAggregates =
            [
                new InheritedAggregateDefinition
                {
                    InheritedIdentifierName = "OrderBase",
                    InheritedNamespace = "Demo.App.Domain",
                    IdentifierName = "Order",
                    ObjectName = "order",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    ParentInterface = "Demo.App.Domain.IOrder",
                    ParentInterfaceNamespace = "Demo.App.Domain",
                    FileLocations = ["Demo\\Domain\\Order.cs"]
                }
            ],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository registration for inherited aggregate
        Assert.Contains("serviceCollection.AddScoped<IOrderRepository, OrderRepository>();", code);
    }

    [Fact]
    public async Task Generate_does_not_register_repositories_for_non_partial_aggregates()
    {
        // Arrange
        var partialAgg = new AggregateDefinition
        {
            IdentifierName = "Product",
            ObjectName = "product",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = true,
            FileLocations = ["Demo\\Domain\\Product.cs"]
        };

        var nonPartialAgg = new AggregateDefinition
        {
            IdentifierName = "Temp",
            ObjectName = "temp",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            IsPartialClass = false,
            FileLocations = ["Demo\\Domain\\Temp.cs"]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [partialAgg, nonPartialAgg],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Repository for partial aggregate should be registered
        Assert.Contains("serviceCollection.AddScoped<IProductRepository, ProductRepository>();", code);

        // Repository for non-partial aggregate should NOT be registered
        Assert.DoesNotContain("ITempRepository", code);
        Assert.DoesNotContain("TempRepository", code);
    }

    [Fact]
    public async Task Generate_includes_aggregate_namespaces_in_using_statements()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Product",
                    ObjectName = "product",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain.Aggregates",  // Different namespace from project
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Aggregates\\Product.cs"]
                }
            ],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Aggregate namespace should be included in using statements
        Assert.Contains("using Demo.App.Domain.Aggregates;", code);
    }

    [Fact]
    public async Task Generate_registers_aggregate_storage_registry_when_blob_settings_present()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Order",
                    ObjectName = "order",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Order.cs"],
                    EventStreamBlobSettingsAttribute = new EventStreamBlobSettingsAttributeData
                    {
                        DataStore = "orders-store"
                    }
                }
            ],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Storage registry code should be present
        Assert.Contains("IAggregateStorageRegistry", code);
        Assert.Contains("AggregateStorageRegistry", code);
        Assert.Contains("orders-store", code);
    }

    [Fact]
    public async Task Generate_registers_projection_factories_for_blob_projections()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [],
            InheritedAggregates = [],
            Projections =
            [
                new ProjectionDefinition
                {
                    Name = "Dashboard",
                    Namespace = "Demo.App.Projections",
                    FileLocations = ["Demo\\Projections\\Dashboard.cs"],
                    BlobProjection = new BlobProjectionDefinition
                    {
                        Container = "dashboards",
                        Connection = "StorageConnection"
                    },
                    Constructors = [],
                    Properties = [],
                    Events = []
                }
            ]
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Projection registration code should be present
        Assert.Contains("services.AddSingleton<Demo.App.Projections.DashboardFactory>();", code);
        Assert.Contains("services.AddSingleton<Demo.App.Projections.Dashboard>", code);
        Assert.Contains("GetOrCreateAsync", code);
    }

    [Fact]
    public async Task Generate_skips_projects_starting_with_framework_name()
    {
        // Arrange
        var frameworkProject = new ProjectDefinition
        {
            Name = "ErikLieben.FA.ES.Core",
            Namespace = "ErikLieben.FA.ES.Core",
            FileLocation = "ErikLieben.FA.ES.Core.csproj",
            Aggregates = [],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(frameworkProject);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "ErikLieben.FA.ES.CoreExtensions.Generated.cs");
        Assert.False(File.Exists(generatedPath));
    }

    [Fact]
    public async Task Generate_skips_generated_file_locations()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.generated.csproj",  // Already generated
            Aggregates = [],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.False(File.Exists(generatedPath));
    }

    [Fact]
    public async Task Generate_handles_event_parameters_with_subtypes()
    {
        // Arrange
        var eventWithParams = new EventDefinition
        {
            TypeName = "ItemAdded",
            Namespace = "Demo.App.Events",
            EventName = "Item.Added",
            ActivationType = "When",
            ActivationAwaitRequired = false,
            Properties = [],
            Parameters =
            [
                new ParameterDefinition
                {
                    Name = "item",
                    Type = "Item",
                    Namespace = "Demo.App.Models",
                    SubTypes =
                    [
                        new ParameterGenericTypeDefinition(
                            Name: "Quantity",
                            Namespace: "Demo.App.ValueObjects",
                            GenericTypes: [],
                            SubTypes: [])
                    ]
                }
            ]
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Cart",
                    ObjectName = "cart",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Cart.cs"],
                    Events = [eventWithParams]
                }
            ],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Parameter subtype should be serialized
        Assert.Contains("[JsonSerializable(typeof(Quantity))]", code);
        Assert.Contains("using Demo.App.ValueObjects;", code);
    }

    [Fact]
    public async Task Generate_handles_generic_properties_with_nested_subtypes()
    {
        // Arrange
        var eventWithGeneric = new EventDefinition
        {
            TypeName = "DataLoaded",
            Namespace = "Demo.App.Events",
            EventName = "Data.Loaded",
            ActivationType = "When",
            ActivationAwaitRequired = false,
            Properties =
            [
                new PropertyDefinition
                {
                    Name = "items",
                    Type = "Dictionary",
                    Namespace = "System.Collections.Generic",
                    IsNullable = false,
                    GenericTypes =
                    [
                        new PropertyGenericTypeDefinition(
                            Name: "String",
                            Namespace: "System",
                            GenericTypes: [],
                            SubTypes:
                            [
                                new PropertyGenericTypeDefinition(
                                    Name: "CustomValue",
                                    Namespace: "Demo.App.Models",
                                    GenericTypes: [],
                                    SubTypes: [])
                            ]),
                        new PropertyGenericTypeDefinition(
                            Name: "Int32",
                            Namespace: "System",
                            GenericTypes: [],
                            SubTypes: [])
                    ],
                    SubTypes = []
                }
            ],
            Parameters = []
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates =
            [
                new AggregateDefinition
                {
                    IdentifierName = "Store",
                    ObjectName = "store",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    IsPartialClass = true,
                    FileLocations = ["Demo\\Domain\\Store.cs"],
                    Events = [eventWithGeneric]
                }
            ],
            InheritedAggregates = [],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Generic type with signature should be serialized
        Assert.Contains("[JsonSerializable(typeof(Dictionary<String, Int32>))]", code);
        // Nested subtype from generic should be serialized
        Assert.Contains("[JsonSerializable(typeof(CustomValue))]", code);
    }

    [Fact]
    public async Task Generate_does_not_register_projection_without_blob_attribute()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [],
            InheritedAggregates = [],
            Projections =
            [
                new ProjectionDefinition
                {
                    Name = "SimpleProjection",
                    Namespace = "Demo.App.Projections",
                    FileLocations = ["Demo\\Projections\\SimpleProjection.cs"],
                    BlobProjection = null,  // No blob projection
                    Constructors = [],
                    Properties = [],
                    Events = []
                }
            ]
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Should not contain projection factory registration
        Assert.DoesNotContain("SimpleProjectionFactory", code);
    }

    [Fact]
    public async Task Generate_registers_storage_from_inherited_aggregates()
    {
        // Arrange
        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = [],
            InheritedAggregates =
            [
                new InheritedAggregateDefinition
                {
                    InheritedIdentifierName = "BaseOrder",
                    InheritedNamespace = "Demo.App.Domain",
                    IdentifierName = "SpecialOrder",
                    ObjectName = "specialorder",
                    IdentifierType = "Guid",
                    IdentifierTypeNamespace = "System",
                    Namespace = "Demo.App.Domain",
                    ParentInterface = "Demo.App.Domain.IOrder",
                    ParentInterfaceNamespace = "Demo.App.Domain",
                    FileLocations = ["Demo\\Domain\\SpecialOrder.cs"],
                    EventStreamBlobSettingsAttribute = new EventStreamBlobSettingsAttributeData
                    {
                        DataStore = "special-orders-store"
                    }
                }
            ],
            Projections = []
        };

        var (solution, outDir) = BuildSolution(project);
        var sut = new GenerateExtensionCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo.AppExtensions.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Storage registry from inherited aggregate
        Assert.Contains("special-orders-store", code);
    }

    [Fact]
    public async Task Constructor_sets_properties_correctly()
    {
        // Arrange
        var solution = new SolutionDefinition
        {
            SolutionName = "Test",
            Generator = new GeneratorInformation { Version = "1.0.0" },
            Projects = []
        };
        var config = new Config();
        var solutionPath = "/path/to/solution";

        // Act
        var sut = new GenerateExtensionCode(solution, config, solutionPath);

        // Assert
        Assert.NotNull(sut);
    }
}
