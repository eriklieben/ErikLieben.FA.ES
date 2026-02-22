using System.Collections.Generic;
using System.IO;
using System.Linq;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class CommandHelperTests
{
    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(
            Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ErikLieben.FA.ES.Attributes.IgnoreAttribute).Assembly.Location)
    ];

    private static (INamedTypeSymbol?, SemanticModel, CSharpCompilation) GetClassSymbol(string code, string assembly="TestAssembly")
    {
        var syntaxTree = SyntaxFactory.ParseSyntaxTree(code,
            new CSharpParseOptions(),
            "c\\repo\\MyAggregate.cs");
        var compilation = CSharpCompilation.Create(
            assembly,
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classNode = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (semanticModel.GetDeclaredSymbol(classNode), semanticModel, compilation);
    }

    [Fact]
    public void Should_ignore_non_public_and_When_or_ctor_methods()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            public record SomeEvent();

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                private void Hidden() {}
                internal void AlsoHidden() {}
                public void When(SomeEvent e) {}
                public MyAggregate() : this(null!) {}
                public Task Perform() { return Task.CompletedTask; }
            }
            """
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        Assert.Empty(commands);
    }

    [Fact]
    public void Should_detect_produced_event_and_require_await_for_async()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            namespace TestDomain;

            public record SomeEvent(string Name);

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task DoWork(string name)
                {
                    await Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
                }
            }
            """,
            assembly: "TestDomainAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("DoWork", cmd.CommandName);
        Assert.True(cmd.RequiresAwait);

        // Parameters
        var param = Assert.Single(cmd.Parameters);
        Assert.Equal("name", param.Name);
        Assert.Equal("String", param.Type);
        Assert.Equal("System", param.Namespace);

        // Return type
        Assert.Equal("Task", cmd.ReturnType.Type);

        // Produced events
        var produced = Assert.Single(cmd.ProducesEvents);
        Assert.Equal("Some.Event", produced.EventName);
        Assert.Equal("TestDomain", produced.Namespace);
        Assert.Equal("SomeEvent", produced.TypeName);
    }

    [Fact]
    public void Should_ignore_command_without_stream_context_usage()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public Task DoNothing() { return Task.CompletedTask; }
            }
            """
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        Assert.Empty(commands);
    }

    [Fact]
    public void Should_handle_array_parameters_correctly()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            namespace TestDomain;

            public record ItemsAdded(string[] Names);

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task AddItems(string[] items)
                {
                    await Stream.Session(ctx => ctx.Append(new ItemsAdded(items)));
                }
            }
            """,
            assembly: "TestArrayAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("AddItems", cmd.CommandName);

        // Parameters should include array notation
        var param = Assert.Single(cmd.Parameters);
        Assert.Equal("items", param.Name);
        Assert.Equal("String[]", param.Type);
        // Array types don't have their own namespace - it's empty
        Assert.Equal(string.Empty, param.Namespace);
    }

    [Fact]
    public void Should_handle_generic_parameters_with_arrays()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;
            using System.Collections.Generic;

            namespace TestDomain;

            public record DataProcessed(List<string[]> Data);

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task ProcessData(List<string[]> data)
                {
                    await Stream.Session(ctx => ctx.Append(new DataProcessed(data)));
                }
            }
            """,
            assembly: "TestGenericArrayAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("ProcessData", cmd.CommandName);

        // Parameters should include full generic type with array notation
        var param = Assert.Single(cmd.Parameters);
        Assert.Equal("data", param.Name);
        Assert.Equal("List<String[]>", param.Type);
    }
}
