#pragma warning disable CS8602 // Dereference of a possibly null reference - test assertions handle null checks
#pragma warning disable CS8604 // Possible null reference argument - test data is always valid
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - testing null scenarios

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
                filePath: Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "NotAnAggregate.cs"));
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, root + Path.DirectorySeparatorChar);
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
                filePath: Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Account.cs"));
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, root + Path.DirectorySeparatorChar);
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
                filePath: Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "A.cs"));
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.Single(agg.Events);
        }

        [Fact]
        public void Should_merge_command_events_without_when_handlers_into_events_list()
        {
            // Arrange: Aggregate with a command that appends an event, but NO When handler for that event
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { }
  public interface ILeasedSession { Task Append(object evt); }
  public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors {
  public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream stream){ Stream = stream; }
    protected ErikLieben.FA.ES.IEventStream Stream { get; }
  }
}
namespace ErikLieben.FA.ES { public interface IEventStream { System.Threading.Tasks.Task Session(System.Func<ILeasedSession, System.Threading.Tasks.Task> f); } }
namespace App.Domain {
  public partial class Project(ErikLieben.FA.ES.IEventStream stream) : ErikLieben.FA.ES.Processors.Aggregate(stream)
  {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
    // Obsolete command that appends legacy event
    [System.Obsolete(""Use CompleteProjectSuccessfully instead"")]
    public System.Threading.Tasks.Task CompleteProject() {
      return Stream.Session(ctx => { return ctx.Append(new ProjectCompleted()); });
    }
    // New command with When handler
    public System.Threading.Tasks.Task CompleteProjectSuccessfully() {
      return Stream.Session(ctx => { return ctx.Append(new ProjectCompletedSuccessfully()); });
    }
    // When handler ONLY for new event
    public void When(ProjectCompletedSuccessfully e) { }
  }
  public record ProjectCompleted();  // Legacy event, no When handler
  public record ProjectCompletedSuccessfully();  // New event, has When handler
}
";
            var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Project.cs"));
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classDeclaration!, semanticModel, compilation!, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.Equal(2, agg.Events.Count);

            // Both events should be in the events list
            var legacyEvent = agg.Events.FirstOrDefault(e => e.TypeName == "ProjectCompleted");
            Assert.NotNull(legacyEvent);
            Assert.Equal("Command", legacyEvent!.ActivationType); // Comes from command, not When

            var newEvent = agg.Events.FirstOrDefault(e => e.TypeName == "ProjectCompletedSuccessfully");
            Assert.NotNull(newEvent);
            Assert.Equal("When", newEvent.ActivationType); // Has When handler

            // Both commands should be detected
            Assert.Equal(2, agg.Commands.Count);
            var legacyCommand = agg.Commands.FirstOrDefault(c => c.CommandName == "CompleteProject");
            Assert.NotNull(legacyCommand);
            Assert.Single(legacyCommand!.ProducesEvents);
            Assert.Equal("ProjectCompleted", legacyCommand.ProducesEvents.First().TypeName);

            var newCommand = agg.Commands.FirstOrDefault(c => c.CommandName == "CompleteProjectSuccessfully");
            Assert.NotNull(newCommand);
            Assert.Single(newCommand!.ProducesEvents);
            Assert.Equal("ProjectCompletedSuccessfully", newCommand.ProducesEvents.First().TypeName);
        }

        [Fact]
        public void Should_detect_user_defined_factory_partial()
        {
            // Arrange: Aggregate with a user-defined partial factory class
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { } public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ } } }
namespace App.Domain {
  public partial class UserProfile(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
  }
  // User-defined partial factory in same namespace
  public partial class UserProfileFactory {
    public async Task<UserProfile> CreateWithEmailAsync(string email) { return null; }
  }
}
";
            var trees = new[] {
                SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "UserProfile.cs"))
            };
            var compilation = CSharpCompilation.Create("TestAssembly", trees, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var aggregateTree = trees[0];
            var semanticModel = compilation.GetSemanticModel(aggregateTree);
            var classNode = aggregateTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "UserProfile");
            var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classNode, semanticModel, compilation, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.True(agg.HasUserDefinedFactoryPartial, "Should detect user-defined partial factory");
        }

        [Fact]
        public void Should_not_detect_factory_partial_when_only_generated_exists()
        {
            // Arrange: Aggregate WITHOUT user-defined factory partial
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { } public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ Stream = s; } protected ErikLieben.FA.ES.IEventStream Stream { get; } } }
namespace App.Domain {
  public partial class Product(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
  }
}
";
            var trees = new[] {
                SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Product.cs")),
                // Simulate generated factory file
                SyntaxFactory.ParseSyntaxTree("namespace App.Domain { public partial class ProductFactory { } }", new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Product.Generated.cs"))
            };
            var compilation = CSharpCompilation.Create("TestAssembly", trees, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var aggregateTree = trees[0];
            var semanticModel = compilation.GetSemanticModel(aggregateTree);
            var classNode = aggregateTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Product");
            var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classNode, semanticModel, compilation, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.False(agg.HasUserDefinedFactoryPartial, "Should NOT detect factory partial when only generated file exists");
        }

        [Fact]
        public void Should_detect_user_defined_repository_partial()
        {
            // Arrange: Aggregate with a user-defined partial repository class
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { } public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ } } }
namespace App.Domain {
  public partial class UserProfile(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
  }
  // User-defined partial repository in same namespace
  public partial class UserProfileRepository {
    public async Task<UserProfile> GetByEmailAsync(string email) { return null; }
  }
}
";
            var trees = new[] {
                SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "UserProfile.cs"))
            };
            var compilation = CSharpCompilation.Create("TestAssembly", trees, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var aggregateTree = trees[0];
            var semanticModel = compilation.GetSemanticModel(aggregateTree);
            var classNode = aggregateTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "UserProfile");
            var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classNode, semanticModel, compilation, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.True(agg.HasUserDefinedRepositoryPartial, "Should detect user-defined partial repository");
        }

        [Fact]
        public void Should_not_detect_repository_partial_when_only_generated_exists()
        {
            // Arrange: Aggregate WITHOUT user-defined repository partial
            var code = @"
using System; using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} public interface IEventStream { } public interface IObjectDocument { } public class ObjectMetadata<T> {} }
namespace ErikLieben.FA.ES.Processors { public abstract class Aggregate { protected Aggregate(ErikLieben.FA.ES.IEventStream s){ Stream = s; } protected ErikLieben.FA.ES.IEventStream Stream { get; } } }
namespace App.Domain {
  public partial class Product(ErikLieben.FA.ES.IEventStream s) : ErikLieben.FA.ES.Processors.Aggregate(s) {
    public ErikLieben.FA.ES.ObjectMetadata<Guid>? Metadata { get; private set; }
  }
}
";
            var trees = new[] {
                SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Product.cs")),
                // Simulate generated repository file
                SyntaxFactory.ParseSyntaxTree("namespace App.Domain { public partial class ProductRepository { } }", new CSharpParseOptions(), Path.Combine(Path.GetTempPath(), "Repo", "App", "Domain", "Product.Generated.cs"))
            };
            var compilation = CSharpCompilation.Create("TestAssembly", trees, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var aggregateTree = trees[0];
            var semanticModel = compilation.GetSemanticModel(aggregateTree);
            var classNode = aggregateTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == "Product");
            var classSymbol = semanticModel.GetDeclaredSymbol(classNode);
            var root = Path.Combine(Path.GetTempPath(), "Repo", "App");
            var sut = new AnalyzeAggregates(classSymbol!, classNode, semanticModel, compilation, root + Path.DirectorySeparatorChar);
            var aggregates = new List<AggregateDefinition>();

            // Act
            sut.Run(aggregates);

            // Assert
            Assert.Single(aggregates);
            var agg = aggregates.First();
            Assert.False(agg.HasUserDefinedRepositoryPartial, "Should NOT detect repository partial when only generated file exists");
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
            [syntaxTree],
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
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        return compilation.GetSemanticModel(tree);
    }

    private static List<PortableExecutableReference> References { get; } =
    [
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
        MetadataReference.CreateFromFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(),
            "System.Collections.dll")),
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];
}
