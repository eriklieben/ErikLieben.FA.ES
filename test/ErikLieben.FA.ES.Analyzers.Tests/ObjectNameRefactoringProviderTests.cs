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

public class ObjectNameRefactoringProviderTests
{
    private readonly ObjectNameRefactoringProvider _provider = new();

    public class AddExplicitAttribute : ObjectNameRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_refactoring_for_aggregate_without_ObjectName_attribute()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "OrderAggregate");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Add explicit [ObjectName(\"orderAggregate\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_add_attribute_with_correct_convention_name_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "OrderAggregate");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should have the attribute with camelCase name
            Assert.Contains("[ObjectName(\"orderAggregate\")]", newCode);
            // Should have added the using
            Assert.Contains("using ErikLieben.FA.ES.Attributes;", newCode);
        }

        [Fact]
        public async Task Should_use_correct_convention_for_single_word_class_name()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                public partial class Order : Aggregate
                {
                    public Order(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "class Order");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Add explicit [ObjectName(\"order\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_not_offer_refactoring_for_non_aggregate_class()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public class NotAnAggregate
                {
                    public NotAnAggregate()
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "NotAnAggregate");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Empty(actions);
        }

        [Fact]
        public async Task Should_not_offer_add_refactoring_when_attribute_already_exists()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                [ObjectName("customName")]
                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "class OrderAggregate");

            var actions = await GetRefactoringsAsync(document, span);

            // Should not offer to add (already has attribute), and should not offer to remove (custom name doesn't match convention)
            Assert.Empty(actions);
        }
    }

    public class RemoveRedundantAttribute : ObjectNameRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_to_remove_attribute_when_it_matches_convention()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                [ObjectName("orderAggregate")]
                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "ObjectName");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Remove redundant [ObjectName] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_remove_attribute_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                [ObjectName("orderAggregate")]
                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "ObjectName");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should not have the attribute anymore
            Assert.DoesNotContain("[ObjectName", newCode);
        }

        [Fact]
        public async Task Should_not_offer_to_remove_attribute_when_it_differs_from_convention()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;
                using ErikLieben.FA.ES.Processors;

                namespace Test;

                [ObjectName("custom-order-name")]
                public partial class OrderAggregate : Aggregate
                {
                    public OrderAggregate(IEventStream stream) : base(stream)
                    {
                    }
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "ObjectName");

            var actions = await GetRefactoringsAsync(document, span);

            // Should not offer to remove because the attribute has a custom name
            Assert.Empty(actions);
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
                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
                public class ObjectNameAttribute : System.Attribute
                {
                    public ObjectNameAttribute(string name) { Name = name; }
                    public string Name { get; init; }
                }
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
