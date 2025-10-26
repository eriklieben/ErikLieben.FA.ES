using System;
using System.IO;
using System.Threading.Tasks;
using CliSetup = ErikLieben.FA.ES.CLI.Setup.Setup;
using Xunit;

namespace ErikLieben.FA.ES.CLI.Tests.Setup;

public class SetupTests
{
    [Fact]
    public async Task Initialize_creates_expected_directories_and_config_when_missing()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            // Act
            await CliSetup.Initialize(root);

            // Assert
            var elfa = Path.Combine(root, ".elfa");
            var es = Path.Combine(elfa, "es");
            var ff = Path.Combine(elfa, "ff");
            var cfg = Path.Combine(elfa, "config.json");

            Assert.True(Directory.Exists(elfa));
            Assert.True(Directory.Exists(es));
            Assert.True(Directory.Exists(ff));
            Assert.True(File.Exists(cfg));

            var content = await File.ReadAllTextAsync(cfg);
            Assert.Equal("{}", content);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Initialize_is_idempotent_and_does_not_overwrite_existing_config()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var elfa = Path.Combine(root, ".elfa");
        var es = Path.Combine(elfa, "es");
        var ff = Path.Combine(elfa, "ff");
        Directory.CreateDirectory(elfa);
        Directory.CreateDirectory(es);
        Directory.CreateDirectory(ff);
        var cfg = Path.Combine(elfa, "config.json");
        await File.WriteAllTextAsync(cfg, "{\"preset\":true}");

        try
        {
            // Act - run twice
            await CliSetup.Initialize(root);
            await CliSetup.Initialize(root);

            // Assert
            Assert.True(Directory.Exists(elfa));
            Assert.True(Directory.Exists(es));
            Assert.True(Directory.Exists(ff));
            Assert.True(File.Exists(cfg));

            // Existing content should not be overwritten (Initialize only writes when file doesn't exist)
            var content = await File.ReadAllTextAsync(cfg);
            Assert.Equal("{\"preset\":true}", content);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }
}
