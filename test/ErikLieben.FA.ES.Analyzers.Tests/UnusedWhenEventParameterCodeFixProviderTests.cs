#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class UnusedWhenEventParameterCodeFixProviderTests
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
    public record ProjectMerged { }
}
";

    [Fact]
    public async Task Should_add_when_attribute_and_rename_method_for_unused_parameter()
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
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_when_attribute_and_rename_method_for_discard_parameter()
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

        private void When({|#0:ProjectDeleted _|})
        {
            IsDeleted = true;
        }
    }
}
";
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_when_attribute_for_expression_body_method()
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
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted() => IsDeleted = true;
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_when_attribute_in_projection()
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
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class MyProjection : Projection
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_add_when_attribute_in_routed_projection()
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
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Projections;
    using Test.Events;

    public partial class MyRoutedProjection : RoutedProjection
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_duplicate_using_directive_if_already_present()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
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
        var fixedCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Attributes;
    using ErikLieben.FA.ES.Processors;
    using Test.Events;

    public partial class MyAggregate : Aggregate
    {
        public bool IsDeleted { get; private set; }

        [When<ProjectDeleted>]
        private void WhenProjectDeleted()
        {
            IsDeleted = true;
        }
    }
}
";

        var expected = new DiagnosticResult(UnusedWhenEventParameterAnalyzer.DiagnosticId, DiagnosticSeverity.Info)
            .WithLocation(0);

        // Act & Assert
        await new CSharpCodeFixTest<UnusedWhenEventParameterAnalyzer, UnusedWhenEventParameterCodeFixProvider, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            FixedCode = fixedCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }
}

#pragma warning restore 0618
