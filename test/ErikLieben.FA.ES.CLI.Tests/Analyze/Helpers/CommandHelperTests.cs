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
    public void Should_detect_event_from_variable_reference()
    {
        // Arrange — matches the pattern:
        //   var @event = new SomeEvent { ... };
        //   await Stream.Session(context => Fold(context.Append(@event)));
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            namespace TestDomain;

            public record UserDirectoryRemoved(string RemovedBy);

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task RemoveUserDirectory(string removedBy)
                {
                    var @event = new UserDirectoryRemoved(removedBy);
                    await Stream.Session(context => context.Append(@event));
                }
            }
            """,
            assembly: "TestVarRefAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("RemoveUserDirectory", cmd.CommandName);
        Assert.True(cmd.RequiresAwait);

        var produced = Assert.Single(cmd.ProducesEvents);
        Assert.Equal("User.Directory.Removed", produced.EventName);
        Assert.Equal("TestDomain", produced.Namespace);
        Assert.Equal("UserDirectoryRemoved", produced.TypeName);
    }

    [Fact]
    public void Should_detect_multiple_events_from_variable_references()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            namespace TestDomain;

            public record FirstEvent();
            public record SecondEvent();

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task DoMultiple()
                {
                    var evt1 = new FirstEvent();
                    var evt2 = new SecondEvent();
                    await Stream.Session(context => { context.Append(evt1); context.Append(evt2); });
                }
            }
            """,
            assembly: "TestMultiVarAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("DoMultiple", cmd.CommandName);
        Assert.Equal(2, cmd.ProducesEvents.Count);
        Assert.Contains(cmd.ProducesEvents, e => e.TypeName == "FirstEvent");
        Assert.Contains(cmd.ProducesEvents, e => e.TypeName == "SecondEvent");
    }

    [Fact]
    public void Should_detect_mixed_inline_and_variable_reference_events()
    {
        // Arrange
        var (classSymbol, semanticModel, _) = GetClassSymbol(
            """
            using ErikLieben.FA.ES;
            using ErikLieben.FA.ES.Processors;
            using System.Threading.Tasks;

            namespace TestDomain;

            public record InlineEvent();
            public record VariableEvent();

            public class MyAggregate(IEventStream stream) : Aggregate(stream)
            {
                public async Task DoMixed()
                {
                    var evt = new VariableEvent();
                    await Stream.Session(context => { context.Append(new InlineEvent()); context.Append(evt); });
                }
            }
            """,
            assembly: "TestMixedAsm"
        );
        Assert.NotNull(classSymbol);
        var roslyn = new RoslynHelper(semanticModel, "c:\\repo\\");

        // Act
        var commands = CommandHelper.GetCommandMethods(classSymbol!, roslyn);

        // Assert
        var cmd = Assert.Single(commands);
        Assert.Equal("DoMixed", cmd.CommandName);
        Assert.Equal(2, cmd.ProducesEvents.Count);
        Assert.Contains(cmd.ProducesEvents, e => e.TypeName == "InlineEvent");
        Assert.Contains(cmd.ProducesEvents, e => e.TypeName == "VariableEvent");
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
