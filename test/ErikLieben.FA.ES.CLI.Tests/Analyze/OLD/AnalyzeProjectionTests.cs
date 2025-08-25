// using System.Runtime.InteropServices;
// using ErikLieben.FA.ES.Attributes;
// using ErikLieben.FA.ES.CLI.Analyze;
// using ErikLieben.FA.ES.CLI.Model;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
//
// namespace ErikLieben.FA.ES.CLI.Tests.Analyze;
//
// public class AnalyzeProjectionTests
// {
//     public class Foo
//     {
//         //[Fact]
//         //public void Should_()
//         //{
//         //    // Arrange
//         //    var returnValue = new List<ProjectionDefinition>();
//         //    var (classSymbol, classDeclaration, semanticModel, compilation) = GetFromCode(
//         //        """
//         //        using ErikLieben.FA.ES;
//         //        using ErikLieben.FA.ES.Documents;
//         //        using ErikLieben.FA.ES.Projections;
//         //        using ErikLieben.FA.ES.VersionTokenParts;
//         //        using System;
//         //        using System.Threading.Tasks;
//         //        using System.Collections.Generic;
//
//         //        namespace TestDomain;
//
//         //        public record FeatureFlagDisabled(string Name);
//
//         //        public class MyProjection : Projection
//         //        {
//         //            public List<string> FeatureFlags { get; set; } = [];
//
//         //            private void When(FeatureFlagDisabled @event)
//         //            {
//         //                var featureFlag = @event.Name.Trim();
//         //                FeatureFlags.Remove(featureFlag);
//         //            }
//
//         //            public override Task Fold(IEvent @event, IObjectDocument document)
//         //            {
//         //                throw new NotImplementedException();
//         //            }
//
//         //            public override Dictionary<ObjectIdentifier, VersionIdentifier> VersionIndex { get; set; }
//
//         //            public override void LoadFromJson(string json)
//         //            {
//         //                throw new NotImplementedException();
//         //            }
//
//         //            public override string ToJson()
//         //            {
//         //                throw new NotImplementedException();
//         //            }
//
//         //            protected override Dictionary<string, IProjectionWhenParameterValueFactory> WhenParameterValueFactories { get; }
//         //        }
//         //        """);
//         //    Assert.NotNull(classDeclaration);
//         //    Assert.NotNull(compilation);
//         //    Assert.NotNull(classSymbol);
//
//         //    var sut = new AnalyzeProjections(
//         //        classSymbol,
//         //        classDeclaration,
//         //        semanticModel,
//         //        compilation,
//         //        @"C:\TestDomain\");
//
//         //    // Act
//         //    sut.Run(options);
//
//
//         //    // Assert
//         //    var first = returnValue.First();
//         //    Assert.Empty(first.Properties);
//         //}
//     }
//
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
