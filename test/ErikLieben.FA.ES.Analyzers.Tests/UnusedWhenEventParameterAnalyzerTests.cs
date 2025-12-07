#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class UnusedWhenEventParameterAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES
{
}

namespace ErikLieben.FA.ES.Processors
{
    public abstract class Aggregate { }
}

namespace ErikLieben.FA.ES.Projections
{
    public abstract class Projection { }
    public abstract class RoutedProjection { }
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
}
";

    [Fact]
    public async Task Should_report_when_event_parameter_is_unused_in_aggregate()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted @event|})
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_event_parameter_is_unused_in_projection()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class MyProjection : Projection
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted @event|})
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_event_parameter_is_used()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public string? Name { get; private set; }
        public string? Description { get; private set; }

        private void When(ProjectCreated @event)
        {
            Name = @event.Name;
            Description = @event.Description;
        }
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_method_already_has_when_attribute()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using ErikLieben.FA.ES.Attributes;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void MarkAsDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_for_non_when_methods()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void HandleEvent(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Act & Assert - no diagnostics expected (method is not named "When")
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_for_classes_not_inheriting_aggregate_or_projection()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using Test.Events;

    public class SomeOtherClass
    {
        public bool IsDeleted { get; private set; }

        private void When(ProjectDeleted @event)
        {
            IsDeleted = true;
        }
    }
}
";

        // Act & Assert - no diagnostics expected (not an Aggregate or Projection)
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_event_parameter_unused_in_expression_body()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted @event|}) => IsDeleted = true;
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_event_parameter_used_in_expression_body()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public string? Name { get; private set; }

        private void When(ProjectCreated @event) => Name = @event.Name;
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_discard_parameter_is_used_in_aggregate()
    {
        // Arrange - Using discard pattern (_) explicitly indicates the parameter is unused
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted _|})
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_discard_parameter_is_used_in_projection()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class MyProjection : Projection
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted _|})
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_discard_parameter_is_used_in_routed_projection()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class MyRoutedProjection : RoutedProjection
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted _|})
        {
            IsDeleted = true;
        }
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_discard_parameter_with_expression_body()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        private void When({|#0:ProjectDeleted _|}) => IsDeleted = true;
    }
}
";
        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpAnalyzerTest<UnusedWhenEventParameterAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }
}

#pragma warning restore 0618
