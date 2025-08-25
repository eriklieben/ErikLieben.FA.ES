// using System.Runtime.InteropServices;
// using ErikLieben.FA.ES.Attributes;
// using ErikLieben.FA.ES.CLI.Analyze;
// using ErikLieben.FA.ES.CLI.Analyze2;
// using ErikLieben.FA.ES.CLI.Model;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.Extensions.DependencyInjection;
// using NSubstitute;
//
// namespace ErikLieben.FA.ES.CLI.Tests.Analyze;
//
// public class AnalyzeAggregatesTests
// {
//     public class Ctor
//     {
//         [Fact]
//         public void Should_throw_an_exception_if_classSymbol_is_null()
//         {
//             // Arrange
//             var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//             var semanticModel = Substitute.For<SemanticModel>();
//             var compilation = CSharpCompilation.Create(
//                 "MyCompilation",
//                 syntaxTrees: [CSharpSyntaxTree.ParseText("public class ClassB {}")],
//                 references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
//             var solutionRootPath = string.Empty;
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeAggregates(
//                     null!,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'classSymbol')",
//                 exception.Message);
//         }
//
//
//         [Fact]
//         public void Should_throw_an_exception_if_classDeclaration_is_null()
//         {
//             // Arrange
//             var classSymbol = Substitute.For<INamedTypeSymbol>();
//             var semanticModel = Substitute.For<SemanticModel>();
//             var compilation = CSharpCompilation.Create(
//                 "MyCompilation",
//                 syntaxTrees: [CSharpSyntaxTree.ParseText("public class ClassB {}")],
//                 references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
//             var solutionRootPath = string.Empty;
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeAggregates(
//                     classSymbol,
//                     null!,
//                     semanticModel,
//                     compilation,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'classDeclaration')",
//                 exception.Message);
//         }
//
//
//         [Fact]
//         public void Should_throw_an_exception_if_semanticModel_is_null()
//         {
//             // Arrange
//             var classSymbol = Substitute.For<INamedTypeSymbol>();
//             var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//             var compilation = CSharpCompilation.Create(
//                 "MyCompilation",
//                 syntaxTrees: [CSharpSyntaxTree.ParseText("public class ClassB {}")],
//                 references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
//             var solutionRootPath = string.Empty;
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     null!,
//                     compilation,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'semanticModel')",
//                 exception.Message);
//         }
//
//
//         [Fact]
//         public void Should_throw_an_exception_if_compilation_is_null()
//         {
//             // Arrange
//             var classSymbol = Substitute.For<INamedTypeSymbol>();
//             var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//             var semanticModel = Substitute.For<SemanticModel>();
//             var solutionRootPath = string.Empty;
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     null!,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'compilation')",
//                 exception.Message);
//         }
//
//
//         [Fact]
//         public void Should_throw_an_exception_if_solutionRootPath_is_null()
//         {
//             // Arrange
//             var classSymbol = Substitute.For<INamedTypeSymbol>();
//             var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//             var semanticModel = Substitute.For<SemanticModel>();
//             var compilation = CSharpCompilation.Create(
//                 "MyCompilation",
//                 syntaxTrees: [CSharpSyntaxTree.ParseText("public class ClassB {}")],
//                 references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     null!));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'solutionRootPath')",
//                 exception.Message);
//         }
//     }
//
//     public class Run
//     {
//         public class AnalyzeWhenMethods
//         {
//             [Fact]
//             public void Should_only_process_aggregates_if_class_inherits_from_aggregate()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     """
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Attributes;
//                     using ErikLieben.FA.ES.Processors;
//
//                     namespace TestDomain;
//
//                     public class TestAggregate(IEventStream stream)
//                     {
//                     }
//                     """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 Assert.Empty(returnValue);
//             }
//
//             [Fact]
//             public void Should_skip_aggregates_with_ignore_attribute()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     """
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Attributes;
//                     using ErikLieben.FA.ES.Processors;
//
//                     namespace TestDomain;
//
//                     [Ignore("Only for testing purposes")]
//                     public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                     {
//                     }
//                     """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 Assert.Empty(returnValue);
//             }
//
//             [Fact]
//             public void Should_skip_aggregate_in_namespace_ErikLieben_fa_es()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     """
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Attributes;
//                     using ErikLieben.FA.ES.Processors;
//
//                     namespace ErikLieben.FA.ES;
//
//                     public class SomeAggregate : Aggregate(stream)
//                     {
//                     }
//                     """,
//                     "ErikLieben.FA.ES");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 Assert.Empty(returnValue);
//             }
//
//             [Fact]
//             public void Should_skip_aggregate_if_its_not_the_solution_root_folder()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     """
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Attributes;
//                     using ErikLieben.FA.ES.Processors;
//
//                     namespace ErikLieben.FA.ES;
//
//                     public class SomeAggregate : Aggregate(stream)
//                     {
//                     }
//                     """,
//                     filePath: @"c:\TestDomain2\App\File\C.cs");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 Assert.Empty(returnValue);
//             }
//
//             [Fact]
//             public void Should_detect_simple_event_with_event_name_attribute_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       [EventName("SomeEventName")]
//                       public record SomeEvent();
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//                 var firstEvent = first.Events.First();
//                 Assert.Equal("SomeEventName", firstEvent.EventName);
//                 Assert.Equal("SomeEvent", firstEvent.TypeName);
//                 Assert.Equal("TestDomain", firstEvent.Namespace);
//                 Assert.Equal("When", firstEvent.ActivationType);
//                 Assert.False(firstEvent.ActivationAwaitRequired);
//             }
//
//             [Fact]
//             public void Should_detect_simple_event_without_event_name_attribute_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent();
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//                 var firstEvent = first.Events.First();
//                 Assert.Equal("Some.Event", firstEvent.EventName);
//                 Assert.Equal("SomeEvent", firstEvent.TypeName);
//                 Assert.Equal("TestDomain", firstEvent.Namespace);
//                 Assert.Equal("When", firstEvent.ActivationType);
//                 Assert.False(firstEvent.ActivationAwaitRequired);
//             }
//
//             [Fact]
//             public void Should_detect_simple_event_with_event_name_attribute_but_with_empty_value_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent();
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//                 var firstEvent = first.Events.First();
//                 Assert.Equal("Some.Event", firstEvent.EventName);
//                 Assert.Equal("SomeEvent", firstEvent.TypeName);
//                 Assert.Equal("TestDomain", firstEvent.Namespace);
//                 Assert.Equal("When", firstEvent.ActivationType);
//                 Assert.False(firstEvent.ActivationAwaitRequired);
//             }
//
//             [Fact]
//             public void Should_detect_single_property_on_event_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent(string Name);
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//                 var firstEvent = first.Events.First();
//                 Assert.Single(firstEvent.Properties);
//                 var firstProperty = firstEvent.Properties.First();
//                 Assert.Equal("Name", firstProperty.Name);
//                 Assert.Equal("String", firstProperty.Type);
//                 Assert.Equal("System", firstProperty.Namespace);
//                 Assert.False(firstProperty.IsGeneric);
//             }
//
//             [Fact]
//             public void Should_detect_multiple_properties_on_event_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent(string Name, Guid id);
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//                 var firstEvent = first.Events.First();
//                 Assert.Equal(2, firstEvent.Properties.Count);
//
//                 var firstProperty = firstEvent.Properties.First();
//                 Assert.Equal("Name", firstProperty.Name);
//                 Assert.Equal("String", firstProperty.Type);
//                 Assert.Equal("System", firstProperty.Namespace);
//                 Assert.False(firstProperty.IsGeneric);
//
//                 var secondProperty = firstEvent.Properties.Last();
//                 Assert.Equal("id", secondProperty.Name);
//                 Assert.Equal("Guid", secondProperty.Type);
//                 Assert.Equal("System", firstProperty.Namespace);
//                 Assert.False(secondProperty.IsGeneric);
//             }
//
//             [Fact]
//             public void Should_detect_generic_property_on_event_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent(List<string> Name);
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Events);
//
//                 var firstEvent = first.Events.First();
//                 Assert.Single(firstEvent.Properties);
//
//                 var firstProperty = firstEvent.Properties.First();
//                 Assert.True(firstProperty.IsGeneric);
//                 Assert.Single(firstProperty.GenericTypes);
//
//                 var genericType = firstProperty.GenericTypes.First();
//                 Assert.Equal("String", genericType.Name);
//                 Assert.Equal("System", genericType.Namespace);
//             }
//         }
//
//         public class AnalyzeCommandMethods
//         {
//             [Fact]
//             public void Should_not_add_command_if_append_is_not_used_in_when_method()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent();
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public Task PerformSome() {
//                             return Task.CompletedTask;
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Empty(first.Commands);
//             }
//
//             [Fact]
//             public void Should_detect_event_produced_by_append_with_inline_expression()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System.Threading.Tasks;
//
//                         namespace TestDomain;
//
//                         public record SomeEvent();
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public Task PerformSome()
//                           {
//                               return Stream.Session(
//                                   ctx => ctx.Append(new SomeEvent()));
//                           }
//                         }
//                       """,
//                     filePath: @"C:\TestDomain\SomeAggregate.cs");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Commands);
//
//                 var firstCommand = first.Commands.First();
//                 Assert.Equal("PerformSome", firstCommand.CommandName);
//
//                 var producesEvent = firstCommand.ProducesEvents.First();
//                 Assert.Equal("Some.Event", producesEvent.EventName);
//                 Assert.Equal("TestDomain", producesEvent.Namespace);
//                 Assert.Equal("SomeEvent", producesEvent.TypeName);
//                 Assert.Equal("SomeAggregate.cs", producesEvent.File);
//             }
//
//             [Fact]
//             public void Should_detect_event_produced_by_append_with_body_expression()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System.Threading.Tasks;
//
//                         namespace TestDomain;
//
//                         public record SomeEvent();
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public Task PerformSome()
//                           {
//                             return Stream.Session(
//                                 ctx => {
//                                     return ctx.Append(new SomeEvent());
//                                 });
//                           }
//                         }
//                       """,
//                     filePath: @"C:\TestDomain\SomeAggregate.cs");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Commands);
//
//                 var firstCommand = first.Commands.First();
//                 Assert.Equal("PerformSome", firstCommand.CommandName);
//
//                 var producesEvent = firstCommand.ProducesEvents.First();
//                 Assert.Equal("Some.Event", producesEvent.EventName);
//                 Assert.Equal("TestDomain", producesEvent.Namespace);
//                 Assert.Equal("SomeEvent", producesEvent.TypeName);
//                 Assert.Equal("SomeAggregate.cs", producesEvent.File);
//             }
//
//             [Fact]
//             public void Should_detect_event_produced_by_append_with_inline_expression_with_async_await()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System.Threading.Tasks;
//
//                         namespace TestDomain;
//
//                         public record SomeEvent();
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public async Task PerformSome()
//                           {
//                              await Stream.Session(
//                                 ctx => {
//                                     return ctx.Append(new SomeEvent());
//                                 });
//                           }
//                         }
//                       """,
//                     filePath: @"C:\TestDomain\SomeAggregate.cs");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Commands);
//
//                 var firstCommand = first.Commands.First();
//                 Assert.Equal("PerformSome", firstCommand.CommandName);
//
//                 var producesEvent = firstCommand.ProducesEvents.First();
//                 Assert.Equal("Some.Event", producesEvent.EventName);
//                 Assert.Equal("TestDomain", producesEvent.Namespace);
//                 Assert.Equal("SomeEvent", producesEvent.TypeName);
//                 Assert.Equal("SomeAggregate.cs", producesEvent.File);
//             }
//
//             [Fact]
//             public void Should_detect_event_produced_by_append_with_body_expression_with_async_await()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System.Threading.Tasks;
//
//                         namespace TestDomain;
//
//                         public record SomeEvent();
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public async Task PerformSome()
//                           {
//                              await Stream.Session(ctx =>  ctx.Append(new SomeEvent()));
//                           }
//                         }
//                       }
//                       """,
//                     filePath: @"C:\TestDomain\SomeAggregate.cs");
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Commands);
//
//                 var firstCommand = first.Commands.First();
//                 Assert.Equal("PerformSome", firstCommand.CommandName);
//
//                 var producesEvent = firstCommand.ProducesEvents.First();
//                 Assert.Equal("Some.Event", producesEvent.EventName);
//                 Assert.Equal("TestDomain", producesEvent.Namespace);
//                 Assert.Equal("SomeEvent", producesEvent.TypeName);
//                 Assert.Equal("SomeAggregate.cs", producesEvent.File);
//             }
//         }
//
//         public class AnalyzePostWhenMethod
//         {
//             [Fact]
//             public void Should_detect_post_when_method_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//                       using System.Threading.Tasks;
//                       using ErikLieben.FA.ES.Documents;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent();
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//
//                           public Task PostWhen(IObjectDocument document, IEvent @event) {
//                             return Task.CompletedTask;
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.NotNull(first.PostWhen);
//                 var firstParam = first.PostWhen.Parameters.First();
//                 Assert.Equal("IObjectDocument", firstParam.Type);
//                 Assert.Equal("document", firstParam.Name);
//                 Assert.Equal("ErikLieben.FA.ES.Documents", firstParam.Namespace);
//
//                 var lastParam = first.PostWhen.Parameters.Last();
//                 Assert.Equal("IEvent", lastParam.Type);
//                 Assert.Equal("event", lastParam.Name);
//                 Assert.Equal("ErikLieben.FA.ES", lastParam.Namespace);
//             }
//
//             [Fact]
//             public void Should_not_find_post_when_method_if_not_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Attributes;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public record SomeEvent();
//
//                       public class SomeAggregate : Aggregate(stream)
//                       {
//                           public void When(SomeEvent @event)
//                           {
//                           }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Null(first.PostWhen);
//             }
//         }
//
//         public class AnalyzePublicGetterProperties
//         {
//             [Fact]
//             public void Should_detect_public_getter_properties_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//                       using System.Threading.Tasks;
//                       using ErikLieben.FA.ES.Documents;
//
//                       namespace TestDomain;
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                             public string SomeProperty { get; set; }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Single(first.Properties);
//             }
//
//             [Fact]
//             public void Should_not_detect_private_property_with_getter_properties_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//                       using System.Threading.Tasks;
//                       using ErikLieben.FA.ES.Documents;
//
//                       namespace TestDomain;
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                             private string SomeProperty { get; set; }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Empty(first.Properties);
//             }
//
//             [Fact]
//             public void Should_not_detect_public_property_with_private_getter_properties_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                             public string SomeProperty { private get; set; }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Empty(first.Properties);
//
//             }
//
//             [Fact]
//             public void Should_not_detect_internal_property_with_getter_properties_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//                       using System.Threading.Tasks;
//                       using ErikLieben.FA.ES.Documents;
//
//                       namespace TestDomain;
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                             private string SomeProperty { get; set; }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Empty(first.Properties);
//             }
//
//             [Fact]
//             public void Should_not_detect_public_property_with_internal_getter_properties_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                       using ErikLieben.FA.ES;
//                       using ErikLieben.FA.ES.Processors;
//
//                       namespace TestDomain;
//
//                       public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                       {
//                             public string SomeProperty { internal get; set; }
//                       }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Empty(first.Properties);
//
//
//             }
//         }
//
//         public class AnalyzeIdentifierTypeFromMetadata
//         {
//
//             [Fact]
//             public void Should_detect_identifier_type_from_metadata_if_present()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System;
//
//                         namespace TestDomain;
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                           public ObjectMetadata<Guid>? Metadata { get; private set; }
//                         }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Equal("Guid", first.IdentifierType);
//                 Assert.Equal("System", first.IdentifierTypeNamespace);
//             }
//
//
//             [Fact]
//             public void Should_set_to_string_if_no_metadata_is_detected()
//             {
//                 // Arrange
//                 var returnValue = new List<AggregateDefinition>();
//                 var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                     $$"""
//                         using ErikLieben.FA.ES;
//                         using ErikLieben.FA.ES.Processors;
//                         using System;
//
//                         namespace TestDomain;
//
//                         public class SomeAggregate(IEventStream stream) : Aggregate(stream)
//                         {
//                         }
//                       """);
//                 Assert.NotNull(classDeclaration);
//                 Assert.NotNull(compilation);
//                 Assert.NotNull(classSymbol);
//
//                 var sut = new AnalyzeAggregates(
//                     classSymbol,
//                     classDeclaration,
//                     semanticModel,
//                     compilation,
//                     @"C:\TestDomain\");
//
//                 // Act
//                 sut.Run(returnValue);
//
//                 // Assert
//                 var first = returnValue.First();
//                 Assert.Equal("String", first.IdentifierType);
//                 Assert.Equal("System", first.IdentifierTypeNamespace);
//             }
//         }
//
//
//         private static (
//             INamedTypeSymbol?,
//             ClassDeclarationSyntax?,
//             SemanticModel,
//             CSharpCompilation?) GetFromCode(
//                 string code,
//                 string testAssembly = "TestAssembly",
//                 string? filePath = "Test.cs")
//         {
//             SyntaxTree? syntaxTree = null;
//             if (string.IsNullOrWhiteSpace(filePath))
//             {
//                 syntaxTree = CSharpSyntaxTree.ParseText(code);
//             }
//             else
//             {
//                 syntaxTree = SyntaxFactory.ParseSyntaxTree(code,
//                     new CSharpParseOptions(),
//                     filePath);
//             }
//
//             var compilation = CSharpCompilation.Create(
//                 testAssembly,
//                 [syntaxTree],
//                 References,
//                 new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
//             );
//             var semanticModel = compilation.GetSemanticModel(syntaxTree);
//             var classNode = syntaxTree
//                 .GetRoot()
//                 .DescendantNodes()
//                 .OfType<ClassDeclarationSyntax>()
//                 .First();
//
//             return (
//                 semanticModel.GetDeclaredSymbol(classNode),
//                 classNode,
//                 semanticModel,
//                 compilation);
//         }
//
//         private static List<PortableExecutableReference> References { get; } =
//         [
//             MetadataReference.CreateFromFile(
//                 Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
//             MetadataReference.CreateFromFile(
//                 Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
//             MetadataReference.CreateFromFile(
//                 Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
//             MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
//             MetadataReference.CreateFromFile(typeof(IgnoreAttribute).Assembly.Location)
//         ];
//     }
// }
