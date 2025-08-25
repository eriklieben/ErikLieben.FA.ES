using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using ErikLieben.FA.ES.CLI.Configuration;
using ErikLieben.FA.ES.CLI.Model;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class GenerateInheritedAggregateCodeTests
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
    public async Task Generate_writes_factory_and_interface_and_resolves_constructor_dependencies()
    {
        // Arrange
        var inherited = new InheritedAggregateDefinition
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
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                        new ConstructorParameter { Name = "cache", Type = "ICacheService", Namespace = "Demo.App.Services", IsNullable = false }
                    ]
                }
            },
            Commands = new List<CommandDefinition>
            {
                new()
                {
                    ReturnType = new CommandReturnType { Namespace = "System.Threading.Tasks", Type = "Task" },
                    CommandName = "PlaceOrder",
                    RequiresAwait = true,
                    Parameters = new List<CommandParameter>
                    {
                        new()
                        {
                            Name = "orderId",
                            Type = "Guid",
                            Namespace = "System",
                            IsGeneric = false,
                            GenericTypes = new List<PropertyGenericTypeDefinition>()
                        }
                    }
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Order.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition>(),
            InheritedAggregates = new List<InheritedAggregateDefinition> { inherited },
            Projections = new List<ProjectionDefinition>()
        };

        var (solution, outDir) = BuildSolution(project);
        // Create target directory expected by the generator
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateInheritedAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Order.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // Factory interface and class
        Assert.Contains("public interface IOrderFactory : IAggregateFactory<Order, Guid>", code);
        Assert.Contains("public class OrderFactory : IOrderFactory", code);

        // ObjectName and GetObjectName
        Assert.Contains("public static string ObjectName => \"Order\";", code);
        Assert.Contains("public string GetObjectName()", code);

        // DI resolution line for non-IEventStream constructor params and ctor invocation
        Assert.Contains("var cache = serviceProvider.GetService(typeof(ICacheService)) as ICacheService;", code);
        Assert.Contains("return new Order(eventStream, cache!);", code);

        // Both Create overloads present
        Assert.Contains("public Order Create(IEventStream eventStream)", code);
        Assert.Contains("public Order Create(IObjectDocument document)", code);

        // Parent interface using and IOrder interface extending it
        Assert.Contains("using Demo.App.Domain;", code);
        Assert.Contains("public interface IOrder : Demo.App.Domain.IOrder", code);

        // Command signature appears in the IOrder interface
        Assert.Contains("Task PlaceOrder(Guid orderId)", code);
    }

    [Fact]
    public async Task Generate_writes_command_signatures_and_async_retrieval_methods()
    {
        // Arrange
        var inherited = new InheritedAggregateDefinition
        {
            InheritedIdentifierName = "CustomerBase",
            InheritedNamespace = "Demo.App.Domain",
            IdentifierName = "Customer",
            ObjectName = "Customer",
            IdentifierType = "Guid",
            IdentifierTypeNamespace = "System",
            Namespace = "Demo.App.Domain",
            ParentInterface = "Demo.App.Domain.ICustomer",
            ParentInterfaceNamespace = "Demo.App.Domain",
            Constructors = new List<ConstructorDefinition>
            {
                new()
                {
                    Parameters =
                    [
                        new ConstructorParameter { Name = "eventStream", Type = "IEventStream", Namespace = "ErikLieben.FA.ES", IsNullable = false },
                        new ConstructorParameter { Name = "svc", Type = "IReportingService", Namespace = "Demo.App.Services", IsNullable = false }
                    ]
                }
            },
            Commands = new List<CommandDefinition>
            {
                // Generic parameter example: IList<Guid> ids
                new()
                {
                    ReturnType = new CommandReturnType { Namespace = "System", Type = "bool" },
                    CommandName = "Activate",
                    RequiresAwait = false,
                    Parameters = new List<CommandParameter>
                    {
                        new()
                        {
                            Name = "ids",
                            Type = "IList",
                            Namespace = "System.Collections.Generic",
                            IsGeneric = true,
                            GenericTypes = new List<PropertyGenericTypeDefinition>
                            {
                                new PropertyGenericTypeDefinition(
                                    Name: "Guid",
                                    Namespace: "System",
                                    GenericTypes: new List<PropertyGenericTypeDefinition>(),
                                    SubTypes: new List<PropertyGenericTypeDefinition>())
                            }
                        }
                    }
                }
            },
            FileLocations = new List<string> { "Demo\\Domain\\Customer.cs" }
        };

        var project = new ProjectDefinition
        {
            Name = "Demo.App",
            Namespace = "Demo.App",
            FileLocation = "Demo.App.csproj",
            Aggregates = new List<AggregateDefinition>(),
            InheritedAggregates = new List<InheritedAggregateDefinition> { inherited },
            Projections = new List<ProjectionDefinition>()
        };

        var (solution, outDir) = BuildSolution(project);
        Directory.CreateDirectory(Path.Combine(outDir, "Demo", "Domain"));

        var sut = new GenerateInheritedAggregateCode(solution, new Config(), outDir);

        // Act
        await sut.Generate();

        // Assert
        var generatedPath = Path.Combine(outDir, "Demo", "Domain", "Customer.Generated.cs");
        Assert.True(File.Exists(generatedPath));
        var code = await File.ReadAllTextAsync(generatedPath);

        // ICustomer interface should include generic param signature
        Assert.Contains("bool Activate(IList<System.Guid> ids)", code);

        // Async retrieval methods should be present
        Assert.Contains("public async Task<Customer> CreateAsync(Guid id)", code);
        Assert.Contains("public async Task<Customer> GetAsync(Guid id)", code);
        Assert.Contains("public async Task<(Customer, IObjectDocument)> GetWithDocumentAsync(Guid id)", code);
        Assert.Contains("public async Task<Customer> GetFirstByDocumentTag(string tag)", code);
        Assert.Contains("public async Task<IEnumerable<Customer>> GetAllByDocumentTag(string tag)", code);
    }
}
