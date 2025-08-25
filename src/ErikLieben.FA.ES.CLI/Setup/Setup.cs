namespace ErikLieben.FA.ES.CLI.Setup;

public class Setup
{
    public async Task Initialize(string solutionPath)
    {
        var elfaDirectoryPath = Path.Combine(solutionPath, ".elfa");
        if (!Directory.Exists(elfaDirectoryPath))
        {
            Directory.CreateDirectory(elfaDirectoryPath);
        }

        var esDirectoryPath = Path.Combine(elfaDirectoryPath, "es");
        if (!Directory.Exists(esDirectoryPath))
        {
            Directory.CreateDirectory(esDirectoryPath);
        }

        var ffDirectoryPath = Path.Combine(elfaDirectoryPath, "ff");
        if (!Directory.Exists(ffDirectoryPath))
        {
            Directory.CreateDirectory(ffDirectoryPath);
        }

        var configFilePath = Path.Combine(elfaDirectoryPath, "config.json");
        if (!File.Exists(configFilePath))
        {
            await File.WriteAllTextAsync(configFilePath, "{}");
        }
    }
}
