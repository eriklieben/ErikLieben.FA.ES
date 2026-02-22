using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ErikLieben.FA.ES.CLI.Analyze;
using ErikLieben.FA.ES.CLI.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Analyze;

public class AnalyzeProjectionsTests
{
    public class Ctor
    {
        [Fact]
        public void Should_throw_when_classSymbol_is_null()
        {
            // Arrange
            var (semanticModel, compilation) = GetSemanticModel("public class A {} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeProjections(
                null!,
                semanticModel,
                compilation!,
                "C:\\Repo\\App\\"));

            // Assert
            Assert.Equal("classSymbol", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_semanticModel_is_null()
        {
            // Arrange
            var (classSymbol, _, _, compilation) = GetFromCode("public class A {} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeProjections(
                classSymbol!,
                null!,
                compilation!,
                "C:\\Repo\\App\\"));

            // Assert
            Assert.Equal("semanticModel", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_compilation_is_null()
        {
            // Arrange
            var (classSymbol, _, semanticModel, _) = GetFromCode("public class A {} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeProjections(
                classSymbol!,
                semanticModel,
                null!,
                "C:\\Repo\\App\\"));

            // Assert
            Assert.Equal("compilation", ex.ParamName);
        }

        [Fact]
        public void Should_throw_when_solutionRootPath_is_null()
        {
            // Arrange
            var (classSymbol, _, semanticModel, compilation) = GetFromCode("public class A {} ");

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new AnalyzeProjections(
                classSymbol!,
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
        public void Should_not_add_projection_when_type_is_not_projection()
        {
            // Arrange
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                "namespace App.Projections; public class NotAProjection { }",
                filePath: "C:\\Repo\\App\\Projections\\NotAProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_not_add_projection_when_assembly_starts_with_framework_prefix()
        {
            // Arrange: compile under an assembly name that starts with ErikLieben.FA.ES
            const string code = @"namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }\nnamespace App.Projections { public class P : ErikLieben.FA.ES.Projections.Projection {} }";
            var syntaxTree = CSharpSyntaxTree.ParseText(code, path: "C\\\\Repo\\\\App\\\\Projections\\\\P.cs");
            var compilation = CSharpCompilation.Create(
                assemblyName: "ErikLieben.FA.ES.Framework",
                syntaxTrees: [syntaxTree],
                references: References,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var classDecl = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);

            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Should_collect_projection_with_events_properties_ctors_and_postwhen_and_flags()
        {
            // Arrange: Define Projection base, attributes, event, and projection class
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} }
namespace ErikLieben.FA.ES.Documents { public interface IObjectDocument {} }
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { public virtual Task PostWhenAll() => Task.CompletedTask; } }
public class ProjectionWithExternalCheckpointAttribute : System.Attribute {}
public class BlobJsonProjectionAttribute : System.Attribute { public BlobJsonProjectionAttribute(string container) { } public string Connection { get; set; } }
namespace App.Events { public class UserCreated : ErikLieben.FA.ES.IEvent {} }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections; using ErikLieben.FA.ES; using ErikLieben.FA.ES.Documents; using App.Events;
    [ProjectionWithExternalCheckpoint]
    [BlobJsonProjection(""container"", Connection=""conn"")] 
    public class Accounts : Projection {
        public string Name { get; private set; }
        public Accounts() { }
        public void When(UserCreated e, IObjectDocument document) { }
        public System.Threading.Tasks.Task PostWhen(IObjectDocument document, IEvent @event) => System.Threading.Tasks.Task.CompletedTask;
        public void PostWhenAll() { }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\Accounts.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var p = list.First();
            Assert.Equal("Accounts", p.Name);
            Assert.Equal("App.Projections", p.Namespace);
            Assert.True(p.ExternalCheckpoint);
            Assert.NotNull(p.BlobProjection);
            Assert.Equal("container", p.BlobProjection!.Container);
            Assert.Equal("conn", p.BlobProjection!.Connection);
            Assert.Contains(p.FileLocations, f => f.EndsWith("Projections\\Accounts.cs"));

            Assert.NotEmpty(p.Constructors);
            Assert.Contains(p.Properties, pr => pr.Name == "Name");
            Assert.Single(p.Events);
            var ev = p.Events.First();
            Assert.Equal("User.Created", ev.EventName);
            Assert.Equal("UserCreated", ev.TypeName);
            Assert.Equal("App.Events", ev.Namespace);

            Assert.NotNull(p.PostWhen);
            Assert.True(p.HasPostWhenAllMethod);
        }

        [Fact]
        public void Should_collect_projection_with_cosmosdb_projection_attribute()
        {
            // Arrange: Define Projection base, CosmosDbJsonProjection attribute, and projection class
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} }
namespace ErikLieben.FA.ES.Documents { public interface IObjectDocument {} }
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { public virtual Task PostWhenAll() => Task.CompletedTask; } }
public class ProjectionWithExternalCheckpointAttribute : System.Attribute {}
public class CosmosDbJsonProjectionAttribute : System.Attribute {
    public CosmosDbJsonProjectionAttribute(string container) { Container = container; }
    public string Container { get; }
    public string Connection { get; set; }
    public string PartitionKeyPath { get; set; } = ""/projectionName"";
}
namespace App.Events { public class UserCreated : ErikLieben.FA.ES.IEvent {} }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections; using ErikLieben.FA.ES; using ErikLieben.FA.ES.Documents; using App.Events;
    [ProjectionWithExternalCheckpoint]
    [CosmosDbJsonProjection(""projections"", Connection=""cosmosdb"", PartitionKeyPath=""/customKey"")]
    public class SprintDashboard : Projection {
        public string Name { get; private set; }
        public SprintDashboard() { }
        public void When(UserCreated e, IObjectDocument document) { }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\SprintDashboard.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var p = list.First();
            Assert.Equal("SprintDashboard", p.Name);
            Assert.Equal("App.Projections", p.Namespace);
            Assert.True(p.ExternalCheckpoint);

            // Verify CosmosDB projection settings
            Assert.NotNull(p.CosmosDbProjection);
            Assert.Equal("projections", p.CosmosDbProjection!.Container);
            Assert.Equal("cosmosdb", p.CosmosDbProjection!.Connection);
            Assert.Equal("/customKey", p.CosmosDbProjection!.PartitionKeyPath);

            // BlobProjection should be null
            Assert.Null(p.BlobProjection);
        }

        [Fact]
        public void Should_use_default_partition_key_path_when_not_specified()
        {
            // Arrange: Define Projection base with CosmosDbJsonProjection without custom partition key
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES { public interface IEvent {} }
namespace ErikLieben.FA.ES.Documents { public interface IObjectDocument {} }
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
public class CosmosDbJsonProjectionAttribute : System.Attribute {
    public CosmosDbJsonProjectionAttribute(string container) { Container = container; }
    public string Container { get; }
    public string Connection { get; set; }
    public string PartitionKeyPath { get; set; } = ""/projectionName"";
}
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    [CosmosDbJsonProjection(""my-container"", Connection=""myconn"")]
    public class SimpleProjection : Projection { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\SimpleProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var p = list.First();
            Assert.NotNull(p.CosmosDbProjection);
            Assert.Equal("my-container", p.CosmosDbProjection!.Container);
            Assert.Equal("myconn", p.CosmosDbProjection!.Connection);
            Assert.Equal("/projectionName", p.CosmosDbProjection!.PartitionKeyPath);
        }

        [Fact]
        public void Should_find_existing_projection_instead_of_creating_new()
        {
            // Arrange: Projection base and projection class
            var code = @"
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    public class ExistingProjection : Projection { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\ExistingProjection.cs");

            // Pre-populate with an existing projection
            var existingProjection = new ProjectionDefinition
            {
                Name = "ExistingProjection",
                Namespace = "App.Projections",
                FileLocations = ["Projections\\ExistingProjection.cs"],
                ExternalCheckpoint = true
            };
            var list = new List<ProjectionDefinition> { existingProjection };
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.Same(existingProjection, list[0]);
        }

        [Fact]
        public void Should_handle_routed_projection_with_destination()
        {
            // Arrange: RoutedProjection base and destination type
            var code = @"
using System.Threading.Tasks;
namespace ErikLieben.FA.ES.Projections {
    public abstract class Projection { }
    public abstract class RoutedProjection : Projection {
        protected void AddDestination<T>() where T : class { }
    }
}
public class BlobJsonProjectionAttribute : System.Attribute {
    public BlobJsonProjectionAttribute(string container) { Container = container; }
    public string Container { get; }
}
public class ProjectionWithExternalCheckpointAttribute : System.Attribute {}
namespace App.Destinations {
    [BlobJsonProjection(""dest-container"")]
    [ProjectionWithExternalCheckpoint]
    public class MyDestination { }
}
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    using App.Destinations;
    public class MyRoutedProjection : RoutedProjection {
        public MyRoutedProjection() {
            AddDestination<MyDestination>();
        }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\MyRoutedProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var p = list.First();
            Assert.True(p is RoutedProjectionDefinition);
            var rp = (RoutedProjectionDefinition)p;
            Assert.True(rp.IsRoutedProjection);
            Assert.Equal("MyDestination", rp.DestinationType);
            Assert.Contains("MyDestination", rp.DestinationPathTemplates.Keys);
            Assert.Equal("dest-container", rp.DestinationPathTemplates["MyDestination"]);
            Assert.Contains("MyDestination", rp.DestinationsWithExternalCheckpoint);
        }

        [Fact]
        public void Should_handle_projection_without_external_checkpoint()
        {
            // Arrange: Projection without the attribute
            var code = @"
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    public class NoCheckpointProjection : Projection { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\NoCheckpointProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.False(list.First().ExternalCheckpoint);
        }

        [Fact]
        public void Should_handle_projection_without_blob_or_cosmos_attributes()
        {
            // Arrange: Plain projection without storage attributes
            var code = @"
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    public class PlainProjection : Projection { }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\PlainProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var p = list.First();
            Assert.Null(p.BlobProjection);
            Assert.Null(p.CosmosDbProjection);
        }

        [Fact]
        public void Should_collect_multiple_events_from_when_methods()
        {
            // Arrange: Projection with multiple When methods
            var code = @"
namespace ErikLieben.FA.ES { public interface IEvent {} }
namespace ErikLieben.FA.ES.Documents { public interface IObjectDocument {} }
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
namespace App.Events {
    public class OrderCreated : ErikLieben.FA.ES.IEvent {}
    public class OrderUpdated : ErikLieben.FA.ES.IEvent {}
    public class OrderDeleted : ErikLieben.FA.ES.IEvent {}
}
namespace App.Projections {
    using ErikLieben.FA.ES.Projections; using ErikLieben.FA.ES.Documents; using App.Events;
    public class MultiEventProjection : Projection {
        public void When(OrderCreated e, IObjectDocument doc) { }
        public void When(OrderUpdated e, IObjectDocument doc) { }
        public void When(OrderDeleted e, IObjectDocument doc) { }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\MultiEventProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.Equal(3, list.First().Events.Count);
        }

        [Fact]
        public void Should_collect_multiple_properties()
        {
            // Arrange: Projection with multiple properties
            var code = @"
namespace ErikLieben.FA.ES.Projections { public abstract class Projection { } }
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    public class MultiPropProjection : Projection {
        public string Name { get; set; }
        public int Count { get; set; }
        public System.DateTime Created { get; set; }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\MultiPropProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            Assert.Equal(3, list.First().Properties.Count);
            Assert.Contains(list.First().Properties, p => p.Name == "Name");
            Assert.Contains(list.First().Properties, p => p.Name == "Count");
            Assert.Contains(list.First().Properties, p => p.Name == "Created");
        }

        [Fact]
        public void Should_handle_routed_projection_with_destination_without_blob_attribute()
        {
            // Arrange: RoutedProjection with destination that has no BlobJsonProjection
            var code = @"
namespace ErikLieben.FA.ES.Projections {
    public abstract class Projection { }
    public abstract class RoutedProjection : Projection {
        protected void AddDestination<T>() where T : class { }
    }
}
namespace App.Destinations {
    public class NoBlobDestination { }
}
namespace App.Projections {
    using ErikLieben.FA.ES.Projections;
    using App.Destinations;
    public class RoutedNoBlobProjection : RoutedProjection {
        public RoutedNoBlobProjection() {
            AddDestination<NoBlobDestination>();
        }
    }
}
";
            var (classSymbol, _, semanticModel, compilation) = GetFromCode(
                code,
                testAssembly: "AppAssembly",
                filePath: "C:\\Repo\\App\\Projections\\RoutedNoBlobProjection.cs");
            var sut = new AnalyzeProjections(classSymbol!, semanticModel, compilation!, "C:\\Repo\\App\\");
            var list = new List<ProjectionDefinition>();

            // Act
            sut.Run(list);

            // Assert
            Assert.Single(list);
            var rp = list.First() as RoutedProjectionDefinition;
            Assert.NotNull(rp);
            Assert.True(rp!.IsRoutedProjection);
            Assert.Empty(rp.DestinationPathTemplates);
        }
    }

    private static (INamedTypeSymbol?, ClassDeclarationSyntax?, SemanticModel, CSharpCompilation?) GetFromCode(
        string code,
        string testAssembly = "TestAssembly",
        string? filePath = "Test.cs")
    {
        var syntaxTree = string.IsNullOrWhiteSpace(filePath)
            ? CSharpSyntaxTree.ParseText(code)
            : SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions(), filePath);

        var compilation = CSharpCompilation.Create(
            testAssembly,
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classNodes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var classNode = classNodes.Last();
        return (semanticModel.GetDeclaredSymbol(classNode), classNode, semanticModel, compilation);
    }

    private static (SemanticModel, CSharpCompilation) GetSemanticModel(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "Dummy",
            [syntaxTree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        return (compilation.GetSemanticModel(syntaxTree), compilation);
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
