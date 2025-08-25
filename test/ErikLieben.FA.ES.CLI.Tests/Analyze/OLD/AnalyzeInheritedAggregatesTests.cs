// using System.Runtime.InteropServices;
// using ErikLieben.FA.ES.Attributes;
// using ErikLieben.FA.ES.CLI.Analyze;
// using ErikLieben.FA.ES.CLI.Model;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using NSubstitute;
//
// namespace ErikLieben.FA.ES.CLI.Tests.Analyze;
//
// public class AnalyzeInheritedAggregatesTests
// {
//     public class Ctor
//     {
//         [Fact]
//         public void Should_throw_an_exception_if_typeSymbol_is_null()
//         {
//             // Arrange
//             var semanticModel = Substitute.For<SemanticModel>();
//             var solutionRootPath = string.Empty;
//
//             // Act
//             var exception = Assert.Throws<ArgumentNullException>(
//                 () => new AnalyzeInheritedAggregates(
//                     null!,
//                     semanticModel,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'typeSymbol')",
//                 exception.Message);
//         }
//
//         // [Fact]
//         // public void Should_throw_an_exception_if_classDeclaration_is_null()
//         // {
//         //     // Arrange
//         //     var classSymbol = Substitute.For<INamedTypeSymbol>();
//         //     var semanticModel = Substitute.For<SemanticModel>();
//         //     var compilation = CSharpCompilation.Create(
//         //         "MyCompilation",
//         //         syntaxTrees: [CSharpSyntaxTree.ParseText("public class ClassB {}")],
//         //         references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
//         //     var solutionRootPath = string.Empty;
//         //
//         //     // Act
//         //     var exception = Assert.Throws<ArgumentNullException>(
//         //         () => new AnalyzeInheritedAggregates(
//         //             classSymbol,
//         //             semanticModel,
//         //             solutionRootPath));
//         //
//         //     // Assert
//         //     Assert.Equal(
//         //         "Value cannot be null. (Parameter 'classDeclaration')",
//         //         exception.Message);
//         // }
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
//                 () => new AnalyzeInheritedAggregates(
//                     classSymbol,
//                     null!,
//                     solutionRootPath));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'semanticModel')",
//                 exception.Message);
//         }
//
//         // [Fact]
//         // public void Should_throw_an_exception_if_compilation_is_null()
//         // {
//         //     // Arrange
//         //     var classSymbol = Substitute.For<INamedTypeSymbol>();
//         //     var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//         //     var semanticModel = Substitute.For<SemanticModel>();
//         //     var solutionRootPath = string.Empty;
//         //
//         //     // Act
//         //     var exception = Assert.Throws<ArgumentNullException>(
//         //         () => new AnalyzeInheritedAggregates(
//         //             classSymbol,
//         //             semanticModel,
//         //             solutionRootPath));
//         //
//         //     // Assert
//         //     Assert.Equal(
//         //         "Value cannot be null. (Parameter 'compilation')",
//         //         exception.Message);
//         // }
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
//                 () => new AnalyzeInheritedAggregates(
//                     classSymbol,
//                     semanticModel,
//                     null!));
//
//             // Assert
//             Assert.Equal(
//                 "Value cannot be null. (Parameter 'solutionRootPath')",
//                 exception.Message);
//         }
//     }
//
//     public class AnalyzeWhenMethods
//     {
//         [Fact]
//         public void Should_only_process_aggregates_if_class_inherits_from_aggregate()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                 }
//
//                 public class Foo { }
//                 """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             Assert.Empty(returnValue);
//         }
//
//         [Fact]
//         public void Should_skip_aggregates_with_ignore_attribute()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public record SomeEvent(string Name);
//
//                 [Ignore("Only for testing purposes")]
//                 public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                   public Task PerformCommand(string name) {
//                       return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                   }
//                 }
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                 }
//                 """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             Assert.Empty(returnValue);
//         }
//
//         [Fact]
//         public void Should_skip_aggregate_in_namespace_ErikLieben_fa_es()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace ErikLieben.FA.ES;
//
//                 public record SomeEvent(string Name);
//
//                 public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                   public Task PerformCommand(string name) {
//                       return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                   }
//                 }
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                 }
//                 """,
//                 testAssembly: "ErikLieben.FA.ES");
//
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             Assert.Empty(returnValue);
//         }
//
//         [Fact]
//         public void Should_skip_aggregate_if_its_not_the_solution_root_folder()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public record SomeEvent(string Name);
//
//                 public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                   public Task PerformCommand(string name) {
//                       return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                   }
//                 }
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                 }
//                 """,
//                 filePath: @"c:\TestDomain2\App\File\C.cs");
//
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             Assert.Empty(returnValue);
//         }
//
//         [Fact]
//         public void Should_detect_simple_event_with_event_name_attribute_in_when_method()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 [EventName("Some.EventName")]
//                 public record SomeEvent(string Name);
//
//                  public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                    public Task PerformCommand(string name) {
//                        return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                    }
//                  }
//
//                  public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                  {
//                  }
//                 """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformCommand", firstCommand.CommandName);
//             Assert.True(firstCommand.RequiresAwait);
//             Assert.Equal("System", firstCommand.Parameters.First().Namespace);
//             Assert.Equal("String", firstCommand.Parameters.First().Type);
//             Assert.Equal("name", firstCommand.Parameters.First().Name);
//             Assert.Equal("Some.EventName", firstCommand.ProducesEvents.First().EventName);
//             Assert.Equal("TestDomain", firstCommand.ProducesEvents.First().Namespace);
//             Assert.Equal("SomeEvent", firstCommand.ProducesEvents.First().TypeName);
//         }
//
//         [Fact]
//         public void Should_detect_simple_event_without_event_name_attribute_in_when_method()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public record SomeEvent(string Name);
//
//                  public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                    public Task PerformCommand(string name) {
//                        return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                    }
//                  }
//
//                  public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                  {
//                  }
//                 """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformCommand", firstCommand.CommandName);
//             Assert.True(firstCommand.RequiresAwait);
//             Assert.Equal("System", firstCommand.Parameters.First().Namespace);
//             Assert.Equal("String", firstCommand.Parameters.First().Type);
//             Assert.Equal("name", firstCommand.Parameters.First().Name);
//             Assert.Equal("Some.Event", firstCommand.ProducesEvents.First().EventName);
//             Assert.Equal("TestDomain", firstCommand.ProducesEvents.First().Namespace);
//             Assert.Equal("SomeEvent", firstCommand.ProducesEvents.First().TypeName);
//         }
//
//         [Fact]
//         public void Should_detect_simple_event_with_event_name_attribute_but_with_empty_value_in_when_method()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public record SomeEvent();
//
//                  public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                    public Task PerformCommand(string name) {
//                        return Stream.Session(ctx => ctx.Append(new SomeEvent(name)));
//                    }
//                  }
//
//                  public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                  {
//                  }
//
//                 """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformCommand", firstCommand.CommandName);
//             Assert.True(firstCommand.RequiresAwait);
//             Assert.Equal("System", firstCommand.Parameters.First().Namespace);
//             Assert.Equal("String", firstCommand.Parameters.First().Type);
//             Assert.Equal("name", firstCommand.Parameters.First().Name);
//             Assert.Equal("Some.Event", firstCommand.ProducesEvents.First().EventName);
//             Assert.Equal("TestDomain", firstCommand.ProducesEvents.First().Namespace);
//             Assert.Equal("SomeEvent", firstCommand.ProducesEvents.First().TypeName);
//         }
//     }
//
//     public class AnalyzeCommandMethods
//     {
//         [Fact]
//         public void Should_not_add_command_if_append_is_not_used_in_when_method()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public record SomeEvent();
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                   {
//                       public void When(SomeEvent @event)
//                       {
//                       }
//
//                       public Task PerformSome() {
//                         return Task.CompletedTask;
//                       }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Empty(first.Commands);
//         }
//
//         [Fact]
//         public void Should_detect_event_produced_by_append_with_inline_expression()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Processors;
//                     using System.Threading.Tasks;
//
//                     namespace TestDomain;
//
//                     public record SomeEvent();
//
//                     public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                     {
//                       public void When(SomeEvent @event)
//                       {
//                       }
//
//                       public Task PerformSome()
//                       {
//                           return Stream.Session(
//                               ctx => ctx.Append(new SomeEvent()));
//                       }
//                     }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """,
//                 filePath: @"C:\TestDomain\SomeAggregate.cs");
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformSome", firstCommand.CommandName);
//
//             var producesEvent = firstCommand.ProducesEvents.First();
//             Assert.Equal("Some.Event", producesEvent.EventName);
//             Assert.Equal("TestDomain", producesEvent.Namespace);
//             Assert.Equal("SomeEvent", producesEvent.TypeName);
//             Assert.Equal("SomeAggregate.cs", producesEvent.File);
//         }
//
//         [Fact]
//         public void Should_detect_event_produced_by_append_with_body_expression()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Processors;
//                     using System.Threading.Tasks;
//
//                     namespace TestDomain;
//
//                     public record SomeEvent();
//
//                     public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                     {
//                       public void When(SomeEvent @event)
//                       {
//                       }
//
//                       public Task PerformSome()
//                       {
//                         return Stream.Session(
//                             ctx => {
//                                 return ctx.Append(new SomeEvent());
//                             });
//                       }
//                     }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """,
//                 filePath: @"C:\TestDomain\SomeAggregate.cs");
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformSome", firstCommand.CommandName);
//
//             var producesEvent = firstCommand.ProducesEvents.First();
//             Assert.Equal("Some.Event", producesEvent.EventName);
//             Assert.Equal("TestDomain", producesEvent.Namespace);
//             Assert.Equal("SomeEvent", producesEvent.TypeName);
//             Assert.Equal("SomeAggregate.cs", producesEvent.File);
//         }
//
//         [Fact]
//         public void Should_detect_event_produced_by_append_with_inline_expression_with_async_await()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Processors;
//                     using System.Threading.Tasks;
//
//                     namespace TestDomain;
//
//                     public record SomeEvent();
//
//                     public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                     {
//                       public void When(SomeEvent @event)
//                       {
//                       }
//
//                       public async Task PerformSome()
//                       {
//                          await Stream.Session(
//                             ctx => {
//                                 return ctx.Append(new SomeEvent());
//                             });
//                       }
//                     }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """,
//                 filePath: @"C:\TestDomain\SomeAggregate.cs");
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformSome", firstCommand.CommandName);
//
//             var producesEvent = firstCommand.ProducesEvents.First();
//             Assert.Equal("Some.Event", producesEvent.EventName);
//             Assert.Equal("TestDomain", producesEvent.Namespace);
//             Assert.Equal("SomeEvent", producesEvent.TypeName);
//             Assert.Equal("SomeAggregate.cs", producesEvent.File);
//         }
//
//         [Fact]
//         public void Should_detect_event_produced_by_append_with_body_expression_with_async_await()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                     using ErikLieben.FA.ES;
//                     using ErikLieben.FA.ES.Processors;
//                     using System.Threading.Tasks;
//
//                     namespace TestDomain;
//
//                     public record SomeEvent();
//
//                     public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                     {
//                       public void When(SomeEvent @event)
//                       {
//                       }
//
//                       public async Task PerformSome()
//                       {
//                          await Stream.Session(ctx =>  ctx.Append(new SomeEvent()));
//                       }
//                     }
//
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """,
//                 filePath: @"C:\TestDomain\SomeAggregate.cs");
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Commands);
//
//             var firstCommand = first.Commands.First();
//             Assert.Equal("PerformSome", firstCommand.CommandName);
//
//             var producesEvent = firstCommand.ProducesEvents.First();
//             Assert.Equal("Some.Event", producesEvent.EventName);
//             Assert.Equal("TestDomain", producesEvent.Namespace);
//             Assert.Equal("SomeEvent", producesEvent.TypeName);
//             Assert.Equal("SomeAggregate.cs", producesEvent.File);
//         }
//     }
//
//     public class AnalyzePublicGetterProperties
//     {
//         [Fact]
//         public void Should_detect_public_getter_properties_if_present()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//                   using System.Threading.Tasks;
//                   using ErikLieben.FA.ES.Documents;
//
//                   namespace TestDomain;
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                       public string SomeProperty { get; set; }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Single(first.Properties);
//         }
//
//         [Fact]
//         public void Should_not_detect_private_property_with_getter_properties_if_present()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//                   using System.Threading.Tasks;
//                   using ErikLieben.FA.ES.Documents;
//
//                   namespace TestDomain;
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                       private string SomeProperty { get; set; }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Empty(first.Properties);
//         }
//
//         [Fact]
//         public void Should_not_detect_public_property_with_private_getter_properties_if_present()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                       public string SomeProperty { private get; set; }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Empty(first.Properties);
//         }
//
//         [Fact]
//         public void Should_not_detect_internal_property_with_getter_properties_if_present()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//                   using System.Threading.Tasks;
//                   using ErikLieben.FA.ES.Documents;
//
//                   namespace TestDomain;
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                       internal string SomeProperty { public get; set; }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Empty(first.Properties);
//         }
//
//         [Fact]
//         public void Should_not_detect_public_property_with_internal_getter_properties_if_present()
//         {
//             // Arrange
//             var returnValue = new List<InheritedAggregateDefinition>();
//             var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//                 $$"""
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//
//                   public class InheritedAggregate(IEventStream stream) : TestAggregate(stream) {
//                       public string SomeProperty { internal get; set; }
//                   }
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                   """);
//             Assert.NotNull(classDeclaration);
//             Assert.NotNull(compilation);
//             Assert.NotNull(classSymbol);
//
//             var sut = new AnalyzeInheritedAggregates(
//                 classSymbol,
//                 semanticModel,
//                 @"C:\TestDomain\");
//
//             // Act
//             sut.Run(returnValue);
//
//             // Assert
//             var first = returnValue.First();
//             Assert.Empty(first.Properties);
//         }
//     }
//
//
//     private static (
//         INamedTypeSymbol?,
//         ClassDeclarationSyntax?,
//         SemanticModel,
//         CSharpCompilation?) GetFromCode(
//             string code,
//             string testAssembly = "TestAssembly",
//             string? filePath = "Test.cs")
//     {
//         SyntaxTree? syntaxTree = null;
//         if (string.IsNullOrWhiteSpace(filePath))
//         {
//             syntaxTree = CSharpSyntaxTree.ParseText(code);
//         }
//         else
//         {
//             syntaxTree = SyntaxFactory.ParseSyntaxTree(code,
//                 new CSharpParseOptions(),
//                 filePath);
//         }
//
//         var compilation = CSharpCompilation.Create(
//             testAssembly,
//             [syntaxTree],
//             References,
//             new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
//         );
//         var semanticModel = compilation.GetSemanticModel(syntaxTree);
//         var classNode = syntaxTree
//             .GetRoot()
//             .DescendantNodes()
//             .OfType<ClassDeclarationSyntax>()
//             .First();
//
//         return (
//             semanticModel.GetDeclaredSymbol(classNode),
//             classNode,
//             semanticModel,
//             compilation);
//     }
//
//     private static List<PortableExecutableReference> References { get; } =
//     [
//         MetadataReference.CreateFromFile(
//             Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll")),
//         MetadataReference.CreateFromFile(
//             Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
//         MetadataReference.CreateFromFile(
//             Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Collections.dll")),
//         MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
//         MetadataReference.CreateFromFile(typeof(IgnoreAttribute).Assembly.Location)
//     ];
// }
