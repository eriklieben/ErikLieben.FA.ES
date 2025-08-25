using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeAggregatesTests
{
    public class Ctor
    {
        [Fact]
        public void Should_throw_when_classSymbol_is_null()
        {
            // Arrange
            var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
            var semanticModel = GetSemanticModel();
            var compilation = semanticModel.Compilation;
            var solutionRootPath = @"C:\\Repo\\App\\";

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeAggregates(
                null!,
                classDeclaration,
                semanticModel,
                compilation,
                solutionRootPath));

            // Assert
            Assert.Equal("classSymbol", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_classDeclaration_is_null()
        {
            // Arrange
            var (classSymbol, _, semanticModel, compilation) = GetFromCode("public class A {} ");
            var solutionRootPath = @"C:\\Repo\\App\\";

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeAggregates(
                classSymbol!,
                null!,
                semanticModel,
                compilation!,
                solutionRootPath));

            // Assert
            Assert.Equal("classDeclaration", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_semanticModel_is_null()
        {
            // Arrange
            var (classSymbol, classDeclaration, _, compilation) = GetFromCode("public class A {} ");
            var solutionRootPath = @"C:\\Repo\\App\\";

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeAggregates(
                classSymbol!,
                classDeclaration!,
                null!,
                compilation!,
                solutionRootPath));

            // Assert
            Assert.Equal("semanticModel", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_compilation_is_null()
        {
            // Arrange
            var (classSymbol, classDeclaration, semanticModel, _) = GetFromCode("public class A {} ");
            var solutionRootPath = @"C:\\Repo\\App\\";

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeAggregates(
                classSymbol!,
                classDeclaration!,
                semanticModel,
                null!,
                solutionRootPath));

            // Assert
            Assert.Equal("compilation", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_solutionRootPath_is_null()
        {
            // Arrange
            var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode("public class A {} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeAggregates(
                classSymbol!,
                classDeclaration!,
                semanticModel,
                compilation!,
                null!));

            // Assert
            Assert.Equal("solutionRootPath", ex.ParamName);
        }
    }

    public class Run
    {
        [Fact]
        public void Should_not_add_aggregate_when_type_is_not_processable()
        {
            // Arrange: simple class that does NOT inherit Aggregate
            var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
                """
                namespace App.Domain;
                public class NotAnAggregate { }
                """,
                filePath: @"C:\\Repo\\App\\Domain\\NotAnAggregate.cs");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, @"C\\Repo\\App\\");
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Empty(aggregates);
        }

        [Fact]
        public void Should_add_aggregate_and_collect_events_commands_properties_constructors_and_postwhen_and_stream_actions_and_identifier()
        {
            // Arrange: Define minimal framework stubs and an aggregate deriving from ErikLieben.FA.ES.Processors.Aggregate
            var code = @"
using System; using System.Collections.Generic; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { }
  public interface ILeasedSession { Task Append(object evt); }
  public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Attributes { public class IgnoreAttribute : System.Attribute { public IgnoreAttribute(string m){ } } }
namespace ErikLieben.FA.ES.Processors {
  public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream stream){ Stream = stream; }
    protected ErikLieben.FA.ES.IEventStream Stream { get; }
  }
}
namespace ErikLieben.FA.ES { public interface IAsyncPostCommitAction {} public interface IPostAppendAction {} public interface IPostReadAction {} public interface IPreAppendAction {} public interface IPreReadAction {}
  public interface IEventStream { System.Threading.Tasks.Task Session(System.Func<ILeasedSession, System.Threading.Tasks.Task> f); }
}
namespace App.Domain {
  public class AppendAction : ErikLieben.FA.ES.IPostAppendAction {}
  public class UsesActionsAttribute<T> : System.Attribute { }
  [UsesActionsAttribute<AppendAction>]
  public partial class Account(ErikLieben.FA.ES.IEventStream stream) : ErikLieben.FA.ES.Processors.Aggregate(stream)
  {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
    public string Name { get; private set; }
    public void When(UserCreated e) { }
    public System.Threading.Tasks.Task DoSomething() { return Stream.Session(ctx => { return ctx.Append(new UserCreated()); }); }
    public System.Threading.Tasks.Task PostWhen(ErikLieben.FA.ES.IObjectDocument document, ErikLieben.FA.ES.IEvent @event) => System.Threading.Tasks.Task.CompletedTask;
  }
  public record UserCreated(string FirstName);
}
";
            var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\\Repo\\App\\Domain\\Account.cs");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, @"C:\\Repo\\App\\");
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.Equal("Account", agg.IdentifierName);
            Assert.Equal("account", agg.ObjectName);
            Assert.Equal("App.Domain", agg.Namespace);
            Assert.True(agg.IsPartialClass);
            Assert.Contains(agg.FileLocations, f => f.Replace("\\\\", "\\").EndsWith("Domain\\Account.cs") || f.EndsWith("Account.cs"));

            // Constructors
            Assert.NotEmpty(agg.Constructors);
            // Properties
            Assert.Contains(agg.Properties, p => p.Name == "Name");
            // Events
            Assert.Single(agg.Events);
            var ev = agg.Events.First();
            Assert.Equal("User.Created", ev.EventName);
            Assert.Equal("UserCreated", ev.TypeName);
            Assert.Equal("App.Domain", ev.Namespace);
            // Commands
            Assert.Single(agg.Commands);
            Assert.Equal("DoSomething", agg.Commands.First().CommandName);
            // PostWhen
            Assert.NotNull(agg.PostWhen);
            Assert.Collection(agg.PostWhen!.Parameters,
                p => { Assert.Equal("IObjectDocument", p.Type); },
                p => { Assert.Equal("IEvent", p.Type); });
            // Stream actions from attribute generic type
            Assert.Single(agg.StreamActions);
            var sa = agg.StreamActions.First();
            Assert.Equal("AppendAction", sa.Type);
            Assert.Contains("IPostAppendAction", sa.StreamActionInterfaces);
            // Identifier from ObjectMetadata<Guid>
            Assert.Equal("Guid", agg.IdentifierType);
            Assert.Equal("System", agg.IdentifierTypeNamespace);
        }

        [Fact]
        public void Should_not_duplicate_members_when_Run_called_twice()
        {
            // Arrange
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { } public interface ILeasedSession { Task Append(object evt); } public class ObjectMetadata<T> {} public interface IObjectDocument {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ } protected ErikLieben.FA.ES.IEventStream Stream { get; } }
}
namespace App.Domain { public partial class A(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) { public void When(E e){} } public record E(); }
";
            var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: @"C:\\Repo\\App\\Domain\\A.cs");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, @"C:\\Repo\\App\\");
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.Single(agg.Events);
        }
    }

    private static (INamedTypeSymbol?, ClassDeclarationSyntax?, SemanticModel, CSharpCompilation?) GetFromCode(
        string code,
        string testAssembly = "TestAssembly",
        string? filePath = "Test.cs")
    {
        SyntaxTree syntaxTree = string.IsNullOrWhiteSpace(filePath)
            ? CSharpSyntaxTree.ParseText(code)
            : SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), filePath);

        var compilation = CSharpCompilation.Create(
            testAssembly,
            new[] { syntaxTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classNode = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last();

        return (semanticModel.GetDeclaredSymbol(classNode), classNode, semanticModel, compilation);
    }

    private static SemanticModel GetSemanticModel()
    {
        var tree = CSharpSyntaxTree.ParseText("public class A {} ");
        var compilation = CSharpCompilation.Create(
            "Dummy",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        return compilation.GetSemanticModel(tree);
    }

    private static List<PortableExecutableReference> References { get; } = new()
    {
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    };
}
