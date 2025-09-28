#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class NonPartialAggregateAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES
{
}

namespace ErikLieben.FA.ES.Processors
{
    public abstract class Aggregate { }
}
";

    [Fact]
    public async Task Should_report_when_class_inherits_aggregate_and_is_not_partial()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public class {|#0:MyAgg|} : Aggregate
    {
    }
}
";
        var expected = new DiagnosticResult(NonPartialAggregateAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Class 'MyAgg' inherits from Aggregate and should be declared partial to allow CLI code generation");

        // Act/Assert
        await new CSharpAnalyzerTest<NonPartialAggregateAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_class_is_partial()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public partial class MyAgg : Aggregate
    {
    }
}
";

        // Act/Assert
        await new CSharpAnalyzerTest<NonPartialAggregateAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_also_for_indirect_inheritance()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public abstract class {|#1:BaseAgg|} : Aggregate { }

    public class {|#0:MyAgg|} : BaseAgg
    {
    }
}
";
        var expected1 = new DiagnosticResult(NonPartialAggregateAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);
        var expected2 = new DiagnosticResult(NonPartialAggregateAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(1);

        // Act/Assert
        await new CSharpAnalyzerTest<NonPartialAggregateAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected1, expected2 }
        }.RunAsync();
    }
}

#pragma warning restore 0618
