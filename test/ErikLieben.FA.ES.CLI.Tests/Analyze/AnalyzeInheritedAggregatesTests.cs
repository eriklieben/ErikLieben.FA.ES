using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeInheritedAggregatesTests
{
    public class Ctor
    {
        [Fact]
        public void Should_throw_when_typeSymbol_is_null()
        {
            // Arrange
            var tree = CSharpSyntaxTree.ParseText("public class A{} ");
            var compilation = CSharpCompilation.Create(
                "Dummy",
                new[] { tree },
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var semanticModel = compilation.GetSemanticModel(tree);

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeInheritedAggregates(
                null!,
                semanticModel,
                "C\\Repo\\App\\"));

            // Assert
            Assert.Equal("typeSymbol", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_semanticModel_is_null()
        {
            // Arrange
            var (typeSymbol, _) = GetTypeSymbol("namespace X; public class A{} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeInheritedAggregates(
                typeSymbol!,
                null!,
                "C\\Repo\\App\\"));

            // Assert
            Assert.Equal("semanticModel", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_solutionRootPath_is_null()
        {
            // Arrange
            var (typeSymbol, semanticModel) = GetTypeSymbol("namespace X; public class A{} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeInheritedAggregates(
                typeSymbol!,
                semanticModel,
                null!));

            // Assert
            Assert.Equal("solutionRootPath", ex.ParamName);
        }
    }

    public class Run
    {
        [Fact]
        public void Should_not_add_when_type_is_not_inherited_aggregate()
        {
            // Arrange: directly inherits Aggregate (not an inherited aggregate per IsInheritedAggregate)
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEventStream {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){} } }
namespace App.Domain { public class Direct(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) { } }
";
            var baseRoot = Path.GetTempPath();
            var root = Path.Combine(baseRoot, "Repo", "App");
            var (symbol, semanticModel, compilation) = GetClassSymbol(
                code,
                filePath: Path.Combine(root, "Domain", "Direct.cs"));
            var sut = new AnalyzeInheritedAggregates(symbol!, semanticModel, root + Path.DirectorySeparatorChar);
            var list = new List<InheritedAggregateDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_add_inherited_and_collect_members_and_parent_info_and_identifier()
        {
            // Arrange: OrderBase derives Aggregate, Order derives OrderBase (inherited aggregate)
            var code = @"
using System; using System.Threading.Tasks; using System.Collections.Generic;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream {} public interface ILeasedSession { Task Append(object e); } public interface IObjectDocument {} public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ Stream = s; } protected ErikLieben.FA.ES.IEventStream Stream { get; } } }
namespace App.Domain {
  public class UserCreated : ErikLieben.FA.ES.IEvent {}
  public interface IService {}
  public class OrderBase(ErikLieben.FA.ES.IEventStream s, IService svc) : ErikLieben.FA.ES.Processors.Aggregate(s) { }
  public partial class Order(ErikLieben.FA.ES.IEventStream s, IService svc) : OrderBase(s, svc) {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
    public string Name { get; private set; }
    public Task Do() { return Stream.Session(ctx => ctx.Append(new UserCreated())); }
  }
}
namespace ErikLieben.FA.ES { public interface IEventStream { Task Session(System.Func<ILeasedSession, Task> f); } }
";
            var baseRoot2 = Path.GetTempPath();
            var root2 = Path.Combine(baseRoot2, "Repo", "App");
            var (symbol, semanticModel, compilation) = GetClassSymbol(
                code,
                filePath: Path.Combine(root2, "Domain", "Order.cs"));

            var sut = new AnalyzeInheritedAggregates(symbol!, semanticModel, root2 + Path.DirectorySeparatorChar);
            var list = new List<InheritedAggregateDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var agg = list.First();
            Assert.Equal("Order", agg.IdentifierName);
            Assert.Equal("App.Domain", agg.Namespace);
            // FileLocations relative
            Assert.Contains(agg.FileLocations, p => p.Replace("\\\\","\\").EndsWith("Domain\\Order.cs") || p.EndsWith("Order.cs"));
            // Constructors captured (should include IEventStream and IService)
            Assert.NotEmpty(agg.Constructors);
            Assert.Contains(agg.Constructors.First().Parameters, p => p.Type == "IEventStream");
            Assert.Contains(agg.Constructors.First().Parameters, p => p.Type == "IService");
            // Properties include Name
            Assert.Contains(agg.Properties, p => p.Name == "Name");
            // Commands include Do
            Assert.Contains(agg.Commands, c => c.CommandName == "Do");
            // Identifier type inferred from Metadata<Guid>
            Assert.Equal("Guid", agg.IdentifierType);
            Assert.Equal("System", agg.IdentifierTypeNamespace);
            // Parent info from OrderBase
            Assert.Equal("OrderBase", agg.InheritedIdentifierName);
            Assert.Equal("App.Domain", agg.InheritedNamespace);
            Assert.Equal("IOrderBase", agg.ParentInterface);
            Assert.Equal("App.Domain", agg.ParentInterfaceNamespace);
            // ObjectName based on parent type (lower camel of OrderBase)
            Assert.Equal("orderBase", agg.ObjectName);
        }

        [Fact]
        public void Should_keep_default_identifier_when_no_metadata_property()
        {
            // Arrange: inherited aggregate without Metadata property
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEventStream {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){} } }
namespace App.Domain { public class Base(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) { }
  public class Child(ErikLieben.FA.ES.IEventStream s) : Base(s) { public string Name { get; private set; } }
}
";
            var baseRoot3 = Path.GetTempPath();
            var root3 = Path.Combine(baseRoot3, "Repo", "App");
            var (symbol, semanticModel, compilation) = GetClassSymbol(
                code,
                filePath: Path.Combine(root3, "Domain", "Child.cs"));
            var sut = new AnalyzeInheritedAggregates(symbol!, semanticModel, root3 + Path.DirectorySeparatorChar);
            var list = new List<InheritedAggregateDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var agg = list.First();
            Assert.Equal("String", agg.IdentifierType);
            Assert.Equal("System", agg.IdentifierTypeNamespace);
        }
    }

    private static (INamedTypeSymbol?, SemanticModel) GetTypeSymbol(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "DummyAsm",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var classDecl = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        return (model.GetDeclaredSymbol(classDecl), model);
    }

    private static (INamedTypeSymbol?, SemanticModel, CSharpCompilation) GetClassSymbol(string code, string? filePath = "Test.cs")
    {
        var tree = string.IsNullOrWhiteSpace(filePath)
            ? CSharpSyntaxTree.ParseText(code)
            : SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), filePath);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var classDecls = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var classDecl = classDecls.Last();
        return (model.GetDeclaredSymbol(classDecl), model, compilation);
    }

    private static List<PortableExecutableReference> References { get; } = new()
    {
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    };
}
