#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class AppendWithoutApplyAnalyzerTests
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
        public T Append<T>(T evt) => evt; // stub Append
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
        protected Task Fold<T>(T _x) => Task.CompletedTask; // stub Fold
        protected Task When<T>(T _x) => Task.CompletedTask; // stub When
    }
}
";

    [Fact]
    public async Task Should_report_when_append_not_applied_with_fold_or_when()
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
            return Stream.Session(ctx => { ctx.{|#0:Append|}(new object()); return Task.CompletedTask; });
        }
    }
}
";

        // Act
        var expected = new DiagnosticResult(AppendWithoutApplyAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0);

        await new CSharpAnalyzerTest<AppendWithoutApplyAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_append_is_wrapped_in_fold()
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
            return Stream.Session(ctx => Fold(ctx.Append(new object())));
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<AppendWithoutApplyAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_append_is_wrapped_in_when()
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
            return Stream.Session(ctx => When(ctx.Append(new object())));
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<AppendWithoutApplyAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();

        // Assert
        Assert.True(true);
    }
    [Fact]
    public async Task Should_not_report_when_append_outside_session()
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
            var c = new SessionContext();
            c.Append(new object());
            return Task.CompletedTask;
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<AppendWithoutApplyAnalyzer, XUnitVerifier>
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
            return _stream.Session(ctx => { ctx.Append(new object()); return Task.CompletedTask; });
        }
    }
}
";

        // Act
        await new CSharpAnalyzerTest<AppendWithoutApplyAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = test
        }.RunAsync();

        // Assert
        Assert.True(true);
    }
}

#pragma warning restore 0618
