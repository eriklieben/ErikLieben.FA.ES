using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze.Helpers;

public class WhenMethodHelperTests
{
    private static readonly PortableExecutableReference[] References =
    [
        MetadataReference.CreateFromFile(
            System.IO.Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(
            System.IO.Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(
            System.IO.Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ErikLieben.FA.ES.Projections.Projection).Assembly.Location)
    ];

    private static (Compilation Compilation, SemanticModel Model, SyntaxTree Tree) Compile(string code, string assemblyName = "WAsm", string? path = null)
    {
        var parseOptions = new CSharpParseOptions();
        var tree = string.IsNullOrWhiteSpace(path)
            ? CSharpSyntaxTree.ParseText(code, parseOptions)
            : SyntaxFactory.ParseSyntaxTree(code, parseOptions, path);
        var compilation = CSharpCompilation.Create(assemblyName, new[] { tree }, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        return (compilation, model, tree);
    }

    [Fact]
    public void Should_detect_event_from_simple_When_method_and_collect_metadata()
    {
        var code = "using System.Collections.Generic; using ErikLieben.FA.ES.Attributes; using ErikLieben.FA.ES; using ErikLieben.FA.ES.Projections; namespace P { [EventName(\"User.Created\")] public record UserCreated(string Name, int Age); public class MyFactory1 : IProjectionWhenParameterValueFactory<string, UserCreated> { public string Create(ErikLieben.FA.ES.Documents.IObjectDocument d, IEvent<UserCreated> e) => e.Data().Name; } public class MyFactory2 : IProjectionWhenParameterValueFactory<int> { public int Create(ErikLieben.FA.ES.Documents.IObjectDocument d, IEvent e) => 0; } public abstract class MyProjection : Projection { [WhenParameterValueFactory<MyFactory1>] [WhenParameterValueFactory<MyFactory2>] private void When(UserCreated e, List<string> tags) { var x = e.Age; } } }";
        var (comp, model, tree) = Compile(code, path: "c:\\repo\\P\\MyProjection.cs");
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "MyProjection");
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(cls)!;
        var sutRoslyn = new RoslynHelper(model, "c:\\repo\\");

        var defs = WhenMethodHelper.GetEventDefinitions(classSymbol, comp, sutRoslyn);

        var ev = Assert.Single(defs);
        Assert.Equal("UserCreated", ev.TypeName);
        Assert.Equal("P", ev.Namespace);
        Assert.Equal("User.Created", ev.EventName);
        Assert.False(ev.ActivationAwaitRequired); // void return
        Assert.Equal("P\\\\MyProjection.cs".Replace("\\\\","\\"), ev.File.Replace("/","\\"));
        // Extra parameter captured
        Assert.Single(ev.WhenParameterDeclarations);
        Assert.Equal("tags", ev.WhenParameterDeclarations[0].Name);
        Assert.Equal("List", ev.WhenParameterDeclarations[0].Type);
        Assert.Equal("System.Collections.Generic", ev.WhenParameterDeclarations[0].Namespace);
        Assert.Single(ev.WhenParameterDeclarations[0].GenericArguments);
        // WhenParameterValueFactories
        Assert.Equal(2, ev.WhenParameterValueFactories.Count);
        Assert.Contains(ev.WhenParameterValueFactories, f => f.EventType == "P.UserCreated" && f.ForType.Type == "string");
        Assert.Contains(ev.WhenParameterValueFactories, f => string.IsNullOrEmpty(f.EventType) && f.ForType.Type == "int");
    }

    [Fact]
    public void Should_handle_generic_IEvent_parameter_and_skip_IExecutionContextWithEvent_and_detect_awaitable()
    {
        var code = "using System.Threading.Tasks; using ErikLieben.FA.ES; using ErikLieben.FA.ES.Projections; namespace P2 { public record OrderPlaced(int Amount); public abstract class MyProjection : Projection { private Task When(IEvent<OrderPlaced> e) { return Task.CompletedTask; } private void When(IExecutionContextWithEvent<OrderPlaced> ctx) {} } }";
        var (comp, model, tree) = Compile(code, path: "c:\\repo\\P2\\MyProjection.cs");
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "MyProjection");
        var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(cls)!;
        var sutRoslyn = new RoslynHelper(model, "c:\\repo\\");

        var defs = WhenMethodHelper.GetEventDefinitions(classSymbol, comp, sutRoslyn);

        var ev = Assert.Single(defs); // ctx version is skipped
        Assert.Equal("OrderPlaced", ev.TypeName); // unwrap IEvent<T>
        Assert.Equal("ErikLieben.FA.ES", ev.Namespace);
        Assert.True(ev.ActivationAwaitRequired); // Task-returning method
    }
}
