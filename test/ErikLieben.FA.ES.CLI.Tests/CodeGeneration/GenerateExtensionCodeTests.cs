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
            Projects = new List<ProjectDefinition> { project }
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
                    FileLocations = new List<string> { "Demo\\Domain\\Account.cs" }
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
                    FileLocations = new List<string> { "Demo\\Domain\\Order.cs" }
                }
            ],
            Projections = new List<ProjectionDefinition>()
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
                        GenericTypes: new List<PropertyGenericTypeDefinition>(),
                        SubTypes: new List<PropertyGenericTypeDefinition>())
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
            Aggregates = new List<AggregateDefinition> { aggregate },
            Projections = new List<ProjectionDefinition>
            {
                new ProjectionDefinition
                {
                    Name = "AccountsProjection",
                    Namespace = "Demo.App.Projections",
                    Constructors = new List<ConstructorDefinition>(),
                    Properties = new List<PropertyDefinition>(),
                    Events = new List<ProjectionEventDefinition>
                    {
                        new ProjectionEventDefinition
                        {
                            TypeName = "FeatureFlagEnabled",
                            Namespace = "Demo.App.Events",
                            EventName = "FeatureFlag.Enabled",
                            ActivationType = "When",
                            ActivationAwaitRequired = false,
                            Parameters = new List<ParameterDefinition>(),
                            Properties = new List<PropertyDefinition>()
                        }
                    },
                    FileLocations = new List<string> { "Demo\\Projections\\AccountsProjection.cs" }
                }
            }
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
                        GenericTypes: new List<PropertyGenericTypeDefinition>(),
                        SubTypes: new List<PropertyGenericTypeDefinition>())
                ],
                // Add a complex subtype with its own generic arguments to exercise nested generics
                SubTypes =
                [
                    new PropertyGenericTypeDefinition(
                        Name: "Dictionary",
                        Namespace: "System.Collections.Generic",
                        GenericTypes: new List<PropertyGenericTypeDefinition>
                        {
                            new PropertyGenericTypeDefinition(
                                Name: "String",
                                Namespace: "System",
                                GenericTypes: new List<PropertyGenericTypeDefinition>(),
                                SubTypes: new List<PropertyGenericTypeDefinition>()),
                            new PropertyGenericTypeDefinition(
                                Name: "Int32",
                                Namespace: "System",
                                GenericTypes: new List<PropertyGenericTypeDefinition>(),
                                SubTypes: new List<PropertyGenericTypeDefinition>())
                        },
                        SubTypes: new List<PropertyGenericTypeDefinition>())
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
            FileLocations = new List<string> { "Demo\\Domain\\Temp.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition> { partialAgg, nonPartialAgg },
            Projections = new List<ProjectionDefinition>()
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
}
