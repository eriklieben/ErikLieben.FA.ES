using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeManualStreamActionsTests
{
    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    private static AggregateDefinition CreateAggregate(
        string identifierName,
        string objectName,
        List<StreamActionDefinition>? streamActions = null) => new()
    {
        IdentifierName = identifierName,
        ObjectName = objectName,
        IdentifierType = "Guid",
        IdentifierTypeNamespace = "System",
        Namespace = "Test",
        StreamActions = streamActions ?? []
    };

    private static (ClassDeclarationSyntax ClassDecl, SemanticModel Model) GetClassAndModel(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var model = compilation.GetSemanticModel(tree);
        var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();
        return (classDecl, model);
    }

    public class RunMethod : AnalyzeManualStreamActionsTests
    {
        [Fact]
        public void Should_not_fail_on_empty_class()
        {
            // Arrange
            var code = """
                public class EmptyExtensions { }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            var aggregates = new List<AggregateDefinition>();
            var sut = new AnalyzeManualStreamActions(classDecl, model);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_not_fail_on_class_with_instance_methods()
        {
            // Arrange
            var code = """
                public class NonStaticExtensions
                {
                    public void SomeMethod() { }
                }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            var aggregates = new List<AggregateDefinition>();
            var sut = new AnalyzeManualStreamActions(classDecl, model);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_not_fail_on_class_with_static_methods_without_RegisterAction()
        {
            // Arrange
            var code = """
                public static class HelperExtensions
                {
                    public static void DoSomething() { }
                    public static int Calculate(int x) => x * 2;
                }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            var aggregates = new List<AggregateDefinition>();
            var sut = new AnalyzeManualStreamActions(classDecl, model);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_ignore_non_RegisterAction_member_access()
        {
            // Arrange
            var code = """
                public static class Extensions
                {
                    public static void Configure(object stream)
                    {
                        stream.ToString();
                    }
                }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            var aggregates = new List<AggregateDefinition>();
            var sut = new AnalyzeManualStreamActions(classDecl, model);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_handle_method_with_aggregate_naming_convention()
        {
            // Arrange
            var code = """
                public static class OrderExtensions
                {
                    public static void AddOrderActions(object stream)
                    {
                        // This method follows the naming convention Add{Aggregate}Actions
                    }
                }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Order", "Order") };
            var sut = new AnalyzeManualStreamActions(classDecl, model);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            Assert.Empty(aggregates[0].StreamActions);
        }

        [Fact]
        public void Should_skip_non_generic_RegisterAction_calls()
        {
            // Arrange - RegisterAction without generic type argument
            var code = """
                public class StreamMock
                {
                    public void RegisterAction() { }
                }

                public static class Extensions
                {
                    public static void Configure(StreamMock stream)
                    {
                        stream.RegisterAction();
                    }
                }
                """;
            var (classDecl, model) = GetClassAndModel(code);
            // Get the Extensions class specifically
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "Extensions");

            var aggregates = new List<AggregateDefinition>();
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_analyze_RegisterAction_with_generic_type()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class MyAction : IPostAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class Order { }

                public static class OrderExtensions
                {
                    public static void AddOrderActions(IEventStream<Order> stream)
                    {
                        stream.RegisterAction<MyAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "OrderExtensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Order", "Order") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            Assert.Single(aggregates[0].StreamActions);
            Assert.Equal("MyAction", aggregates[0].StreamActions[0].Type);
            Assert.Equal("Manual", aggregates[0].StreamActions[0].RegistrationType);
        }

        [Fact]
        public void Should_detect_stream_action_interfaces()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }
                public interface IPreAppendAction { }

                public class MultiInterfaceAction : IPostAppendAction, IPreAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class Order { }

                public static class OrderExtensions
                {
                    public static void AddOrderActions(IEventStream<Order> stream)
                    {
                        stream.RegisterAction<MultiInterfaceAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "OrderExtensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Order", "Order") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates[0].StreamActions);
            Assert.Contains("IPostAppendAction", aggregates[0].StreamActions[0].StreamActionInterfaces);
            Assert.Contains("IPreAppendAction", aggregates[0].StreamActions[0].StreamActionInterfaces);
        }

        [Fact]
        public void Should_not_duplicate_existing_action()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class ExistingAction : IPostAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class Order { }

                public static class OrderExtensions
                {
                    public static void AddOrderActions(IEventStream<Order> stream)
                    {
                        stream.RegisterAction<ExistingAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "OrderExtensions");
            var existingAction = new StreamActionDefinition
            {
                Type = "ExistingAction",
                Namespace = "",
                RegistrationType = "Attribute",
                StreamActionInterfaces = []
            };
            var aggregates = new List<AggregateDefinition>
            {
                CreateAggregate("Order", "Order", [existingAction])
            };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates[0].StreamActions);
            Assert.Equal("Attribute", aggregates[0].StreamActions[0].RegistrationType);
        }

        [Fact]
        public void Should_find_aggregate_by_ObjectName()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class OrderAction : IPostAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class OrderAggregate { }

                public static class Extensions
                {
                    public static void AddOrderAggregateActions(IEventStream<OrderAggregate> stream)
                    {
                        stream.RegisterAction<OrderAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "Extensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("OrderAgg", "OrderAggregate") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates[0].StreamActions);
        }

        [Fact]
        public void Should_handle_local_variable_stream()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class LocalAction : IPostAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class Customer { }

                public static class CustomerExtensions
                {
                    public static void Configure()
                    {
                        IEventStream<Customer> localStream = null!;
                        localStream.RegisterAction<LocalAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "CustomerExtensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Customer", "Customer") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates[0].StreamActions);
            Assert.Equal("LocalAction", aggregates[0].StreamActions[0].Type);
        }

        [Fact]
        public void Should_skip_when_aggregate_not_found()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class UnknownAction : IPostAppendAction { }

                public interface IEventStream<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class UnknownAggregate { }

                public static class Extensions
                {
                    public static void AddUnknownAggregateActions(IEventStream<UnknownAggregate> stream)
                    {
                        stream.RegisterAction<UnknownAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "Extensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Order", "Order") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates[0].StreamActions);
        }

        [Fact]
        public void Should_handle_IEventStreamBuilder_parameter()
        {
            // Arrange
            var code = """
                public interface IPostAppendAction { }

                public class BuilderAction : IPostAppendAction { }

                public interface IEventStreamBuilder<T>
                {
                    void RegisterAction<TAction>() where TAction : class;
                }

                public class Product { }

                public static class ProductExtensions
                {
                    public static void Configure(IEventStreamBuilder<Product> builder)
                    {
                        builder.RegisterAction<BuilderAction>();
                    }
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                [tree],
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            var semanticModel = compilation.GetSemanticModel(tree);
            var extensionsClass = tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == "ProductExtensions");
            var aggregates = new List<AggregateDefinition> { CreateAggregate("Product", "Product") };
            var sut = new AnalyzeManualStreamActions(extensionsClass, semanticModel);

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates[0].StreamActions);
            Assert.Equal("BuilderAction", aggregates[0].StreamActions[0].Type);
        }
    }
}
