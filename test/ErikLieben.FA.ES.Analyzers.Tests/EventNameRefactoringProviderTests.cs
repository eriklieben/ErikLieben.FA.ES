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

public class EventNameRefactoringProviderTests
{
    private readonly EventNameRefactoringProvider _provider = new();

    public class AddExplicitAttribute : EventNameRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_refactoring_for_event_without_EventName_attribute()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "UserCreated");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Add explicit [EventName(\"User.Created\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_add_attribute_with_correct_convention_name_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "record UserCreated");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should have the attribute with period-separated name
            Assert.Contains("[EventName(\"User.Created\")]", newCode);
            // Should have added the using
            Assert.Contains("using ErikLieben.FA.ES.Attributes;", newCode);
        }

        [Fact]
        public async Task Should_use_correct_convention_for_multi_word_event_name()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public record OrderItemAdded : IEvent<OrderItemAdded>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "OrderItemAdded");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Add explicit [EventName(\"Order.Item.Added\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_use_correct_convention_for_single_word_event_name()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public record Created : IEvent<Created>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "record Created");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            // Single word should remain unchanged
            Assert.Contains("Add explicit [EventName(\"Created\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_work_with_class_based_events()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public class UserRegistered : IEvent<UserRegistered>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "UserRegistered");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Add explicit [EventName(\"User.Registered\")] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_not_offer_refactoring_for_non_event_type()
        {
            var code = """
                using ErikLieben.FA.ES;

                namespace Test;

                public record NotAnEvent
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "NotAnEvent");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Empty(actions);
        }

        [Fact]
        public async Task Should_not_offer_add_refactoring_when_attribute_already_exists()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;

                namespace Test;

                [EventName("custom.event.name")]
                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "record UserCreated");

            var actions = await GetRefactoringsAsync(document, span);

            // Should not offer to add (already has attribute), and should not offer to remove (custom name doesn't match convention)
            Assert.Empty(actions);
        }
    }

    public class RemoveRedundantAttribute : EventNameRefactoringProviderTests
    {
        [Fact]
        public async Task Should_offer_to_remove_attribute_when_it_matches_convention()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;

                namespace Test;

                [EventName("User.Created")]
                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "EventName");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Remove redundant [EventName] attribute", actions[0].Title);
        }

        [Fact]
        public async Task Should_remove_attribute_when_applied()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;

                namespace Test;

                [EventName("User.Created")]
                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "EventName");

            var actions = await GetRefactoringsAsync(document, span);
            Assert.Single(actions);

            var changedDocument = await ApplyRefactoringAsync(document, actions[0]);
            var newCode = (await changedDocument.GetTextAsync()).ToString();

            // Should not have the attribute anymore
            Assert.DoesNotContain("[EventName", newCode);
        }

        [Fact]
        public async Task Should_not_offer_to_remove_attribute_when_it_differs_from_convention()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;

                namespace Test;

                [EventName("Custom.Event.Name")]
                public record UserCreated : IEvent<UserCreated>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "EventName");

            var actions = await GetRefactoringsAsync(document, span);

            // Should not offer to remove because the attribute has a custom name
            Assert.Empty(actions);
        }

        [Fact]
        public async Task Should_offer_to_remove_for_multi_word_convention_match()
        {
            var code = """
                using ErikLieben.FA.ES;
                using ErikLieben.FA.ES.Attributes;

                namespace Test;

                [EventName("Order.Item.Added")]
                public record OrderItemAdded : IEvent<OrderItemAdded>
                {
                }
                """;

            var (document, span) = CreateDocumentWithSpan(code, "EventName");

            var actions = await GetRefactoringsAsync(document, span);

            Assert.Single(actions);
            Assert.Contains("Remove redundant [EventName] attribute", actions[0].Title);
        }
    }

    private static (Document document, TextSpan span) CreateDocumentWithSpan(string code, string textToSelect)
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
                public interface IEvent<T>
                {
                }
            }

            namespace ErikLieben.FA.ES.Attributes
            {
                [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
                public class EventNameAttribute : System.Attribute
                {
                    public EventNameAttribute(string name) { Name = name; }
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
