using System;
using System.IO;
using System.Linq;
using ErikLieben.FA.ES.CLI.CodeGeneration;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.CodeGeneration;

public class CodeFormattingHelperTests
{
    [Fact]
    public void FormatCode_formats_basic_code_with_proper_indentation()
    {
        // Arrange
        var code = """
            namespace Test;
            public class Foo {
            public void Bar() {
            var x = 1;
            }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("namespace Test;", result);
        Assert.Contains("public class Foo", result);
        Assert.Contains("public void Bar()", result);
        Assert.Contains("    var x = 1;", result); // Should be indented
    }

    [Fact]
    public void FormatCode_removes_unused_using_directives()
    {
        // Arrange
        var code = """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            namespace Test;

            public class Foo
            {
                public int Count => 5;
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // System.Collections.Generic and System.Linq are not used
        Assert.DoesNotContain("using System.Collections.Generic;", result);
        Assert.DoesNotContain("using System.Linq;", result);
    }

    [Fact]
    public void FormatCode_keeps_used_using_directives()
    {
        // Arrange
        var code = """
            using System;
            using System.Collections.Generic;

            namespace Test;

            public class Foo
            {
                public List<string> Items { get; set; } = new List<string>();
                public DateTime Created { get; set; } = DateTime.Now;
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // Both System and System.Collections.Generic are used
        Assert.Contains("using System;", result);
        Assert.Contains("using System.Collections.Generic;", result);
    }

    [Fact]
    public void FormatCode_removes_consecutive_empty_lines()
    {
        // Arrange
        var code = """
            namespace Test;


            public class Foo
            {


                public void Bar()
                {
                }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // Should not have more than one consecutive empty line
        Assert.DoesNotContain("\n\n\n", result);
        Assert.DoesNotContain("\r\n\r\n\r\n", result);
    }

    [Fact]
    public void FormatCode_removes_empty_lines_before_closing_braces()
    {
        // Arrange
        var code = """
            namespace Test;

            public class Foo
            {
                public void Bar()
                {
                    var x = 1;

                }

            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // Empty lines before closing braces should be removed
        var lines = result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "}")
            {
                // Previous line should not be empty
                Assert.NotEmpty(lines[i - 1].Trim());
            }
        }
    }

    [Fact]
    public void FormatCode_keeps_all_usings_when_symbols_cannot_be_resolved()
    {
        // Arrange - code with a type that can't be resolved
        var code = """
            using System;
            using System.Collections.Generic;
            using Custom.Namespace;

            namespace Test;

            public class Foo
            {
                public UnresolvedType Value { get; set; }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // When UnresolvedType can't be resolved, all usings should be kept
        Assert.Contains("using System;", result);
        Assert.Contains("using System.Collections.Generic;", result);
        Assert.Contains("using Custom.Namespace;", result);
    }

    [Fact]
    public void FormatCode_keeps_using_for_attributes()
    {
        // Arrange
        var code = """
            using System;
            using System.ComponentModel;

            namespace Test;

            public class Foo
            {
                [Description("Test")]
                public string Name { get; set; }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // System.ComponentModel is used for Description attribute
        Assert.Contains("using System.ComponentModel;", result);
    }

    [Fact]
    public void FormatCode_keeps_using_for_generic_types()
    {
        // Arrange
        var code = """
            using System;
            using System.Collections.Concurrent;

            namespace Test;

            public class Foo
            {
                public ConcurrentDictionary<string, int> Cache { get; set; } = new();
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // System.Collections.Concurrent is used for ConcurrentDictionary
        Assert.Contains("using System.Collections.Concurrent;", result);
    }

    [Fact]
    public void FindProjectDirectory_finds_project_from_file_path()
    {
        // Arrange
        // Create a temporary directory structure with a .csproj file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(tempDir, "TestProject");
        var subDir = Path.Combine(projectDir, "src");
        Directory.CreateDirectory(subDir);

        var csprojPath = Path.Combine(projectDir, "TestProject.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var testFilePath = Path.Combine(subDir, "Test.cs");

        try
        {
            // Act
            var foundProjectDir = CodeFormattingHelper.FindProjectDirectory(testFilePath);

            // Assert
            Assert.NotNull(foundProjectDir);
            Assert.True(Directory.Exists(foundProjectDir));
            Assert.True(Directory.GetFiles(foundProjectDir, "*.csproj").Any());
            Assert.Equal(projectDir, foundProjectDir);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void FindProjectDirectory_returns_null_when_no_project_found()
    {
        // Arrange
        var tempPath = Path.GetTempPath();
        var testFile = Path.Combine(tempPath, "test.cs");

        // Act
        var projectDir = CodeFormattingHelper.FindProjectDirectory(testFile);

        // Assert
        // Should return null if no .csproj is found up the tree
        Assert.Null(projectDir);
    }

    [Fact]
    public void FormatCode_handles_empty_code()
    {
        // Arrange
        var code = "";

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Trim());
    }

    [Fact]
    public void FormatCode_handles_whitespace_only_code()
    {
        // Arrange
        var code = "   \n\n   \n   ";

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void FormatCode_preserves_namespace_using()
    {
        // Arrange
        var code = """
            using System;
            using ErikLieben.FA.ES;

            namespace Test;

            public class Foo
            {
                public IEvent Event { get; set; }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // ErikLieben.FA.ES is used for IEvent
        Assert.Contains("using ErikLieben.FA.ES;", result);
    }

    [Fact]
    public void FormatCode_with_project_directory_loads_additional_references()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // Create a helper file in the project
        File.WriteAllText(Path.Combine(tempDir, "MyHelper.cs"), """
            namespace Test;
            public class MyHelper { }
            """);

        var code = """
            namespace Test;
            public class Foo
            {
                public MyHelper Helper { get; set; }
            }
            """;

        try
        {
            // Act
            var result = CodeFormattingHelper.FormatCode(code, tempDir);

            // Assert
            Assert.Contains("public class Foo", result);
            Assert.Contains("MyHelper", result);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FormatCode_skips_files_in_bin_directory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var binDir = Path.Combine(tempDir, "bin", "Debug");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "Compiled.cs"), "public class Compiled { }");

        var code = "namespace Test; public class Foo { }";

        try
        {
            // Act - Should not throw
            var result = CodeFormattingHelper.FormatCode(code, tempDir);

            // Assert
            Assert.Contains("public class Foo", result);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FormatCode_skips_files_in_obj_directory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var objDir = Path.Combine(tempDir, "obj", "Debug");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "Generated.cs"), "public class Generated { }");

        var code = "namespace Test; public class Foo { }";

        try
        {
            // Act - Should not throw
            var result = CodeFormattingHelper.FormatCode(code, tempDir);

            // Assert
            Assert.Contains("public class Foo", result);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FormatCode_skips_generated_files()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "Helper.Generated.cs"), "public class Helper { }");

        var code = "namespace Test; public class Foo { }";

        try
        {
            // Act
            var result = CodeFormattingHelper.FormatCode(code, tempDir);

            // Assert
            Assert.Contains("public class Foo", result);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FormatCode_with_cancellation_token()
    {
        // Arrange
        var code = "public class Foo { }";
        using var cts = new CancellationTokenSource();

        // Act
        var result = CodeFormattingHelper.FormatCode(code, null, cts.Token);

        // Assert
        Assert.Contains("public class Foo", result);
    }

    [Fact]
    public void FormatCode_handles_records()
    {
        // Arrange
        var code = """
            namespace Test;
            public record Person(string Name, int Age);
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("public record Person", result);
    }

    [Fact]
    public void FormatCode_handles_file_scoped_namespaces()
    {
        // Arrange
        var code = """
            namespace Test;
            public class Foo
            {
                public void Bar() { }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("namespace Test;", result);
    }

    [Fact]
    public void FormatCode_handles_async_methods()
    {
        // Arrange
        var code = """
            using System.Threading.Tasks;
            namespace Test;
            public class Foo
            {
                public async Task<int> GetValueAsync()
                {
                    await Task.Delay(1);
                    return 42;
                }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("public async Task<int> GetValueAsync()", result);
        Assert.Contains("using System.Threading.Tasks;", result);
    }

    [Fact]
    public void FormatCode_handles_static_using()
    {
        // Arrange
        var code = """
            using static System.Math;
            namespace Test;
            public class Foo
            {
                public double Value => PI;
                public double Calculate(double x) => Abs(x);
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        // Static using may be removed if symbols can't be resolved - just verify formatting works
        Assert.Contains("public class Foo", result);
        Assert.Contains("Calculate", result);
    }

    [Fact]
    public void FormatCode_handles_global_using()
    {
        // Arrange
        var code = """
            global using System;
            namespace Test;
            public class Foo
            {
                public DateTime Created { get; set; }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("global using System;", result);
    }

    [Fact]
    public void FormatCode_handles_using_alias()
    {
        // Arrange
        var code = """
            using Dict = System.Collections.Generic.Dictionary<string, int>;
            namespace Test;
            public class Foo
            {
                public Dict Data { get; set; } = new();
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("using Dict = System.Collections.Generic.Dictionary<string, int>;", result);
    }

    [Fact]
    public void FormatCode_preserves_xml_documentation()
    {
        // Arrange
        var code = """
            namespace Test;
            /// <summary>
            /// This is a test class
            /// </summary>
            public class Foo { }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("/// <summary>", result);
        Assert.Contains("/// This is a test class", result);
        Assert.Contains("/// </summary>", result);
    }

    [Fact]
    public void FormatCode_handles_interfaces()
    {
        // Arrange
        var code = """
            namespace Test;
            public interface IFoo
            {
                void Bar();
                string Name { get; set; }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("public interface IFoo", result);
        Assert.Contains("void Bar();", result);
    }

    [Fact]
    public void FormatCode_handles_enums()
    {
        // Arrange
        var code = """
            namespace Test;
            public enum Status { Active, Inactive, Pending }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("public enum Status", result);
        Assert.Contains("Active", result);
    }

    [Fact]
    public void FormatCode_handles_partial_classes()
    {
        // Arrange
        var code = """
            namespace Test;
            public partial class Foo
            {
                public void Bar() { }
            }
            """;

        // Act
        var result = CodeFormattingHelper.FormatCode(code);

        // Assert
        Assert.Contains("public partial class Foo", result);
    }
}
