#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class ExtensionsRegistrationAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES.Processors
{
    public abstract class Aggregate { }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }
}

namespace ErikLieben.FA.ES.Aggregates
{
    public interface IAggregateFactory<TAggregate, TId> { }
}
";

    [Fact]
    public async Task Should_report_when_extensions_file_is_missing()
    {
        // Arrange - Aggregate class but no Extensions.Generated.cs file
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public partial class {|#0:MyAggregate|} : Aggregate
    {
        public string? Name { get; private set; }
    }
}
";
        var expected = new DiagnosticResult(ExtensionsRegistrationAnalyzer.MissingExtensionsFileDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Project contains Aggregates but no Extensions.Generated.cs file. Run 'dotnet faes' to generate.");

        // Act & Assert
        await new CSharpAnalyzerTest<ExtensionsRegistrationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_when_aggregate_not_registered_in_extensions()
    {
        // Arrange - Aggregate exists but not registered in Extensions
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public partial class {|#0:MyAggregate|} : Aggregate
    {
        public string? Name { get; private set; }
    }

    public partial class OtherAggregate : Aggregate
    {
        public string? Title { get; private set; }
    }
}
";

        // Extensions file only has OtherAggregate registered, not MyAggregate
        var extensionsCode = @"
// File: TestExtensions.Generated.cs
namespace Test
{
    using Microsoft.Extensions.DependencyInjection;
    using ErikLieben.FA.ES.Aggregates;

    public class TestFactory
    {
        public static void Register(IServiceCollection serviceCollection)
        {
            // Only OtherAggregate is registered
            // serviceCollection.AddSingleton<IAggregateFactory<OtherAggregate, OtherId>, OtherFactory>();
        }

        public static System.Type Get(System.Type type)
        {
            return type switch
            {
                System.Type agg when agg == typeof(OtherAggregate) => typeof(IAggregateFactory<OtherAggregate, string>),
                _ => null!
            };
        }
    }
}
";

        var expected = new DiagnosticResult(ExtensionsRegistrationAnalyzer.AggregateNotRegisteredDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("Aggregate 'MyAggregate' is not registered in Extensions. Run 'dotnet faes' to update.");

        // Act & Assert
        var test = new CSharpAnalyzerTest<ExtensionsRegistrationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            ExpectedDiagnostics = { expected }
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/TestExtensions.Generated.cs", extensionsCode));
        await test.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_all_aggregates_registered()
    {
        // Arrange - All aggregates are registered in Extensions
        var sourceCode = CommonStubs + @"
// File: MyAggregate.cs
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public partial class MyAggregate : Aggregate
    {
        public string? Name { get; private set; }
    }
}
";

        // Extensions file has MyAggregate registered
        var extensionsCode = @"
// File: TestExtensions.Generated.cs
namespace Test
{
    using Microsoft.Extensions.DependencyInjection;
    using ErikLieben.FA.ES.Aggregates;

    public class TestFactory
    {
        public static void Register(IServiceCollection serviceCollection)
        {
            // MyAggregate is registered via IAggregateFactory
        }

        public static System.Type Get(System.Type type)
        {
            return type switch
            {
                System.Type agg when agg == typeof(MyAggregate) => typeof(IAggregateFactory<MyAggregate, string>),
                _ => null!
            };
        }
    }
}
";

        // Act & Assert - no diagnostics expected
        var test = new CSharpAnalyzerTest<ExtensionsRegistrationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };
        test.TestState.Sources.Add(("/Test/MyAggregate.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/TestExtensions.Generated.cs", extensionsCode));
        await test.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_for_non_partial_aggregate_classes()
    {
        // Non-partial classes are handled by FAES0003, not this analyzer
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES.Processors;

    public class MyAggregate : Aggregate
    {
        public string? Name { get; private set; }
    }
}
";

        // Act & Assert - no diagnostics from this analyzer (FAES0003 handles non-partial)
        await new CSharpAnalyzerTest<ExtensionsRegistrationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_for_classes_not_inheriting_aggregate()
    {
        // Regular classes that don't inherit from Aggregate shouldn't trigger anything
        var sourceCode = CommonStubs + @"
namespace Test
{
    public partial class RegularClass
    {
        public string? Name { get; private set; }
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<ExtensionsRegistrationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();
    }
}

#pragma warning restore 0618
