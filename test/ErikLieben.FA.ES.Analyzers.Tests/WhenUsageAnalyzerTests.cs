#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class WhenUsageAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES
{
    using System;
    using System.Threading.Tasks;
    public interface IEventStream
    {
        Task Session(Func<SessionContext, Task> action);
    }
    public sealed class SessionContext
    {
        public T Append<T>(T evt) => evt; // stub
    }
}

namespace ErikLieben.FA.ES.Processors
{
    using ErikLieben.FA.ES;
    using System.Threading.Tasks;
    public abstract class Aggregate
    {
        protected Aggregate(IEventStream stream) { Stream = stream; }
        protected IEventStream Stream { get; }
        protected Task Fold<T>(T _x) => Task.CompletedTask; // stub
        protected Task When<T>(T _x) => Task.CompletedTask; // discouraged API stub
    }
}
";

    [Fact]
    public async Task Should_report_when_used_inside_session_in_aggregate()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using System.Threading.Tasks;
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    public class MyAgg : Aggregate
    {
        public MyAgg(IEventStream stream) : base(stream) {}
        public Task Do()
        {
            return Stream.Session(ctx => {|#0:When|}(ctx.Append(new object())));
        }
    }
}
";

        // Act
        var expected = new DiagnosticResult(WhenUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<WhenUsageAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_outside_session()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using System.Threading.Tasks;
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    public class MyAgg : Aggregate
    {
        public MyAgg(IEventStream stream) : base(stream) {}
        public Task Do()
        {
            // No Stream.Session call here
            return {|#0:When|}(new object());
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<WhenUsageAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_not_in_aggregate()
    {
        // Arrange
        var test = CommonStubs + @"
namespace Test
{
    using System.Threading.Tasks;
    using ErikLieben.FA.ES;

    public class NotAnAggregate
    {
        private readonly IEventStream _stream;
        public NotAnAggregate(IEventStream stream) { _stream = stream; }
        public Task Do()
        {
            // Inside session, but enclosing type is not derived from Aggregate
            return _stream.Session(ctx => When(ctx.Append(new object())));
            Task When<T>(T x) => Task.CompletedTask; // local method named When
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<WhenUsageAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();

        // Assert
        Assert.True(true);
    }
    public class DataSuffix
    {
        [Fact]
        public async Task Should_report_with_suffix_when_data_is_chained_after_when()
        {
            // Arrange
            var test = CommonStubs + @"
namespace Test
{
    using System.Threading.Tasks;
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    public static class Ext { public static T Data<T>(this T v) => v; }

    public class MyAgg : Aggregate
    {
        public MyAgg(IEventStream stream) : base(stream) {}
        public Task Do()
        {
            return Stream.Session(ctx => {|#0:When|}(ctx.Append(new object())).Data());
        }
    }
}
";

            // Act
            var expected = new DiagnosticResult(WhenUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithMessage("Use Fold(...) instead of When(...), and remove trailing .Data() when switching to Fold");

            await new CSharpAnalyzerTest<WhenUsageAnalyzer, XUnitVerifier>
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
                TestCode = test,
                ExpectedDiagnostics = { expected }
            }.RunAsync();

            // Assert
            Assert.True(true);
        }

        [Fact]
        public async Task Should_report_with_suffix_when_data_is_inside_when_arguments()
        {
            // Arrange
            var test = CommonStubs + @"
namespace Test
{
    using System.Threading.Tasks;
    using ErikLieben.FA.ES;
    using ErikLieben.FA.ES.Processors;

    public static class Ext { public static T Data<T>(this T v) => v; }

    public class MyAgg : Aggregate
    {
        public MyAgg(IEventStream stream) : base(stream) {}
        public Task Do()
        {
            return Stream.Session(ctx => {|#0:When|}(ctx.Append(new object()).Data()));
        }
    }
}
";

            // Act
            var expected = new DiagnosticResult(WhenUsageAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithMessage("Use Fold(...) instead of When(...), and remove trailing .Data() when switching to Fold");

            await new CSharpAnalyzerTest<WhenUsageAnalyzer, XUnitVerifier>
            {
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
                TestCode = test,
                ExpectedDiagnostics = { expected }
            }.RunAsync();

            // Assert
            Assert.True(true);
        }
    }
}

#pragma warning restore 0618
