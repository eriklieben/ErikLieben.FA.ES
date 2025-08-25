// using System.Collections.Immutable;
// using System.Runtime.InteropServices;
// using ErikLieben.FA.ES.Attributes;
// using ErikLieben.FA.ES.CLI.Analyze;
// using ErikLieben.FA.ES.CLI.Analyze.Helpers;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Text;
// using NSubstitute;
//
// namespace ErikLieben.FA.ES.CLI.Tests.Analyze;
//
// public class RoslynHelperTests
// {
//     public class IgnoreAggregate
//     {
//         [Fact]
//         public void Should_ignore_aggregate()
//         {
//             // Arrange
//             var (classSymbol, semanticModel) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   [Ignore("Only for testing purposes")]
//                   public class IgnoreMeAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """);
//
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IgnoreAggregate(classSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_not_ignore_aggregate_if_namedTypeSymbol_is_null()
//         {
//             // Arrange
//             var (_, semanticModel) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public class IgnoreMeAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """
//             );
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IgnoreAggregate(null!);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_not_ignore_aggregate_if_not_marked_with_ignore_attribute()
//         {
//             // Arrange
//             var (classSymbol, semanticModel) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public class DoNotIgnoreMeAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """
//             );
//
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IgnoreAggregate(classSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class GetObjectName
//     {
//         [Fact]
//         public void Should_get_the_object_name_from_the_class_symbol()
//         {
//             // Arrange
//             var (classSymbol, _) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   [ObjectName("customAggregate")]
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """
//             );
//             Assert.NotNull(classSymbol);
//
//             // Act
//             var result = RoslynHelper.GetObjectName(classSymbol);
//
//             // Assert
//             Assert.Equal("customAggregate", result);
//         }
//
//
//         [Fact]
//         public void Should_generate_name_bases_off_class_name()
//         {
//             // Arrange
//             var (classSymbol, _) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """
//             );
//             Assert.NotNull(classSymbol);
//
//             // Act
//             var result = RoslynHelper.GetObjectName(classSymbol);
//
//             // Assert
//             Assert.Equal("testAggregate", result);
//         }
//     }
//
//     public class InheritsFromAggregate
//     {
//         [Fact]
//         public void Should_return_true_if_clasS_inherits_from_aggregate()
//         {
//             // Arrange
//             var (classSymbol, _) = GetClassSymbol(
//                 """
//                   using ErikLieben.FA.ES;
//                   using ErikLieben.FA.ES.Attributes;
//                   using ErikLieben.FA.ES.Processors;
//
//                   namespace TestDomain;
//
//                   public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                   {
//                   }
//                 """
//             );
//             Assert.NotNull(classSymbol);
//
//             // Act
//             var result = RoslynHelper.InheritsFromAggregate(classSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_class_is_null()
//         {
//             // Act
//             var result = RoslynHelper.InheritsFromAggregate(null!);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_baseType_is_null()
//         {
//             // Arrange
//             var typeMock = Substitute.For<INamedTypeSymbol>();
//             INamedTypeSymbol? baseType = null;
//             typeMock.BaseType.Returns(baseType);
//
//             // Act
//             var result = RoslynHelper.InheritsFromAggregate(null!);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_inherits_from_other_aggregate_class()
//         {
//             // Arrange
//             var (classSymbol, _) = GetClassSymbol(
//                 """
//                   namespace TestDomain;
//
//                   public class Aggregate {}
//
//                   public class TestAggregate() : Aggregate
//                   {
//                   }
//                 """
//             );
//             Assert.NotNull(classSymbol);
//
//             // Act
//             var result = RoslynHelper.InheritsFromAggregate(classSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class IsReturnTypeAwaitable
//     {
//         [Fact]
//         public void Should_return_true_if_method_has_return_type_task()
//         {
//             // Arrange
//             var (methodDeclarationSymbol, semanticModel) = GetMethodDeclarationSyntax(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                     public Task Go() { return Task.CompletedTask; }
//                 }
//                 """
//             );
//             Assert.NotNull(methodDeclarationSymbol);
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IsReturnTypeAwaitable(methodDeclarationSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_true_if_method_has_return_type_value_task()
//         {
//             // Arrange
//             var (methodDeclarationSymbol, semanticModel) = GetMethodDeclarationSyntax(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                     public ValueTask Go() { return new ValueTask; }
//                 }
//                 """
//             );
//             Assert.NotNull(methodDeclarationSymbol);
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IsReturnTypeAwaitable(methodDeclarationSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         // TODO: There currently is support for awaitable with a return value,
//         // but I am not sure if this is making sense, because we're not dong anything
//         // with the return value.
//
//         [Fact]
//         public void Should_return_true_if_method_has_return_type_value_task_of_t()
//         {
//             // Arrange
//             var (methodDeclarationSymbol, semanticModel) = GetMethodDeclarationSyntax(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                     public ValueTask<int> Go() { return new ValueTask(1); }
//                 }
//                 """
//             );
//             Assert.NotNull(methodDeclarationSymbol);
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IsReturnTypeAwaitable(methodDeclarationSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_method_has_void_return_type()
//         {
//             // Arrange
//             var (methodDeclarationSymbol, semanticModel) = GetMethodDeclarationSyntax(
//                 """
//                 using ErikLieben.FA.ES;
//                 using ErikLieben.FA.ES.Attributes;
//                 using ErikLieben.FA.ES.Processors;
//
//                 namespace TestDomain;
//
//                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
//                 {
//                     public void Go() { }
//                 }
//                 """
//             );
//             Assert.NotNull(methodDeclarationSymbol);
//             var sut = new RoslynHelper(
//                 semanticModel,
//                 string.Empty);
//
//             // Act
//             var result = sut.IsReturnTypeAwaitable(methodDeclarationSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//
// //         [Fact]
// //         public void Should_throw_an_exception_if_method_declaration_is_null()
// //         {
// //             // Arrange
// //             var (methodDeclarationSymbol, semanticModel) = GetMethodDeclarationSyntax(
// //                 """
// //                 using ErikLieben.FA.ES;
// //                 using ErikLieben.FA.ES.Attributes;
// //                 using ErikLieben.FA.ES.Processors;
// //
// //                 namespace TestDomain;
// //
// //                 public class TestAggregate(IEventStream stream) : Aggregate(stream)
// //                 {
// //                     public void Go() { }
// //                 }
// //                 """
// //             );
// //             Assert.NotNull(methodDeclarationSymbol);
// //             var sut = new RoslynHelper(
// //                 semanticModel,
// //                 string.Empty);
// //
// //             // Act & Assert
// //             var result = Assert.Throws<ArgumentNullException>(
// //                 () => sut.IsReturnTypeAwaitable(null!));
// //         }
//
//         private static (MethodDeclarationSyntax?, SemanticModel) GetMethodDeclarationSyntax(string code)
//         {
//             var syntaxTree = CSharpSyntaxTree.ParseText(code);
//             var compilation = CSharpCompilation.Create(
//                 "TestAssembly",
//                 [syntaxTree],
//                 References,
//                 new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
//             );
//             var semanticModel = compilation.GetSemanticModel(syntaxTree);
//             var methodDeclarationSyntax = syntaxTree
//                 .GetRoot()
//                 .DescendantNodes()
//                 .OfType<MethodDeclarationSyntax>()
//                 .First();
//
//             return (methodDeclarationSyntax, semanticModel);
//         }
//     }
//
//     public class IsSystemNullable
//     {
//         [Fact]
//         public void Should_return_true_if_nullable_annotation_is_annotated()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.NullableAnnotation.Returns(NullableAnnotation.Annotated);
//             typeSymbol.OriginalDefinition.ToDisplayString().Returns("Namespace.CustomType");
//
//             // Act
//             var result = RoslynHelper.IsSystemNullable(typeSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_nullable_annotation_is_not_annotated()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.NullableAnnotation.Returns(NullableAnnotation.NotAnnotated);
//             typeSymbol.OriginalDefinition.ToDisplayString().Returns("Namespace.CustomType");
//
//             // Act
//             var result = RoslynHelper.IsSystemNullable(typeSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_nullable_annotation_is_none()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.NullableAnnotation.Returns(NullableAnnotation.None);
//             typeSymbol.OriginalDefinition.ToDisplayString().Returns("Namespace.CustomType");
//
//             // Act
//             var result = RoslynHelper.IsSystemNullable(typeSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_original_definition_is_system_nullable()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.NullableAnnotation.Returns(NullableAnnotation.Annotated);
//             typeSymbol.OriginalDefinition.ToDisplayString().Returns("System.Nullable<T>");
//
//             // Act
//             var result = RoslynHelper.IsSystemNullable(typeSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class IsExplicitlyNullableType
//     {
//         [Fact]
//         public void Should_return_true_if_type_is_reference_type()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.IsReferenceType.Returns(true);
//
//             // Act
//             var result = RoslynHelper.IsExplicitlyNullableType(typeSymbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_type_is_not_reference_type()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.IsReferenceType.Returns(false);
//
//             // Act
//             var result = RoslynHelper.IsExplicitlyNullableType(typeSymbol);
//
//             // Assert
//             Assert.False(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_type_is_null()
//         {
//             // Act
//             var result = RoslynHelper.IsExplicitlyNullableType(null!);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class IsPartial
//     {
//         [Fact]
//         public void Should_return_true_if_class_is_partial()
//         {
//             // Arrange
//             var classDeclaration = SyntaxFactory
//                 .ClassDeclaration("TestAggregate")
//                 .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
//
//             // Act
//             var result = RoslynHelper.IsPartial(classDeclaration);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_class_is_not_partial()
//         {
//             // Arrange
//             var classDeclaration = SyntaxFactory.ClassDeclaration("TestAggregate");
//
//             // Act
//             var result = RoslynHelper.IsPartial(classDeclaration);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class GetFullNamespace
//     {
//
//     }
//
//     public class GetFullTypeName
//     {
//         [Fact]
//         public void Should_return_just_its_name_if_type_is_not_nested()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.Name.Returns("SelfType");
//             typeSymbol.ContainingType.Returns((INamedTypeSymbol?)null);
//
//             // Act
//             var result = RoslynHelper.GetFullTypeName(typeSymbol);
//
//             // Assert
//             Assert.Equal("SelfType", result);
//         }
//
//         [Fact]
//         public void Should_return_container_type_and_self_type_if_type_is_nested()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.Name.Returns("SelfType");
//             var containingType = Substitute.For<INamedTypeSymbol>();
//             containingType.Name.Returns("ContainerType");
//             containingType.ContainingType.Returns((INamedTypeSymbol?)null);
//             typeSymbol.ContainingType.Returns(containingType);
//
//             // Act
//             var result = RoslynHelper.GetFullTypeName(typeSymbol);
//
//             // Assert
//             Assert.Equal("ContainerType.SelfType", result);
//         }
//
//         [Fact]
//         public void
//             Should_return_super_container_type_and_container_type_and_self_type_if_type_is_nested_in_multiple_levels()
//         {
//             // Arrange
//             var typeSymbol = Substitute.For<ITypeSymbol>();
//             typeSymbol.Name.Returns("SelfType");
//             var containingType = Substitute.For<INamedTypeSymbol>();
//             containingType.Name.Returns("ContainerType");
//             var superContainingType = Substitute.For<INamedTypeSymbol>();
//             superContainingType.Name.Returns("SuperContainerType");
//             superContainingType.ContainingType.Returns((INamedTypeSymbol?)null);
//             typeSymbol.ContainingType.Returns(containingType);
//             containingType.ContainingType.Returns(superContainingType);
//
//             // Act
//             var result = RoslynHelper.GetFullTypeName(typeSymbol);
//
//             // Assert
//             Assert.Equal("SuperContainerType.ContainerType.SelfType", result);
//         }
//
//     }
//
//     public class IsInSolutionRootFolder
//     {
//         [Fact]
//         public void Should_return_true_if_file_path_is_in_solutionRootFolder()
//         {
//             // Arrange
//             var symbol = Substitute.For<ISymbol>();
//             var syntaxTree = SyntaxFactory.ParseSyntaxTree("class C { }",
//                 new CSharpParseOptions(),
//                 @"c:\Repository\App\File\C.cs");
//             var textSpan = TextSpan.FromBounds(0, 1);
//             symbol.Locations.Returns([
//                 Location.Create(syntaxTree, textSpan),
//             ]);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var result = sut.IsInSolutionRootFolder(symbol);
//
//             // Assert
//             Assert.True(result);
//         }
//
//         [Fact]
//         public void Should_return_false_if_file_path_is_outside_of_the_solutionRootFolder()
//         {
//             // Arrange
//             var symbol = Substitute.For<ISymbol>();
//             var syntaxTree = SyntaxFactory.ParseSyntaxTree("class C { }",
//                 new CSharpParseOptions(),
//                 @"c:\Repository\App2\File\C.cs");
//             var textSpan = TextSpan.FromBounds(0, 1);
//             symbol.Locations.Returns([
//                 Location.Create(syntaxTree, textSpan),
//             ]);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var result = sut.IsInSolutionRootFolder(symbol);
//
//             // Assert
//             Assert.False(result);
//         }
//     }
//
//     public class GetFilePath
//     {
//         [Fact]
//         public void Should_return_the_relative_path_to_the_file_if_symbol_has_location()
//         {
//             // Arrange
//             var symbol = Substitute.For<ISymbol>();
//             var syntaxTree = SyntaxFactory.ParseSyntaxTree("class C { }",
//                 new CSharpParseOptions(),
//                 @"c:\Repository\App\File\C.cs");
//             var textSpan = TextSpan.FromBounds(0, 1);
//             symbol.Locations.Returns([
//                 Location.Create(syntaxTree, textSpan),
//             ]);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var result = sut.GetFilePaths(symbol);
//
//             // Assert
//             Assert.Equal(
//                 @"File\C.cs",
//                 result.First());
//         }
//
//         [Fact]
//         public void Should_return_the_full_path_to_the_file_if_symbol_has_location()
//         {
//             // Arrange
//             var symbol = Substitute.For<ISymbol>();
//             var syntaxTree = SyntaxFactory.ParseSyntaxTree("class C { }",
//                 new CSharpParseOptions(),
//                 @"c:\Repository\App2\File\C.cs");
//             var textSpan = TextSpan.FromBounds(0, 1);
//             symbol.Locations.Returns([
//                 Location.Create(syntaxTree, textSpan),
//             ]);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var result = sut.GetFilePaths(symbol);
//
//             // Assert
//             Assert.Equal(
//                 @"..\App2\File\C.cs",
//                 result.First());
//         }
//
//         [Fact]
//         public void Should_get_the_full_file_path_from_the_metadata_reference()
//         {
//             // Arrange
//             var syntaxTree = CSharpSyntaxTree.ParseText("""
//                 using System;
//                 public class TestClass
//                 {
//                     public string TestMethod() => DateTime.Now.ToString();
//                 }
//                 """);
//             var metadataReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
//             var compilation = CSharpCompilation.Create(
//                 assemblyName: "TestAssembly",
//                 syntaxTrees: [syntaxTree],
//                 references: [metadataReference]);
//             var symbol = compilation.GetTypeByMetadataName("System.DateTime");
//             Assert.NotNull(symbol);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var filePath = sut.GetFilePaths(symbol);
//             var result = filePath.First().Split("ErikLieben.FA.ES.CLI.Tests")[1];
//
//             // Assert
//             Assert.Equal(@"\bin\Debug\net9.0\System.Private.CoreLib.dll", result);
//         }
//
//
//         [Fact]
//         public void Should_return_empty_list_if_symbol_has_no_location()
//         {
//             // Arrange
//             var symbol = Substitute.For<ISymbol>();
//             symbol.Locations.Returns([
//
//             ]);
//             var sut = new RoslynHelper(null!, @"c:\Repository\App\");
//
//             // Act
//             var result = sut.GetFilePaths(symbol);
//
//             // Assert
//             Assert.Empty(result);
//         }
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
//
//     private static (INamedTypeSymbol?, SemanticModel) GetClassSymbol(string code)
//     {
//         var syntaxTree = CSharpSyntaxTree.ParseText(code);
//         var compilation = CSharpCompilation.Create(
//             "TestAssembly",
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
//         return (semanticModel.GetDeclaredSymbol(classNode), semanticModel);
//     }
// }
