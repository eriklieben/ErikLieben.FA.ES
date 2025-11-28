using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErikLieben.FA.ES.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ErikLieben.FA.ES.Analyzers.Tests;

public class StreamActionRefactoringProviderTests
{
    private readonly StreamActionRefactoringProvider _provider = new();

    public class ManualToAttribute : StreamActionRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_refactoring_for_RegisterAction_in_aggregate_constructor()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;
                using ErikLieben.FA.ES.Actions;

                namespace Test;

                public class TestAction : IAction { }

                public partial class TestAggregate : Aggregate
                {
                    public TestAggregate(IEventStream stream) : base(stream)
                    {
                        stream.RegisterAction(new TestAction());
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "RegisterAction");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Convert to [StreamAction<TestAction>] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_not_offer_refactoring_outside_aggregate()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Actions;

                namespace Test;

                public class TestAction : IAction { }

                public class NotAnAggregate
                {
                    public NotAnAggregate(IEventStream stream)
                    {
                        stream.RegisterAction(new TestAction());
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "RegisterAction");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Empty(actions);
        }

        [Fact]
        public async Task Should_add_attribute_and_remove_statement_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;
                using ErikLieben.FA.ES.Actions;

                namespace Test;

                public class TestAction : IAction { }

                public partial class TestAggregate : Aggregate
                {
                    public TestAggregate(IEventStream stream) : base(stream)
                    {
                        stream.RegisterAction(new TestAction());
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "RegisterAction");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should have the attribute
            Assert.Contains("[StreamAction<TestAction>]", newCode);
            // Should not have the RegisterAction call anymore
            Assert.DoesNotContain("RegisterAction", newCode);
            // Should have added the using
            Assert.Contains("using ErikLieben.FA.ES.Attributes;", newCode);
        }
    }

    public class AttributeToManual : StreamActionRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_refactoring_for_StreamAction_attribute()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;
                using ErikLieben.FA.ES.Actions;

                namespace Test;

                public class TestAction : IAction { }

                [StreamAction<TestAction>]
                public partial class TestAggregate : Aggregate
                {
                    public TestAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "StreamAction<TestAction>");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Convert to stream.RegisterAction(new TestAction())", actions[0].Title);
        }

        [Fact]
        public async Task Should_add_RegisterAction_and_remove_attribute_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;
                using ErikLieben.FA.ES.Actions;

                namespace Test;

                public class TestAction : IAction { }

                [StreamAction<TestAction>]
                public partial class TestAggregate : Aggregate
                {
                    public TestAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "StreamAction<TestAction>");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should have the RegisterAction call
            Assert.Contains("stream.RegisterAction(new TestAction())", newCode);
            // Should not have the attribute anymore
            Assert.DoesNotContain("[StreamAction<TestAction>]", newCode);
        }
    }

    private (Document document, TextSpan span) CreateDocumentWithSpan(string code, string textToSelect)
    {
        var index = code.IndexOf(textToSelect, StringComparison.Ordinal);
        var span = new TextSpan(index, textToSelect.Length);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        // Add fake types for the test
        var fakeTypes = """
            namespace ErikLieben.FA.ES
            {
                public interface IEventStream
                {
                    void RegisterAction(ErikLieben.FA.ES.Actions.IAction action);
                }
            }

            namespace ErikLieben.FA.ES.Actions
            {
                public interface IAction { }
            }

            namespace ErikLieben.FA.ES.Processors
            {
                public class Aggregate
                {
                    protected IEventStream Stream { get; }
                    public Aggregate(IEventStream stream) { Stream = stream; }
                }
            }

            namespace ErikLieben.FA.ES.Attributes
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
                public class StreamActionAttribute<T> : System.Attribute where T : ErikLieben.FA.ES.Actions.IAction { }
            }
            """;

        var project = new AdhocWorkspace()
            .AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(references)
            .AddDocument("FakeTypes.cs", fakeTypes)
            .Project;

        var document = project.AddDocument("Test.cs", code);

        return (document, span);
    }

    private async Task<List<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span)
    {
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            span,
            a => actions.Add(a),
            CancellationToken.None);

        await _provider.ComputeRefactoringsAsync(context);
        return actions;
    }

    private static async Task<Document> ApplyRefactoringAsync(Document document, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyChangesOperation = operations.OfType<ApplyChangesOperation>().First();
        return applyChangesOperation.ChangedSolution.GetDocument(document.Id)!;
    }
}
