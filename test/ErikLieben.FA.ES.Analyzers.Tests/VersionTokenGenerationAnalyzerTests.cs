#pragma warning disable 0618 // XUnitVerifier is obsolete in Roslyn testing; suppress to avoid warnings without changing packages
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class VersionTokenGenerationAnalyzerTests
{
    private const string CommonStubs = @"
namespace ErikLieben.FA.ES
{
    public interface IEvent { }
}

namespace ErikLieben.FA.ES.Documents
{
    public interface IObjectDocument { }
}

namespace ErikLieben.FA.ES
{
    public abstract record VersionToken
    {
        protected VersionToken() { }
        protected VersionToken(string versionTokenString) { }
        protected VersionToken(IEvent @event, ErikLieben.FA.ES.Documents.IObjectDocument document) { }
    }

    public abstract record VersionToken<T> : VersionToken
    {
        protected VersionToken() : base() { }
        protected VersionToken(string versionTokenString) : base(versionTokenString) { }
        protected VersionToken(IEvent @event, ErikLieben.FA.ES.Documents.IObjectDocument document) : base(@event, document) { }
        public new T ObjectId { get; set; }
        protected abstract T ToObjectOfT(string objectId);
        protected abstract string FromObjectOfT(T objectId);
    }
}
";

    [Fact]
    public async Task Should_report_when_version_token_generated_file_is_missing()
    {
        // Arrange - VersionToken with no generated file
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES;
    using System;

    public partial record {|#0:ProjectVersionToken|} : VersionToken<Guid>
    {
        protected override Guid ToObjectOfT(string objectId) => Guid.Parse(objectId);
        protected override string FromObjectOfT(Guid objectId) => objectId.ToString();
    }
}
";
        var expected = new DiagnosticResult(VersionTokenGenerationAnalyzer.VersionTokenMissingGeneratedFileDiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithMessage("VersionToken 'ProjectVersionToken' requires code generation. Run 'dotnet faes' to generate supporting code.");

        // Act & Assert
        await new CSharpAnalyzerTest<VersionTokenGenerationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode,
            ExpectedDiagnostics = { expected }
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_when_version_token_has_generated_file()
    {
        // Arrange - VersionToken with generated file present
        var sourceCode = CommonStubs + @"
// File: ProjectVersionToken.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using System;

    public partial record ProjectVersionToken : VersionToken<Guid>
    {
        protected override Guid ToObjectOfT(string objectId) => Guid.Parse(objectId);
        protected override string FromObjectOfT(Guid objectId) => objectId.ToString();
    }
}
";

        var generatedCode = @"
// File: ProjectVersionToken.Generated.cs
namespace Test
{
    using ErikLieben.FA.ES;
    using System;

    public partial record ProjectVersionToken
    {
        public ProjectVersionToken(string versionTokenString) : base(versionTokenString) { }
    }
}
";

        // Act & Assert - no diagnostics expected
        var test = new CSharpAnalyzerTest<VersionTokenGenerationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90
        };
        test.TestState.Sources.Add(("/Test/ProjectVersionToken.cs", sourceCode));
        test.TestState.Sources.Add(("/Test/ProjectVersionToken.Generated.cs", generatedCode));
        await test.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_for_non_partial_version_tokens()
    {
        // Non-partial records should be skipped (they would fail to compile anyway)
        var sourceCode = CommonStubs + @"
namespace Test
{
    using ErikLieben.FA.ES;
    using System;

    public record NonPartialVersionToken : VersionToken<Guid>
    {
        protected override Guid ToObjectOfT(string objectId) => Guid.Parse(objectId);
        protected override string FromObjectOfT(Guid objectId) => objectId.ToString();
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<VersionTokenGenerationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }

    [Fact]
    public async Task Should_not_report_for_regular_records()
    {
        // Regular records that don't inherit from VersionToken shouldn't trigger anything
        var sourceCode = CommonStubs + @"
namespace Test
{
    public partial record RegularRecord
    {
        public string Name { get; init; }
    }
}
";

        // Act & Assert - no diagnostics expected
        await new CSharpAnalyzerTest<VersionTokenGenerationAnalyzer, XUnitVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = sourceCode
        }.RunAsync();

        // Analyzer test assertion: expected diagnostics were verified
        Assert.True(true);
    }
}

#pragma warning restore 0618
