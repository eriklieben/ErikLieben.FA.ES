#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class CodeGenerationRequiredAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES
{
    public interface IEvent
    {
        string EventType { get; }
    }
}

namespace ErikLieben.FA.ES.Processors
{
    public abstract class Aggregate
    {
        public virtual void Fold(ErikLieben.FA.ES.IEvent @event) { }
    }
}

namespace ErikLieben.FA.ES.Projections
{
    public abstract class Projection
    {
        public virtual System.Threading.Tasks.Task Fold<T>(ErikLieben.FA.ES.IEvent @event, object versionToken, T? data = default(T), object? parentContext = null) where T : class
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
    public abstract class RoutedProjection : Projection { }
}

namespace ErikLieben.FA.ES.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WhenAttribute<TEvent> : System.Attribute where TEvent : class { }
}

namespace Test.Events
{
    public record ProjectDeleted { }
    public record ProjectCreated(string Name, string Description) { }
    public record ProjectRenamed(string OldName, string NewName) { }
}
";

    [Fact]
    public async Task Should_report_when_generated_file_is_missing()
    {
        // Arrange - Aggregate with When method but no generated file
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class {|#0:MyAggregate|} : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.MissingGeneratedFileDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Class 'MyAggregate' requires code generation. Run 'dotnet faes' to generate supporting code.");

        // Act & Assert
        await new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_report_when_new_when_method_not_in_generated_fold()
    {
        // Arrange - Aggregate with When method that's not in the generated Fold
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }
        public string? Name { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }

        private void {|#0:When|}(ProjectCreated @event)
        {
            Name = @event.Name;
        }
    }
}
";

        // Generated file only has ProjectDeleted, not ProjectCreated
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
        public string? Name { get; }
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.StaleGeneratedCodeDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Event handler 'When' for 'ProjectCreated' is not in generated code. Run 'dotnet faes' to update.");

        // Act & Assert
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            ExpectedDiagnostics = { expected }
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_report_when_when_attribute_not_in_generated_fold()
    {
        // Arrange - Aggregate with [When<T>] attribute that's not in the generated Fold
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using ErikLieben.FA.ES.Attributes;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }
        public string? NewName { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }

        [{|#0:When<ProjectRenamed>|}]
        private void WhenProjectRenamed()
        {
            // Handle rename
        }
    }
}
";

        // Generated file only has ProjectDeleted, not ProjectRenamed
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
        public string? NewName { get; }
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.StaleGeneratedCodeDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Event handler 'When' for 'ProjectRenamed' is not in generated code. Run 'dotnet faes' to update.");

        // Act & Assert
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            ExpectedDiagnostics = { expected }
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_generated_file_has_all_when_methods()
    {
        // Arrange - Aggregate with When method that IS in the generated Fold
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Generated file has ProjectDeleted
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        // Act & Assert - no diagnostics expected
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_for_non_partial_classes()
    {
        // Non-partial classes are handled by FAES0003, not this analyzer
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Act & Assert - no diagnostics from this analyzer (FAES0003 handles non-partial)
        await new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_for_classes_without_when_methods()
    {
        // Arrange - Aggregate without When methods shouldn't trigger anything
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public partial class EmptyAggregate : Aggregate
    {
        public string? Name { get; private set; }
    }
}
";

        // Act & Assert - no diagnostics expected (no When methods)
        await new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_work_with_projections()
    {
        // Arrange - Projection with When method but no generated file
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class {|#0:MyProjection|} : Projection
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.MissingGeneratedFileDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Class 'MyProjection' requires code generation. Run 'dotnet faes' to generate supporting code.");

        // Act & Assert
        await new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_work_with_routed_projections()
    {
        // Arrange - RoutedProjection with When method but no generated file
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class {|#0:MyRoutedProjection|} : RoutedProjection
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.MissingGeneratedFileDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Class 'MyRoutedProjection' requires code generation. Run 'dotnet faes' to generate supporting code.");

        // Act & Assert
        await new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_report_when_property_not_in_generated_interface()
    {
        // Arrange - Aggregate with public property not in the generated interface
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }
        public string? Name { get; private set; }
        public string? {|#0:NewProperty|} { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Generated file has IMyAggregate interface but missing NewProperty
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
        public string? Name { get; }
        // NewProperty is missing!
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        var expected = new DiagnosticResult(CodeGenerationRequiredAnalyzer.PropertyNotInGeneratedInterfaceDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Property 'NewProperty' is not in generated interface 'IMyAggregate'. Run 'dotnet faes' to update.");

        // Act & Assert
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            ExpectedDiagnostics = { expected }
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_all_properties_in_generated_interface()
    {
        // Arrange - Aggregate with all public properties in the generated interface
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }
        public string? Name { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Generated file has all properties in IMyAggregate interface
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
        public string? Name { get; }
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        // Act & Assert - no diagnostics expected
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_for_private_properties()
    {
        // Arrange - Aggregate with private property that shouldn't be in interface
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }
        private string? PrivateProperty { get; set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Generated file has only public properties
        var generatedCode = @"
// File: MyAggregate.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    internal class ProjectDeletedJsonSerializerContext { public static ProjectDeletedJsonSerializerContext Default => new(); public object ProjectDeleted => null!; }
    internal static class JsonEvent { public static object To(IEvent e, object ctx) => null!; }

    public interface IMyAggregate
    {
        public bool IsDeleted { get; }
    }

    public partial class MyAggregate : Aggregate
    {
        public override void Fold(IEvent @event)
        {
            switch (@event.EventType)
            {
                case ""Project.Deleted"":
                    When(JsonEvent.To(@event, ProjectDeletedJsonSerializerContext.Default.ProjectDeleted));
                    break;
            }
        }

        private void When(object @event) { }
    }
}
";

        // Act & Assert - no diagnostics expected (private properties shouldn't be in interface)
        var test = new CSharpAnalyzerTest<CodeGenerationRequiredAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/MyAggregate.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }
}

#pragma warning restore 0618
