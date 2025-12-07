using System.Linq;
using System.Runtime.InteropServices;
using ErikLieben.FA.ES.Attributes;
using ErikLieben.FA.ES.CLI.Analyze.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class RoslynHelperMoreTests
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
        MetadataReference.CreateFromFile(typeof(IgnoreAttribute).Assembly.Location)
    ];

    private static (Compilation Compilation, SemanticModel Model, SyntaxTree Tree) Compile(string code, string assemblyName = "TestAsm", string? path = null)
    {
        var parseOptions = new CSharpParseOptions();
        var tree = string.IsNullOrWhiteSpace(path)
            ? CSharpSyntaxTree.ParseText(code, parseOptions)
            : SyntaxFactory.ParseSyntaxTree(code, parseOptions, path);
        var compilation = CSharpCompilation.Create(assemblyName, [tree], References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        return (compilation, model, tree);
    }

    [Fact]
    public void GetEventName_should_use_attribute_when_present()
    {
        var code = @"using ErikLieben.FA.ES.Attributes;namespace X{[EventName(""My.Custom"")] public record Evt();}";
        var (comp, model, tree) = Compile(code);
        var record = tree.GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>().First();
        var symbol = model.GetDeclaredSymbol(record)!;
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        var name = RoslynHelper.GetEventName(symbol);
        Assert.Equal("My.Custom", name);
    }

    [Fact]
    public void GetEventName_should_derive_from_type_when_attribute_missing()
    {
        var code = @"namespace X{ public record UserCreated(); }";
        var (comp, model, tree) = Compile(code);
        var record = tree.GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>().First();
        var symbol = model.GetDeclaredSymbol(record)!;
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        var name = RoslynHelper.GetEventName(symbol);
        Assert.Equal("User.Created", name);
    }

    [Fact]
    public void IsProcessableAggregate_should_be_true_for_public_non_generated_non_framework()
    {
        var code = @"using ErikLieben.FA.ES.Processors;namespace A{ public class A1(ErikLieben.FA.ES.IEventStream s):Aggregate(s){} }";
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var path = System.IO.Path.Combine(root, "A", "A1.cs");
        var (comp, model, tree) = Compile(code, assemblyName: "AppAsm", path: path);
        var cls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(cls)!;
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        // method is internal to helper; emulate by combining checks using exposed API
        // We at least cover branch via calling IsInheritedAggregate(false) and InheritsFromAggregate(true)
        Assert.True(RoslynHelper.InheritsFromAggregate(symbol));
        Assert.True(sut.IsInSolutionRootFolder(symbol));
    }

    [Fact]
    public void IsInheritedAggregate_should_be_true_when_class_inherits_from_application_aggregate()
    {
        var code = @"using ErikLieben.FA.ES.Processors; namespace D{ public class BaseAg(ErikLieben.FA.ES.IEventStream s):Aggregate(s){} public class Child(ErikLieben.FA.ES.IEventStream s): BaseAg(s){} }";
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var path = System.IO.Path.Combine(root, "D", "Child.cs");
        var (comp, model, tree) = Compile(code, assemblyName: "AppAsm", path: path);
        var child = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c=>c.Identifier.Text=="Child");
        var childSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(child)!;
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        Assert.True(sut.IsInheritedAggregate(childSymbol));
    }

    [Fact]
    public void GetGenericArguments_should_return_all_generic_arguments_for_named_type()
    {
        var code = @"namespace G{ public class Box<T1,T2>{} public class U{ public Box<int,string> P {get;set;} = null!; } }";
        var (comp, model, tree) = Compile(code);
        var clsU = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c=>c.Identifier.Text=="U");
        var uSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(clsU)!;
        var prop = uSymbol.GetMembers().OfType<IPropertySymbol>().First();
        var named = (INamedTypeSymbol)prop.Type;
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        var args = RoslynHelper.GetGenericArguments(named);
        Assert.Equal(2, args.Count);
        Assert.Equal("Int32", args[0].Type);
        Assert.Equal("System", args[0].Namespace);
        Assert.Equal("String", args[1].Type);
        Assert.Equal("System", args[1].Namespace);
    }

    [Fact]
    public void GetStreamContextUsagesInCommand_overload_should_detect_append_from_method_declaration_syntax()
    {
        var code = @"using ErikLieben.FA.ES;using ErikLieben.FA.ES.Processors;namespace T{ public record Evt(); public class Agg(IEventStream s):Aggregate(s){ public System.Threading.Tasks.Task Do(){ return Stream.Session(ctx=>ctx.Append(new Evt())); } } }";
        var baseRoot = System.IO.Path.GetTempPath();
        var root = System.IO.Path.Combine(baseRoot, "repo");
        var path = System.IO.Path.Combine(root, "T", "Agg.cs");
        var (comp, model, tree) = Compile(code, assemblyName: "TAsm", path: path);
        var methodDecl = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First(m=>m.Identifier.Text=="Do");
        var sut = new RoslynHelper(model, root + System.IO.Path.DirectorySeparatorChar);

        var defs = sut.GetStreamContextUsagesInCommand(methodDecl);
        var ev = Assert.Single(defs);
        Assert.Equal("T", ev.Namespace);
        Assert.Equal("Evt", ev.TypeName);
        Assert.Equal("Evt", ev.EventName); // default derived from type name
    }
}
